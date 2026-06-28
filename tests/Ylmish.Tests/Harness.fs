module Ylmish.Harness

// =============================================================================
// Plan 0006 — Step 0: the validation harness + oracle (option-independent).
//
// This module is the *heart* of plan 0006. It is built ONCE and every candidate
// option (A–E) is judged by it. Nothing here commits to a design; it only
// defines what "correct" means operationally and gives us a trustworthy way to
// detect when a bridge is wrong.
//
// The harness is built around three ideas:
//
//   1. A `Bridge` is anything that can take a *whole immutable model* and push it
//      into a Y.Doc (`Apply`), and read a converged model back out (`Read`).
//      This is exactly the contract Ylmish's `withYlmish` has with the codec:
//      successive whole models in, a converged model out. Today's bridge is the
//      `materialize`/`dematerialize` hybrid; the oracle is a hand-written
//      element-wise program directly on `Y.Array`.
//
//   2. A `run` driver replays a *schedule* (per-replica ops + a delivery policy)
//      against two docs wired with a bridge, exchanges Yjs updates, and reports
//      the converged read-back on each replica.
//
//   3. Differential testing (M1): the raw-Yjs bridge *defines* the intended CRDT
//      semantics, so we take its converged state as ground truth and assert the
//      bridge-under-test produces the same one. No self-authored spec to be wrong
//      about.
//
// Acceptance (Step 0 exit, asserted in the tests below): the harness reports the
// raw-Yjs oracle as correct (no violations) and *catches* today's hybrid bug —
// a concurrent add is silently lost under whole-container LWW. A harness that
// cannot fail on the known bug is worthless, so we pin that it does.
//
// Scope note: Step 0 exercises the **no-loss / membership** core (Add/Remove of
// uniquely-identified items), which is enough to reproduce the known bug and is
// an unimpeachable ground truth (a Y.Array of ids, element-wise). The `Op`/`Item`
// types already carry text + order so Steps 2+ can extend the oracle without
// reshaping the harness.
// =============================================================================

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

// -----------------------------------------------------------------------------
// Model + operations (the representative datatype, membership core for Step 0)
// -----------------------------------------------------------------------------

/// An item with a stable identity and a (future) collaborative text field. Step 0
/// only uses `Id`; `Text` is along for the ride so Steps 2+ reuse this verbatim.
type Item = { Id : string; Text : string }

type Model = Item list

/// The full op alphabet. Step 0 generates only `Add`/`Remove`; `Edit`/`Move`
/// exist so later steps extend the oracle without changing call sites.
type Op =
    | Add of id: string * text: string
    | Remove of id: string
    | Edit of id: string * text: string
    | Move of id: string * toIndex: int

/// Apply an op to a plain immutable model — the pure MVU `update`. Adds are
/// idempotent on id (re-adding an existing id is a no-op, matching set membership).
let applyOp (m: Model) (op: Op) : Model =
    match op with
    | Add (id, text) ->
        if m |> List.exists (fun i -> i.Id = id) then m
        else m @ [ { Id = id; Text = text } ]
    | Remove id -> m |> List.filter (fun i -> i.Id <> id)
    | Edit (id, text) ->
        m |> List.map (fun i -> if i.Id = id then { i with Text = text } else i)
    | Move (id, toIndex) ->
        match m |> List.tryFind (fun i -> i.Id = id) with
        | None -> m
        | Some item ->
            let without = m |> List.filter (fun i -> i.Id <> id)
            let idx = max 0 (min toIndex (List.length without))
            (without |> List.take idx) @ [ item ] @ (without |> List.skip idx)

/// Membership as a set of ids — the comparison Step 0 cares about (no-loss).
let idSet (m: Model) : Set<string> = m |> List.map (fun i -> i.Id) |> Set.ofList

// -----------------------------------------------------------------------------
// Bridge contract
// -----------------------------------------------------------------------------

/// A way to push whole immutable models into a Y.Doc and read converged models
/// back out — the same contract `withYlmish` has with the codec.
type Bridge =
    { Name : string
      Doc : Y.Doc
      Apply : Model -> unit
      Read : unit -> Model }

/// Attach a bridge to a (fresh) doc.
type BridgeFactory = Y.Doc -> Bridge

// -----------------------------------------------------------------------------
// Bridge 1 — today's Ylmish hybrid (materialize / dematerialize). Expected to
// lose concurrent structural edits (whole-container LWW). This is the
// system-under-test we must catch being wrong.
// -----------------------------------------------------------------------------

module Hybrid =
    let private encodeModel (m: Model) : Encoded<Element<string>> =
        let ids = m |> List.map (fun i -> i.Id)
        Encode.object [
            "items", Encode.list (fun (s: string) -> Encode.value id (AVal.constant s)) (AList.ofList ids)
        ]

    let private decoder : Decoder<Model, Element<string>, Model> =
        Decode.object {
            let! items = Decode.object.required "items" (Decode.list.required Decode.value)
            return
                items
                |> IndexList.toList
                |> List.map (fun o -> { Id = string o; Text = "" })
        }

    let factory : BridgeFactory =
        fun doc ->
            { Name = "hybrid"
              Doc = doc
              Apply = fun m -> Y.Doc.materialize doc (encodeModel m)
              Read =
                fun () ->
                    let demat = Y.Doc.dematerialize doc
                    match Decode.run ([] : Model) decoder (AVal.constant (Some demat)) |> AVal.force with
                    | Ok m -> m
                    | Error e -> failwithf "hybrid read: decode failed: %A" e }

// -----------------------------------------------------------------------------
// Bridge 2 — the raw-Yjs ORACLE (ground truth). A hand-written, element-wise
// program directly on a stable `Y.Array` of ids: an Add is a single insert, a
// Remove a single delete. Because the array keeps its identity and accumulates
// ops, concurrent adds both survive — this is the intended CRDT semantics, by
// construction. Step 0 membership only (ids); Text/Move handled in later steps.
// -----------------------------------------------------------------------------

module RawYjs =
    let factory : BridgeFactory =
        fun doc ->
            let arr : Y.Array<obj> = doc.getArray "items"
            let currentIds () = arr.toArray () |> Seq.map string |> Seq.toList
            { Name = "rawYjs"
              Doc = doc
              Read = fun () -> currentIds () |> List.map (fun id -> { Id = id; Text = "" })
              Apply =
                fun m ->
                    let want = m |> List.map (fun i -> i.Id)
                    let wantSet = Set.ofList want
                    doc.transact (fun _ ->
                        let cur = currentIds ()
                        let curSet = Set.ofList cur
                        // Delete ids no longer wanted, high index first so indices stay valid.
                        cur
                        |> List.mapi (fun i id -> i, id)
                        |> List.filter (fun (_, id) -> not (Set.contains id wantSet))
                        |> List.map fst
                        |> List.sortDescending
                        |> List.iter (fun i -> arr.delete (float i, 1.0))
                        // Insert wanted ids not yet present.
                        for id in want do
                            if not (Set.contains id curSet) then arr.push [| box id |]) }

// -----------------------------------------------------------------------------
// Schedule driver
// -----------------------------------------------------------------------------

/// Which replica issues an op. Two replicas (0, 1) is enough for every
/// concurrency hazard at this stage (N=3 enumeration arrives with M3 later).
type ReplicaOp = { Replica : int; Op : Op }

/// When updates cross the wire. `Immediate` ships each local change to the peer
/// right away (no concurrency window); `Concurrent` holds all changes and
/// exchanges once at the end (maximal concurrency — the bug-exposing schedule).
type Delivery =
    | Immediate
    | Concurrent

type RunResult =
    { Final : Model []        // converged read-back per replica
      Converged : bool        // P1: replicas agree (by id-set)
      Ids : Set<string>[] }   // per-replica membership, for differential checks

/// Full-state Yjs exchange from replica i to replica j. Full-state updates are
/// idempotent and order-independent, so this models partition/duplication/
/// reordering for free (re-applying or reordering full state always converges) —
/// a cheap, strong P1 probe without bespoke transport plumbing.
let private syncFromTo (docs: Y.Doc[]) i j =
    Y.applyUpdate (docs.[j], Y.encodeStateAsUpdate docs.[i], box "remote")

/// Several bidirectional rounds — idempotent, guarantees both directions land.
let private syncAll (docs: Y.Doc[]) =
    for _ in 1 .. 2 do
        syncFromTo docs 0 1
        syncFromTo docs 1 0

let run (factory: BridgeFactory) (delivery: Delivery) (ops: ReplicaOp list) : RunResult =
    let docs = [| Y.Doc.Create (); Y.Doc.Create () |]
    let bridges = docs |> Array.map factory
    let models = [| ([] : Model); ([] : Model) |]

    // Deliver i -> j, then refresh replica j's model from the converged Y state.
    // That refresh is `withYlmish`'s remote-update → read-back → `Set` loop: a peer
    // that receives a remote change folds it into its own model before its next
    // local op. Without this the hybrid would "lose" even purely-sequential edits,
    // so modelling it is what makes the harness fair (and makes the *concurrent*
    // loss it reports a genuine CRDT-layer loss, not a missing read-back).
    let deliver i j =
        syncFromTo docs i j
        models.[j] <- bridges.[j].Read ()

    for { Replica = i; Op = op } in ops do
        models.[i] <- applyOp models.[i] op
        bridges.[i].Apply models.[i]
        match delivery with
        | Immediate ->
            deliver i (1 - i)
            deliver (1 - i) i
        | Concurrent -> ()

    syncAll docs

    let final = bridges |> Array.map (fun b -> b.Read ())
    let ids = final |> Array.map idSet
    { Final = final
      Converged = ids.[0] = ids.[1]
      Ids = ids }

// -----------------------------------------------------------------------------
// Differential check (M1): SUT vs raw-Yjs oracle on the same schedule
// -----------------------------------------------------------------------------

type Diff =
    { Converged : bool          // P1 for the SUT
      OracleIds : Set<string>   // ground-truth membership
      SutIds : Set<string>      // SUT membership (replica 0 after convergence)
      MatchesOracle : bool      // M1 / P2: SUT == oracle
      Lost : Set<string> }      // ids the oracle kept but the SUT dropped

/// Run `sut` and the raw-Yjs oracle through the same schedule and compare. The
/// oracle's converged membership is ground truth (it is element-wise Yjs by
/// construction); any id present there but missing in the SUT is intention loss.
let differential (sut: BridgeFactory) (delivery: Delivery) (ops: ReplicaOp list) : Diff =
    let s = run sut delivery ops
    let o = run RawYjs.factory delivery ops
    let oracleIds = o.Ids.[0]
    let sutIds = s.Ids.[0]
    { Converged = s.Converged
      OracleIds = oracleIds
      SutIds = sutIds
      MatchesOracle = sutIds = oracleIds
      Lost = Set.difference oracleIds sutIds }

// -----------------------------------------------------------------------------
// Minimality meter (M5 / P4 perf gate): incremental Yjs update size per apply.
// -----------------------------------------------------------------------------

/// Bytes a single `apply` adds to the doc — the real op size. We snapshot the
/// state vector before and ask Yjs for exactly the delta after. Element-wise
/// bridges are O(Δ) (an add ships one insert); whole-container LWW is O(|state|)
/// (it re-ships the whole array each time).
let incrementalBytes (doc: Y.Doc) (apply: unit -> unit) : int =
    let svBefore = Y.encodeStateVectorFromUpdate (Y.encodeStateAsUpdate doc)
    apply ()
    let inc = Y.encodeStateAsUpdate (doc, svBefore)
    int inc.length

/// Apply a growing sequence of models to a single fresh doc and return the
/// incremental update size for each apply — used to compare O(Δ) vs O(|state|).
let measureApplies (factory: BridgeFactory) (models: Model list) : int list =
    let doc = Y.Doc.Create ()
    let bridge = factory doc
    models |> List.map (fun m -> incrementalBytes doc (fun () -> bridge.Apply m))

// -----------------------------------------------------------------------------
// Step 0 acceptance tests — prove the harness is trustworthy: green on the
// oracle, RED on today's known hybrid bug. (Expressed as passing tests that
// *assert* the bug, so `npm test` stays green while documenting the failure.)
// -----------------------------------------------------------------------------

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let private add r id = { Replica = r; Op = Add (id, "") }

let tests = testList "Harness (0006)" [
    testList "Step 0 — harness acceptance" [

        // BASELINE: the oracle preserves both concurrent adds. If this failed the
        // oracle itself would be wrong and nothing downstream could be trusted.
        test "raw-Yjs oracle preserves two concurrent adds (ground truth is no-loss)" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let r = run RawYjs.factory Concurrent ops
            Expect.isTrue r.Converged "oracle replicas must converge"
            Expect.equal r.Ids.[0] (Set.ofList [ "a"; "b" ])
                "element-wise Yjs keeps BOTH concurrent adds — this is the intended semantics"
        }

        // ACCEPTANCE (the whole point of Step 0): the harness must CATCH today's
        // bug. Driving the materialize/dematerialize hybrid through the same
        // two-concurrent-add schedule, differential testing vs the oracle reports
        // intention loss — exactly the "a peer's concurrent add is clobbered" bug.
        test "harness catches the hybrid's lost concurrent add (red on the known bug)" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let d = differential Hybrid.factory Concurrent ops
            Expect.equal d.OracleIds (Set.ofList [ "a"; "b" ]) "oracle keeps both (ground truth)"
            Expect.isFalse d.MatchesOracle
                "the hybrid must DISAGREE with the oracle — if this passes, the bug is gone or the harness is blind"
            Expect.isNonEmpty (Set.toList d.Lost)
                "the harness pinpoints the lost id (whole-container LWW dropped one peer's add)"
            Expect.equal (Set.count d.SutIds) 1
                "whole-container LWW: exactly one peer's single-item array survives"
        }

        // DISCRIMINATION: the harness is not simply hostile to the hybrid. With no
        // concurrency window (each add delivered before the next), the hybrid's
        // read-back loop folds the remote add in, so it matches the oracle. A
        // harness that failed the hybrid even here would be untrustworthy.
        test "hybrid matches the oracle when edits are sequential (harness discriminates)" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let d = differential Hybrid.factory Immediate ops
            Expect.isTrue d.MatchesOracle "no concurrency window: hybrid read-back recovers both adds"
            Expect.equal d.SutIds (Set.ofList [ "a"; "b" ]) "both items present sequentially"
        }

        // P1 vs P2: the hybrid still *converges* (both replicas agree) — it simply
        // converges on the wrong, lossy state. This pins that "converges" alone is
        // not "correct", which is why P2 (no-loss vs the oracle) is the real gate.
        test "hybrid converges but violates no-loss (P1 holds, P2 fails)" {
            let r = run Hybrid.factory Concurrent [ add 0 "a"; add 1 "b" ]
            Expect.isTrue r.Converged "P1: both replicas reach the same state"
            Expect.equal (Set.count r.Ids.[0]) 1 "P2 violated: that state has lost an item"
        }

        // MINIMALITY (M5 / P4 perf gate): element-wise is O(Δ), LWW is O(|state|).
        // Add one item on top of 20 existing; the oracle ships a single insert
        // while the hybrid re-ships the whole 21-item array.
        test "minimality: raw-Yjs add is O(Δ); hybrid re-materialize is O(|state|)" {
            // Successive whole models: [], [i0], [i0;i1], ... up to 21 items. The
            // last apply adds exactly one item on top of 20.
            let models =
                [ 0 .. 20 ]
                |> List.map (fun n ->
                    [ 0 .. n - 1 ] |> List.map (fun k -> { Id = sprintf "item-%02d" k; Text = "" }))
            let rawSizes = measureApplies RawYjs.factory models
            let hybridSizes = measureApplies Hybrid.factory models
            let rawLast = List.last rawSizes
            let hybridLast = List.last hybridSizes
            Expect.isTrue (rawLast < hybridLast)
                (sprintf "adding 1 item to 20: raw=%d bytes (O(Δ)) must be smaller than hybrid=%d bytes (O(|state|))"
                    rawLast hybridLast)
        }
    ]
]
