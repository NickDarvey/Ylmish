module Ylmish.Harness

// =============================================================================
// Plan 0002, Step 2a — the differential harness (the plan's verification
// backbone, L2). Adapted from the reference branch's Harness.fs (its plan 0006
// Step 0), whose design transfers: no self-authored spec to be wrong about.
//
// Three ideas:
//
//   1. A `Bridge` is anything that can push a change into a Y.Doc (`Apply`)
//      and read a converged model back out (`Read`). `Apply` receives both
//      the op and the pure post-op model: a whole-model system (withYlmish)
//      uses the op only to phrase an intent-shaped message, while the oracle
//      interprets the op element-wise — that contrast IS the differential.
//
//   2. A `run` driver replays a schedule (per-replica ops + a delivery policy)
//      against two docs wired with a bridge, exchanges Yjs updates, and reports
//      the converged read-back on each replica. `Immediate` delivery ships each
//      change before the next op (no concurrency window); `Concurrent` holds
//      everything and exchanges once (maximal concurrency).
//
//   3. `differential` runs the system-under-test and the raw-Yjs oracle through
//      the SAME schedule and compares the converged models. The oracle defines
//      the intended CRDT semantics by construction.
//
// Step 2a calibrated this harness with a triad: green on the oracle, red on
// the then-live materialize path (it lost a concurrent add and the harness
// named the lost id), and discriminating (the same path matched the oracle
// sequentially). Step 7 deleted that path; the oracle ground-truth tests
// remain below, and the binding-vs-oracle check lives in the Binding tests.
//
// Step 9 extended the model to the plan's stress trio — keyed items + a
// collaborative body text + an LWW note register — and made the oracle
// op-driven so text splices have an element-wise ground truth. `run` pins
// the replicas' clientIDs (Yjs breaks concurrency ties by clientID, U3/U4),
// so every schedule replays to ONE deterministic outcome and the SUT and
// oracle runs share tiebreaks — full models compare exactly, not just
// membership.
//
// The decode-direction signal is part of the differential too: every bridge
// reports how many times it was told "a foreign transaction changed something
// you bind" (withYlmish's `Set`; the oracle's deep observers on its own
// roots). `Noise` ops write to a top-level root NEITHER side binds — an
// editor's own fragment sharing the doc — so they must leave both models AND
// both signal counts untouched.
//
// Also included (used from Step 5 onward): `incrementalBytes`, the
// O(delta)-vs-O(state) minimality meter.
// =============================================================================

open System

open FSharp.Data.Adaptive
open Yjs

open Ylmish

// -----------------------------------------------------------------------------
// Model + operations. Items are the membership core (Add/Remove of uniquely-
// identified items; per-item Edit is an LWW title register); Body is a
// collaborative text edited by splices; Note is a top-level LWW register.
// -----------------------------------------------------------------------------

/// An item with a stable identity and a title (an LWW register per item).
type Item = { Id : string; Text : string }

/// The harness model: a keyed collection + a collaborative text + a register —
/// plan 0002 Step 9's trio. Steps before 9 exercised `Items` only.
type Model = { Items : Item list; Body : string; Note : string }

module Model =
    let empty = { Items = []; Body = ""; Note = "" }
    /// Item order is not part of the convergence claim for keyed items —
    /// normalize before comparing whole models.
    let normalize (m : Model) = { m with Items = m.Items |> List.sortBy (fun i -> i.Id) }

type Op =
    | Add of id : string * text : string
    | Remove of id : string
    | Edit of id : string * text : string
    | Move of id : string * toIndex : int
    | SpliceBody of at : int * removed : int * inserted : string
    | SetNote of value : string
    /// A write by something else sharing the doc, into a top-level root no
    /// codec binds (`noiseRoot`) — no model effect, and no signal either.
    | Noise of text : string

/// The unbound root `Noise` writes to: a stand-in for an editor's own
/// fragment (y-prosemirror's, say) living in the same Y.Doc.
let noiseRoot = "draft"

/// The origin `Noise` writes carry: a foreign local writer, neither the
/// binding's own origin nor a remote apply.
let noiseOrigin : obj = box "editor"

/// The one `Noise` write, shared by every bridge: the op's effect on the doc
/// is not the system under test, only the system's reaction to it.
let writeNoise (doc : Y.Doc) (text : string) =
    if text <> "" then
        Y.transact (doc, (fun _ -> (doc.getText noiseRoot : Y.Text).insert (0, text)), noiseOrigin)

/// Splice semantics shared by the pure model and the oracle: clamp into range
/// (mirroring `Ylmish.Text`'s documented clamping contract) and elide
/// content-neutral results (`Text` elides those; an oracle that wrote them
/// would carry CRDT items the SUT deliberately never creates, and the two
/// docs would diverge on merge for no semantic reason).
let clampSplice (content : string) (at : int) (removed : int) (inserted : string) : (int * int * string) option =
    let at = max 0 (min at content.Length)
    let removed = max 0 (min removed (content.Length - at))
    let next = content.Substring (0, at) + inserted + content.Substring (at + removed)
    if next = content then None else Some (at, removed, inserted)

/// The pure MVU `update`. Adds are idempotent on id (set membership); edits
/// touch existing items only.
let applyOp (m : Model) (op : Op) : Model =
    match op with
    | Add (id, text) ->
        if m.Items |> List.exists (fun i -> i.Id = id) then m
        else { m with Items = m.Items @ [ { Id = id; Text = text } ] }
    | Remove id -> { m with Items = m.Items |> List.filter (fun i -> i.Id <> id) }
    | Edit (id, text) ->
        { m with Items = m.Items |> List.map (fun i -> if i.Id = id then { i with Text = text } else i) }
    | Move (id, toIndex) ->
        match m.Items |> List.tryFind (fun i -> i.Id = id) with
        | None -> m
        | Some item ->
            let without = m.Items |> List.filter (fun i -> i.Id <> id)
            let idx = max 0 (min toIndex (List.length without))
            { m with Items = (without |> List.take idx) @ [ item ] @ (without |> List.skip idx) }
    | SpliceBody (at, removed, inserted) ->
        match clampSplice m.Body at removed inserted with
        | None -> m
        | Some (at, removed, inserted) ->
            { m with Body = m.Body.Substring (0, at) + inserted + m.Body.Substring (at + removed) }
    | SetNote value -> { m with Note = value }
    | Noise _ -> m

/// Membership as a set of ids — the no-loss comparison.
let idSet (m : Model) : Set<string> = m.Items |> List.map (fun i -> i.Id) |> Set.ofList

// -----------------------------------------------------------------------------
// Bridge contract
// -----------------------------------------------------------------------------

type Bridge =
    { Name : string
      Doc : Y.Doc
      /// The op plus the pure post-op model. withYlmish-style bridges dispatch
      /// the op as an intent-shaped message (their contract is whole models);
      /// the oracle interprets the op element-wise.
      Apply : Op -> Model -> unit
      Read : unit -> Model
      /// How many decode signals this bridge has received so far: one per
      /// foreign transaction that changed something it binds (U14), none for
      /// its own writes (U6) or for writes to roots it doesn't bind.
      Signals : unit -> int }

type BridgeFactory = Y.Doc -> Bridge

/// Wrap a `withYlmish` program so every `Set` it processes bumps `count` — a
/// withYlmish bridge's `Signals`.
let countingSets (count : int ref) (program : Elmish.Program<'arg, 'model, Ylmish.Program.Message<'msg>, 'view>) =
    program
    |> Elmish.Program.map
        id
        (fun update -> fun msg model ->
            match msg with
            | Ylmish.Program.Message.Set -> count.Value <- count.Value + 1
            | Ylmish.Program.Message.User _ -> ()
            update msg model)
        id id id id

// -----------------------------------------------------------------------------
// The raw-Yjs ORACLE (ground truth). Element-wise on stable roots: a Y.Map of
// items (per-key merge, U2b), a Y.Text body (splices merge, U3), and a "note"
// key in the argless root map (LWW register, U4). Every op is one primitive
// Yjs operation — the intended CRDT semantics, by construction.
//
// Its signal count comes from deep observers on exactly those three roots,
// deduplicated per transaction and filtered to origins other than its own —
// "a foreign transaction changed something I bind", by a different mechanism
// than the one under test.
//
// Content-neutral ops are elided here exactly as Ylmish elides them (change
// propagation is content-driven: a register set to its current value, an edit
// to an unchanged title, a no-op splice — none emits a CRDT op). The elision
// is a pinned Ylmish contract, not implementation leakage: an oracle that
// wrote restamps the SUT deliberately never writes would enter LWW races the
// SUT never entered, and the differential would report semantics nobody has.
// -----------------------------------------------------------------------------

module RawYjs =
    let factory : BridgeFactory =
        fun doc ->
            let items : Y.Map<obj> = doc.getMap "items"
            let body : Y.Text = doc.getText "body"
            let root : Y.Map<obj> = doc.getMap ()
            let own : obj = box (obj ())
            let mutable signals = 0
            let mutable lastSignalled : Y.Transaction = null
            let observer (_ : _) (tr : Y.Transaction) =
                let foreign = not (Object.ReferenceEquals (tr.origin |> Option.toObj, own))
                if foreign && not (Object.ReferenceEquals (tr, lastSignalled)) then
                    lastSignalled <- tr
                    signals <- signals + 1
            items.observeDeep observer
            body.observeDeep observer
            root.observeDeep observer
            let applyBound (op : Op) =
                match op with
                | Add (id, text) ->
                    if not (items.has id) then items.set (id, box text) |> ignore
                | Remove id ->
                    if items.has id then items.delete id
                | Edit (id, text) ->
                    match items.get id with
                    | Some current when string current <> text -> items.set (id, box text) |> ignore
                    | Some _ | None -> ()
                | Move _ -> ()
                | SpliceBody (at, removed, inserted) ->
                    match clampSplice (body.toString ()) at removed inserted with
                    | None -> ()
                    | Some (at, removed, inserted) ->
                        if removed > 0 then body.delete (at, removed)
                        if inserted <> "" then body.insert (at, inserted)
                | SetNote value ->
                    let current = root.get "note" |> Option.map string |> Option.defaultValue ""
                    if current <> value then root.set ("note", box value) |> ignore
                | Noise _ -> ()
            { Name = "rawYjs"
              Doc = doc
              Signals = fun () -> signals
              Read = fun () ->
                { Items =
                    [ for id in items.keys () ->
                        { Id = id; Text = items.get id |> Option.map string |> Option.defaultValue "" } ]
                  Body = body.toString ()
                  Note = root.get "note" |> Option.map string |> Option.defaultValue "" }
              Apply = fun op _ ->
                match op with
                | Noise text -> writeNoise doc text
                | op -> Y.transact (doc, (fun _ -> applyBound op), own) }

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
      Converged : bool        // replicas agree (full model, order-normalized)
      Ids : Set<string>[]     // per-replica membership, for no-loss checks
      Signals : int[] }       // per-replica decode signals received

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
    // Yjs breaks concurrency ties by clientID (U3/U4). Pin them so a schedule
    // replays to one deterministic outcome, shared by the SUT and oracle runs
    // of `differential` — full models compare exactly.
    docs.[0].clientID <- 1.0
    docs.[1].clientID <- 2.0
    let bridges = docs |> Array.map factory
    let models = [| Model.empty; Model.empty |]

    // Deliver i -> j, then refresh replica j's model from the converged Y state —
    // this is `withYlmish`'s remote-update → read-back → `Set` loop. Modelling it
    // is what makes the harness fair: without it even sequential edits would
    // "lose", and the concurrent loss it reports would not be a CRDT-layer fact.
    let deliver i j =
        syncFromTo docs i j
        models.[j] <- bridges.[j].Read ()

    for { Replica = i; Op = op } in ops do
        models.[i] <- applyOp models.[i] op
        bridges.[i].Apply op models.[i]
        match delivery with
        | Immediate ->
            deliver i (1 - i)
            deliver (1 - i) i
        | Concurrent -> ()

    syncAll docs

    let final = bridges |> Array.map (fun b -> b.Read ())
    { Final = final
      Converged = Model.normalize final.[0] = Model.normalize final.[1]
      Ids = final |> Array.map idSet
      Signals = bridges |> Array.map (fun b -> b.Signals ()) }

// -----------------------------------------------------------------------------
// Differential check: SUT vs the raw-Yjs oracle on the same schedule
// -----------------------------------------------------------------------------

type Diff =
    { Converged : bool          // SUT replicas agree
      Sut : RunResult
      Oracle : RunResult
      SutFinal : Model          // normalized replica-0 read-back
      OracleFinal : Model       // ground truth, normalized
      MatchesOracle : bool      // full-model equality with the oracle
      OracleIds : Set<string>
      SutIds : Set<string>
      Lost : Set<string>        // ids the oracle kept but the SUT dropped
      SignalsMatch : bool }     // per-replica signal counts equal the oracle's

let differential (sut : BridgeFactory) (delivery : Delivery) (ops : ReplicaOp list) : Diff =
    let s = run sut delivery ops
    let o = run RawYjs.factory delivery ops
    let sutFinal = Model.normalize s.Final.[0]
    let oracleFinal = Model.normalize o.Final.[0]
    { Converged = s.Converged
      Sut = s
      Oracle = o
      SutFinal = sutFinal
      OracleFinal = oracleFinal
      MatchesOracle = sutFinal = oracleFinal
      OracleIds = o.Ids.[0]
      SutIds = s.Ids.[0]
      Lost = Set.difference o.Ids.[0] s.Ids.[0]
      SignalsMatch = s.Signals = o.Signals }

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

// -----------------------------------------------------------------------------
// Ground-truth tests — prove the oracle (and so the harness) is trustworthy.
// -----------------------------------------------------------------------------

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let private add r id = { Replica = r; Op = Add (id, "") }

let tests = testList "Harness" [
    testList "ground truth" [
        // The oracle IS the spec: if these failed nothing downstream could be
        // trusted. (The materialize-path calibration reds lived here until
        // Step 7 deleted that path; the binding-vs-oracle check lives in the
        // Binding tests.)
        test "oracle: two concurrent adds both survive (ground truth is no-loss)" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let r = run RawYjs.factory Concurrent ops
            Expect.isTrue r.Converged "oracle replicas must converge"
            Expect.equal r.Ids.[0] (Set.ofList [ "a"; "b" ])
                "element-wise Yjs keeps BOTH concurrent adds — the intended semantics"
        }

        test "oracle: concurrent body splices both survive, deterministically (pinned clientIDs)" {
            let ops = [
                { Replica = 0; Op = SpliceBody (0, 0, "ab") }
                { Replica = 1; Op = SpliceBody (0, 0, "xy") } ]
            let r = run RawYjs.factory Concurrent ops
            Expect.isTrue r.Converged "oracle replicas must converge"
            Expect.equal r.Final.[0].Body "abxy"
                "both inserts survive; lower clientID's first (U3's tiebreak, made deterministic by run)"
        }

        test "oracle: concurrent note sets — one deterministic LWW winner (U4)" {
            let ops = [
                { Replica = 0; Op = SetNote "from-0" }
                { Replica = 1; Op = SetNote "from-1" } ]
            let r = run RawYjs.factory Concurrent ops
            Expect.isTrue r.Converged "oracle replicas must converge"
            Expect.equal r.Final.[0].Note "from-1" "higher pinned clientID wins (U4)"
        }

        test "oracle: noise moves neither the model nor the signal count; bound changes signal once per transaction" {
            let ops = [
                { Replica = 0; Op = Noise "typing" }
                { Replica = 1; Op = Noise "more" }
                { Replica = 0; Op = SetNote "n" }
                { Replica = 0; Op = Noise "x" } ]
            let r = run RawYjs.factory Immediate ops
            Expect.equal r.Final.[1].Note "n" "the bound change arrived"
            Expect.equal r.Signals [| 0; 1 |]
                "replica 1 heard the note's delivery and nothing else; replica 0 heard nothing"
        }
    ]
]
