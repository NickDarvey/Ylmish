/// Differential test harness (Plan 0002, Step 2a).
///
/// Public surface (reused in Steps 5, 7, 9):
///   - DeliveryPolicy: Immediate | Concurrent
///   - Schedule<'msg>: a list of (replica-index, message) pairs
///   - Bridge<'model, 'msg>: whole-immutable-model-in → converged-model-out contract
///   - Harness.replay: schedule-replay driver
///   - Harness.differential: compare system-under-test vs raw-Yjs oracle
module Ylmish.Harness

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// ─── Delivery policies ───────────────────────────────────────────────────────

/// Controls when Yjs updates are exchanged between replicas.
type DeliveryPolicy =
    /// Sync after every edit — both replicas always agree.
    | Immediate
    /// All edits happen concurrently (no syncs until the end).
    | Concurrent

// ─── Schedule ────────────────────────────────────────────────────────────────

/// An edit schedule: list of (replica index (0 or 1), message to dispatch).
type Schedule<'msg> = (int * 'msg) list

// ─── Bridge ──────────────────────────────────────────────────────────────────

/// The contract for a system under test: given an initial model and a sequence
/// of model updates on two replicas, produce the final converged model from
/// each replica's perspective (after full sync).
type Bridge<'model> = {
    /// Run the bridge: init model → schedule of model updates → (replica0 result, replica1 result)
    Run : 'model -> DeliveryPolicy -> Schedule<'model -> 'model> -> 'model * 'model
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

/// Push all of src's state into dst (one direction).
let private sync (src : Y.Doc) (dst : Y.Doc) =
    Y.applyUpdate (dst, Y.encodeStateAsUpdate src)

/// Full exchange, both directions.
let private exchange (a : Y.Doc) (b : Y.Doc) =
    sync a b
    sync b a

// ─── Materialize bridge (system under test) ──────────────────────────────────

/// Bridge that uses the current materialize/dematerialize path:
/// encode the whole adaptive model, materialize into the doc,
/// sync, then dematerialize and decode.
let materializeBridge
    (create : 'model -> 'amodel)
    (update : 'amodel -> 'model -> unit)
    (encode : 'amodel -> Encoded<Element<string>>)
    (decode : Decoder<unit, Element<string>, 'model>)
    : Bridge<'model> =
    { Run = fun init policy schedule ->
        // Two replicas, each with their own doc and adaptive model
        let doc0 = Y.Doc.Create ()
        let doc1 = Y.Doc.Create ()
        doc0.clientID <- 1.0
        doc1.clientID <- 2.0

        let am0 = create init
        let am1 = create init

        // Initialize: materialize init into both docs
        let enc0 = encode am0
        Y.Doc.materialize doc0 enc0
        let enc1 = encode am1
        Y.Doc.materialize doc1 enc1

        // Initial sync so both start from the same state
        exchange doc0 doc1

        // Apply schedule
        let mutable model0 = init
        let mutable model1 = init

        for (replica, updateFn) in schedule do
            if replica = 0 then
                model0 <- updateFn model0
                transact (fun () -> update am0 model0)
                let enc = encode am0
                Y.Doc.materialize doc0 enc
            else
                model1 <- updateFn model1
                transact (fun () -> update am1 model1)
                let enc = encode am1
                Y.Doc.materialize doc1 enc

            match policy with
            | Immediate -> exchange doc0 doc1
            | Concurrent -> ()

        // Final sync for Concurrent policy
        match policy with
        | Concurrent -> exchange doc0 doc1
        | Immediate -> ()

        // Read back converged state from both docs
        let readBack doc =
            let element = Y.Doc.dematerialize doc
            let decoded = decode () ([], element) |> AVal.force
            match decoded with
            | Ok m -> m
            | Error errors -> failwith $"Harness: decode failed: %A{errors}"

        readBack doc0, readBack doc1
    }

// ─── Oracle bridge (raw Yjs, ground truth) ───────────────────────────────────

/// Oracle bridge that applies the same schedule directly to raw Yjs types,
/// proving what Yjs itself produces under the given delivery policy.
/// This uses a simple model shape: a single string field "propA" stored as
/// a plain Y.Map value (LWW register — the honest Yjs answer for plain values).
let oracleBridge : Bridge<string> =
    { Run = fun init policy schedule ->
        let doc0 = Y.Doc.Create ()
        let doc1 = Y.Doc.Create ()
        doc0.clientID <- 1.0
        doc1.clientID <- 2.0

        // Initialize
        let root0 : Y.Map<string> = doc0.getMap ()
        let root1 : Y.Map<string> = doc1.getMap ()
        root0.set ("propA", init) |> ignore
        root1.set ("propA", init) |> ignore
        exchange doc0 doc1

        // Apply schedule
        for (replica, updateFn) in schedule do
            let root = if replica = 0 then root0 else root1
            let current =
                match root.get "propA" with
                | Some v -> v
                | None -> init
            let next = updateFn current
            root.set ("propA", next) |> ignore

            match policy with
            | Immediate -> exchange doc0 doc1
            | Concurrent -> ()

        // Final sync
        match policy with
        | Concurrent -> exchange doc0 doc1
        | Immediate -> ()

        // Read back
        let read (root : Y.Map<string>) =
            match root.get "propA" with
            | Some v -> v
            | None -> init
        read root0, read root1
    }

// ─── Differential runner ─────────────────────────────────────────────────────

/// Compare the system-under-test bridge against the oracle for the same
/// schedule and delivery policy. Returns (sut result, oracle result) for
/// assertions.
let differential
    (sut : Bridge<'model>)
    (oracle : Bridge<'oracle>)
    (projectSut : 'model -> 'oracle)
    (init : 'model)
    (oracleInit : 'oracle)
    (policy : DeliveryPolicy)
    (schedule : Schedule<'model -> 'model>)
    (oracleSchedule : Schedule<'oracle -> 'oracle>)
    : ('oracle * 'oracle) * ('oracle * 'oracle) =
    let sutResult = sut.Run init policy schedule
    let oracleResult = oracle.Run oracleInit policy oracleSchedule
    let sutProjected = (projectSut (fst sutResult), projectSut (snd sutResult))
    sutProjected, oracleResult

// ─── Calibration tests ───────────────────────────────────────────────────────

module private Calibration =
    open Example

    module Codec =
        module Submodel =
            let encode (asubmodel : AdaptiveSubmodel) = Encode.object [
                "prop0", asubmodel.Prop0 |> Encode.value id
            ]

            let decode : Decoder<_, _, Submodel> = Decode.object {
                let! prop0 = Decode.object.required "prop0" Decode.value
                return { Prop0 = prop0 }
            }

        let encode (amodel : AdaptiveModel) = Encode.object [
            "propA", amodel.PropA |> Encode.value id
            "propB", amodel.PropB |> Encode.option
            "propC", Encode.list Submodel.encode amodel.PropC
            "propD", Encode.list (fun s -> Encode.value id (AVal.constant s)) amodel.PropD
            "propE", Submodel.encode amodel.PropE
        ]

        let decode : Decoder<_, _, Model> = Decode.object {
            let! propA = Decode.object.required "propA" Decode.value
            let! propB = Decode.object.optional "propB" Decode.value
            let! propC = Decode.object.required "propC" (Decode.list.required Submodel.decode)
            let! propD = Decode.object.required "propD" (Decode.list.required Decode.value)
            let! propE = Decode.object.required "propE" Submodel.decode
            return {
                PropA = propA
                PropB = propB
                PropC = propC
                PropD = propD
                PropE = propE
                PropF = None
            }
        }

    let init : Model = {
        PropA = "initial"
        PropB = None
        PropC = IndexList.empty
        PropD = IndexList.empty
        PropE = { Prop0 = "" }
        PropF = None
    }

    let sut = materializeBridge
                AdaptiveModel.Create
                (fun am m -> am.Update m)
                Codec.encode
                Codec.decode

let tests = testList "Harness" [

    test "oracle: concurrent plain-value edits converge (LWW)" {
        // Two replicas concurrently set propA to different values.
        // Under LWW, the higher clientID (2) wins.
        let schedule : Schedule<string -> string> = [
            0, fun _ -> "from-replica-0"
            1, fun _ -> "from-replica-1"
        ]

        let r0, r1 = oracleBridge.Run "initial" Concurrent schedule

        // Both replicas converge to the same value
        Expect.equal r0 r1 "replicas must converge"
        // Higher clientID (2 = replica 1) wins LWW
        Expect.equal r0 "from-replica-1" "higher clientID wins"
    }

    test "oracle: sequential edits produce expected result" {
        let schedule : Schedule<string -> string> = [
            0, fun _ -> "first"
            1, fun _ -> "second"
        ]

        let r0, r1 = oracleBridge.Run "initial" Immediate schedule

        // With Immediate delivery, each edit is synced before the next,
        // so the last write wins deterministically
        Expect.equal r0 r1 "replicas must converge"
        Expect.equal r0 "second" "last sequential write wins"
    }

    test "materialize path: sequential edits produce correct result" {
        // Sequential edits (Immediate policy): the materialize path should
        // handle this correctly because there's no concurrent divergence.
        let schedule : Schedule<Example.Model -> Example.Model> = [
            0, fun m -> { m with PropA = "first-edit" }
            1, fun m -> { m with PropA = "second-edit" }
        ]

        let (r0, r1) = Calibration.sut.Run Calibration.init Immediate schedule

        Expect.equal r0.PropA r1.PropA "replicas must converge"
        Expect.equal r0.PropA "second-edit" "last sequential write wins"
    }

    // This test is marked pending because the current materialize path has a
    // known bug (#83): concurrent edits to the same field resolve by
    // whole-state LWW (whichever replica materializes last wins), but the
    // non-winning replica's PropA is clobbered rather than converging.
    // The materialize path re-writes the entire doc on each update, so the
    // second replica's state overwrites the first's when they sync.
    //
    // Expected failure: after concurrent edits, one replica's edit is lost.
    // This names the data loss: PropA from replica 0 is discarded.
    //
    // This will be fixed in Steps 5-7 when the materialize path is replaced
    // with a live binding that exchanges deltas.
    ptestCase "materialize path: concurrent edits — KNOWN BUG #83, data loss on PropA" <| fun _ ->
        // Two replicas concurrently edit PropA
        let schedule : Schedule<Example.Model -> Example.Model> = [
            0, fun m -> { m with PropA = "from-replica-0" }
            1, fun m -> { m with PropA = "from-replica-1" }
        ]

        let oracleSchedule : Schedule<string -> string> = [
            0, fun _ -> "from-replica-0"
            1, fun _ -> "from-replica-1"
        ]

        let (sutResult, oracleResult) =
            differential
                Calibration.sut
                oracleBridge
                (fun m -> m.PropA)
                Calibration.init
                "initial"
                Concurrent
                schedule
                oracleSchedule

        // The oracle converges correctly (higher clientID wins)
        Expect.equal (fst oracleResult) (snd oracleResult) "oracle replicas converge"

        // The SUT should also converge — but with materialize, it does NOT.
        // This is the #83 bug: both replicas end up with their own local write,
        // not the LWW-winner, because materialize re-stamps the entire state.
        // When this test is un-pended (Steps 5-7), it should pass.
        Expect.equal (fst sutResult) (snd sutResult)
            "SUT replicas must converge (lost: PropA from the non-winning replica is clobbered)"
        Expect.equal (fst sutResult) (fst oracleResult)
            "SUT must match oracle (lost: the materialize path doesn't respect Yjs LWW resolution)"
]
