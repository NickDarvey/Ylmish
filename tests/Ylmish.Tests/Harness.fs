module Ylmish.Harness

// =============================================================================
// Plan 0002, Step 2a — the differential harness (the plan's verification
// backbone, L2). Adapted from the reference branch's Harness.fs (its plan 0006
// Step 0), whose design transfers: no self-authored spec to be wrong about.
//
// Three ideas:
//
//   1. A `Bridge` is anything that can take a *whole immutable model* and push
//      it into a Y.Doc (`Apply`), and read a converged model back out (`Read`).
//      This is exactly the contract `withYlmish` has with the codec. Today's
//      bridge is the `materialize`/`dematerialize` path; the oracle is a
//      hand-written element-wise program directly on a `Y.Array`.
//
//   2. A `run` driver replays a schedule (per-replica ops + a delivery policy)
//      against two docs wired with a bridge, exchanges Yjs updates, and reports
//      the converged read-back on each replica. `Immediate` delivery ships each
//      change before the next op (no concurrency window); `Concurrent` holds
//      everything and exchanges once (maximal concurrency).
//
//   3. `differential` runs the system-under-test and the raw-Yjs oracle through
//      the SAME schedule and compares converged membership. The oracle defines
//      the intended CRDT semantics by construction.
//
// Step 2a's calibration triad (the tests at the bottom of this file):
//   - GREEN on the oracle: ground truth holds (concurrent adds both survive).
//   - CATCHES the known #83-class bug: the materialize path loses a concurrent
//     add, and the harness names the lost id. (Expressed as a passing test that
//     ASSERTS the mismatch — stronger than a pending test, and the suite stays
//     green. When Step 5's binding layer fixes the bug, this test fails and is
//     flipped into the fix's regression test.)
//   - DISCRIMINATES: the same materialize path MATCHES the oracle when edits
//     are sequential — the harness is not simply hostile to the old code.
//
// Also included (used from Step 5 onward): `incrementalBytes`/`measureApplies`,
// the O(delta)-vs-O(state) minimality meter.
// =============================================================================

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

// -----------------------------------------------------------------------------
// Model + operations (membership core: Add/Remove of uniquely-identified items;
// Edit/Move are in the alphabet so later steps extend the oracle, not the shape)
// -----------------------------------------------------------------------------

/// An item with a stable identity and a (future) collaborative text field.
type Item = { Id : string; Text : string }

type Model = Item list

type Op =
    | Add of id : string * text : string
    | Remove of id : string
    | Edit of id : string * text : string
    | Move of id : string * toIndex : int

/// The pure MVU `update`. Adds are idempotent on id (set membership).
let applyOp (m : Model) (op : Op) : Model =
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

/// Membership as a set of ids — the no-loss comparison.
let idSet (m : Model) : Set<string> = m |> List.map (fun i -> i.Id) |> Set.ofList

// -----------------------------------------------------------------------------
// Bridge contract
// -----------------------------------------------------------------------------

/// Whole immutable models in, a converged model out — `withYlmish`'s contract.
type Bridge =
    { Name : string
      Doc : Y.Doc
      Apply : Model -> unit
      Read : unit -> Model }

type BridgeFactory = Y.Doc -> Bridge

// -----------------------------------------------------------------------------
// Bridge 1 — today's materialize/dematerialize path (the system-under-test the
// harness must catch being wrong; replaced by the binding layer in Step 5).
// -----------------------------------------------------------------------------

module Materialize =
    let private encodeModel (m : Model) : Encoded<Element<string>> =
        let ids = m |> List.map (fun i -> i.Id)
        Encode.object [
            "items", Encode.list (fun (s : string) -> Encode.value id (AVal.constant s)) (AList.ofList ids)
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
            { Name = "materialize"
              Doc = doc
              Apply = fun m -> Y.Doc.materialize doc (encodeModel m)
              Read =
                fun () ->
                    let demat = Y.Doc.dematerialize doc
                    match Decode.run ([] : Model) decoder (AVal.constant (Some demat)) |> AVal.force with
                    | Ok m -> m
                    | Error e -> failwithf "materialize read: decode failed: %A" e }

// -----------------------------------------------------------------------------
// Bridge 2 — the raw-Yjs ORACLE (ground truth). Element-wise on a stable
// root `Y.Array` of ids: an Add is one insert, a Remove one delete. The array
// keeps its identity and accumulates ops, so concurrent adds both survive —
// the intended CRDT semantics, by construction (U2b/U8).
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
                        // Delete unwanted ids, highest index first so indices stay valid.
                        cur
                        |> List.mapi (fun i id -> i, id)
                        |> List.filter (fun (_, id) -> not (Set.contains id wantSet))
                        |> List.map fst
                        |> List.sortDescending
                        |> List.iter (fun i -> arr.delete (float i, 1.0))
                        for id in want do
                            if not (Set.contains id curSet) then arr.push [| box id |]) }

// -----------------------------------------------------------------------------
// Schedule driver
// -----------------------------------------------------------------------------

type ReplicaOp = { Replica : int; Op : Op }

/// `Immediate`: each local change ships to the peer before the next op (no
/// concurrency window). `Concurrent`: hold everything, exchange once at the end.
type Delivery =
    | Immediate
    | Concurrent

type RunResult =
    { Final : Model []        // converged read-back per replica
      Converged : bool        // replicas agree (by id-set)
      Ids : Set<string>[] }   // per-replica membership, for differential checks

/// Full-state exchange i -> j. Full-state updates are idempotent and
/// order-independent, so partition/duplication/reordering come for free.
let private syncFromTo (docs : Y.Doc[]) i j =
    Y.applyUpdate (docs.[j], Y.encodeStateAsUpdate docs.[i], box "remote")

let private syncAll (docs : Y.Doc[]) =
    for _ in 1 .. 2 do
        syncFromTo docs 0 1
        syncFromTo docs 1 0

let run (factory : BridgeFactory) (delivery : Delivery) (ops : ReplicaOp list) : RunResult =
    let docs = [| Y.Doc.Create (); Y.Doc.Create () |]
    let bridges = docs |> Array.map factory
    let models = [| ([] : Model); ([] : Model) |]

    // Deliver i -> j, then refresh replica j's model from the converged Y state —
    // this is `withYlmish`'s remote-update → read-back → `Set` loop. Modelling it
    // is what makes the harness fair: without it even sequential edits would
    // "lose", and the concurrent loss it reports would not be a CRDT-layer fact.
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
// Differential check: SUT vs the raw-Yjs oracle on the same schedule
// -----------------------------------------------------------------------------

type Diff =
    { Converged : bool          // SUT replicas agree
      OracleIds : Set<string>   // ground-truth membership
      SutIds : Set<string>      // SUT membership (replica 0 after convergence)
      MatchesOracle : bool
      Lost : Set<string> }      // ids the oracle kept but the SUT dropped

let differential (sut : BridgeFactory) (delivery : Delivery) (ops : ReplicaOp list) : Diff =
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
// Minimality meter (used from Step 5): incremental Yjs update bytes per apply.
// -----------------------------------------------------------------------------

/// Bytes a single `apply` adds to the doc. Element-wise bridges are O(delta);
/// whole-tree materialization is O(state).
let incrementalBytes (doc : Y.Doc) (apply : unit -> unit) : int =
    let svBefore = Y.encodeStateVectorFromUpdate (Y.encodeStateAsUpdate doc)
    apply ()
    let inc = Y.encodeStateAsUpdate (doc, svBefore)
    int inc.length

let measureApplies (factory : BridgeFactory) (models : Model list) : int list =
    let doc = Y.Doc.Create ()
    let bridge = factory doc
    models |> List.map (fun m -> incrementalBytes doc (fun () -> bridge.Apply m))

// -----------------------------------------------------------------------------
// Step 2a calibration triad — proves the harness is trustworthy.
// -----------------------------------------------------------------------------

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let private add r id = { Replica = r; Op = Add (id, "") }

let tests = testList "Harness" [
    testList "Step 2a calibration" [

        // GREEN ON THE ORACLE: ground truth holds. If this failed, the oracle
        // itself would be wrong and nothing downstream could be trusted.
        test "oracle: two concurrent adds both survive (ground truth is no-loss)" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let r = run RawYjs.factory Concurrent ops
            Expect.isTrue r.Converged "oracle replicas must converge"
            Expect.equal r.Ids.[0] (Set.ofList [ "a"; "b" ])
                "element-wise Yjs keeps BOTH concurrent adds — the intended semantics"
        }

        // CATCHES THE KNOWN BUG (issue #83's class): differential testing
        // reports intention loss on the materialize path and names the lost id.
        // NB: asserted as a mismatch so the suite stays green while pinning that
        // the harness catches it. Step 5 flips this into a regression test.
        test "materialize path: loses a concurrent add — and the harness catches it" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let d = differential Materialize.factory Concurrent ops
            Expect.equal d.OracleIds (Set.ofList [ "a"; "b" ]) "oracle keeps both (ground truth)"
            Expect.isFalse d.MatchesOracle
                "the materialize path must DISAGREE with the oracle — if this fails, either the bug is fixed (move this assertion) or the harness is blind"
            Expect.isNonEmpty (Set.toList d.Lost)
                "the harness pinpoints the lost id (whole-tree LWW dropped one peer's add)"
            Expect.equal (Set.count d.SutIds) 1
                "whole-tree LWW: exactly one peer's single-item array survives"
        }

        // DISCRIMINATES: with no concurrency window the same materialize path
        // matches the oracle — the harness is not simply hostile to the old code.
        test "materialize path: matches the oracle when edits are sequential" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let d = differential Materialize.factory Immediate ops
            Expect.isTrue d.MatchesOracle "no concurrency window: read-back folds the remote add in"
            Expect.equal d.SutIds (Set.ofList [ "a"; "b" ]) "both items present sequentially"
        }

        // Convergence alone is not correctness: the materialize path CONVERGES
        // on the lossy state. This is why the differential check, not
        // convergence, is the real gate.
        test "materialize path: converges, but on the wrong (lossy) state" {
            let r = run Materialize.factory Concurrent [ add 0 "a"; add 1 "b" ]
            Expect.isTrue r.Converged "both replicas reach the same state"
            Expect.equal (Set.count r.Ids.[0]) 1 "that state has lost an item"
        }

        // Minimality meter sanity (the Step 5 perf gate, calibrated now):
        // adding 1 item on top of 20 — the oracle ships one insert, the
        // materialize path re-ships the whole array.
        test "minimality meter: oracle add is O(delta), materialize is O(state)" {
            let models =
                [ 0 .. 20 ]
                |> List.map (fun n ->
                    [ 0 .. n - 1 ] |> List.map (fun k -> { Id = sprintf "item-%02d" k; Text = "" }))
            let rawSizes = measureApplies RawYjs.factory models
            let materializeSizes = measureApplies Materialize.factory models
            let rawLast = List.last rawSizes
            let materializeLast = List.last materializeSizes
            Expect.isTrue (rawLast < materializeLast)
                (sprintf "adding 1 item to 20: oracle=%d bytes (O(delta)) must be smaller than materialize=%d bytes (O(state))"
                    rawLast materializeLast)
        }
    ]
]
