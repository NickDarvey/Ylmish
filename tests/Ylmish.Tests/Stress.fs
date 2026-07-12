module Ylmish.Tests.Stress

// Plan 0002, Step 9 — property-based stress. Random two-replica schedules over
// the keyed-map + text + register trio, replayed deterministically through the
// Step 2a harness under both delivery policies, with a FULL `withYlmish`
// program on each replica (Elmish loop, suppression window, decode → Set —
// the whole stack, not just the binding).
//
// Invariants, per schedule (L2):
//   1. the replicas converge to equal Elmish models;
//   2. the converged state matches the raw-Yjs oracle — full-model equality,
//      exact because `Harness.run` pins clientIDs so SUT and oracle share
//      every concurrency tiebreak;
//   3. nothing the oracle preserves was lost;
//   4. (Immediate only) with no concurrency window the converged state equals
//      the plain sequential fold of the schedule — `applyOp` is the spec.
//
// Determinism: the Hedgehog shim's PRNG is fixed-seed, and clientIDs are
// pinned, so a failure replays identically; the failure message carries the
// whole schedule.

open FSharp.Data.Adaptive
open Hedgehog
open Yjs

open Ylmish
open Ylmish.Codec
open Ylmish.Harness

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// -----------------------------------------------------------------------------
// The consumer under stress: an ordinary withYlmish program whose model is the
// plan's trio — collaborative body text, LWW note register, keyed todos.
// -----------------------------------------------------------------------------

type SutModel = { Body : Text; Note : string; Todos : HashMap<string, string> }

module SutModel =
    let init = { Body = Text.empty; Note = ""; Todos = HashMap.empty }

type Msg = Apply of Op

/// The consumer `update`: each harness op phrased as the intent a real app
/// would dispatch. Semantics mirror `Harness.applyOp` (adds idempotent on id,
/// edits touch existing items only, splices via Text's clamped primitives).
let private update (Apply op) (m : SutModel) =
    let m =
        match op with
        | Add (id, title) ->
            if HashMap.containsKey id m.Todos then m
            else { m with Todos = HashMap.add id title m.Todos }
        | Remove id -> { m with Todos = HashMap.remove id m.Todos }
        | Edit (id, title) ->
            if HashMap.containsKey id m.Todos
            then { m with Todos = HashMap.add id title m.Todos }
            else m
        | Move _ -> m
        | SpliceBody (at, removed, inserted) ->
            { m with Body = Text.replace at removed inserted m.Body }
        | SetNote value -> { m with Note = value }
    m, Elmish.Cmd.none

type private AdaptiveModel (m : SutModel) =
    let body = cval m.Body
    let note = cval m.Note
    let todos = cmap (HashMap.toSeq m.Todos)
    member _.Body = body :> aval<Text>
    member _.Note = note :> aval<string>
    member _.Todos = todos :> amap<string, string>
    member _.Update (m : SutModel) =
        body.Value <- m.Body
        note.Value <- m.Note
        todos.Value <- m.Todos

let private encode (am : AdaptiveModel) : Encoded =
    Encode.object [
        "body", Encode.text am.Body
        "note", Encode.string am.Note
        "todos", Encode.map (fun (title : string) -> Encode.string (AVal.constant title)) am.Todos
    ]

let private decode : Decoder<SutModel, SutModel> =
    Decode.object {
        let! model = Decode.ask
        let! body = Decode.object.optional "body" Decode.text
        let! note = Decode.object.optional "note" Decode.string
        let! todos = Decode.object.optional "todos" (Decode.map Decode.string)
        return
            { model with
                Body = defaultArg body Text.empty
                Note = defaultArg note model.Note
                Todos = defaultArg todos HashMap.empty }
    }

/// A harness bridge backed by a full withYlmish program: Apply dispatches the
/// op as a user message; Read is the live Elmish model.
let private sutFactory : BridgeFactory =
    fun doc ->
        let program =
            Elmish.Program.mkProgram (fun () -> SutModel.init, Elmish.Cmd.none) update (fun _ _ -> ())
            |> Ylmish.Program.withYlmish {
                Doc = doc
                Create = AdaptiveModel
                Update = fun (am : AdaptiveModel) m -> am.Update m
                Encode = encode
                Decode = decode
                OnError = Ylmish.Program.OnError.log
            }
        let dispatcher = Elmish.Program.test program
        { Name = "withYlmish"
          Doc = doc
          Apply = fun op _ -> dispatcher.Dispatch (Ylmish.Program.Message.User (Apply op))
          Read = fun () ->
            let m = dispatcher.Model
            { Items = [ for id, title in HashMap.toList m.Todos -> { Id = id; Text = title } ]
              Body = Text.toString m.Body
              Note = m.Note } }

// -----------------------------------------------------------------------------
// Schedule generation. Ids come from a small pool so removes/edits actually
// collide with adds (and with each other, across replicas); splice positions
// are raw and rely on the shared clamp contract.
// -----------------------------------------------------------------------------

let private genId = gen {
    let! i = Gen.int32 (Range.linear 0 4)
    return $"t{i}"
}

let private genSmallString = Gen.string (Range.linear 0 5) Gen.alphaNum

let private genOp = gen {
    let! kind = Gen.int32 (Range.linear 0 9)
    if kind <= 2 then
        let! id = genId
        let! title = genSmallString
        return Add (id, title)
    elif kind = 3 then
        let! id = genId
        return Remove id
    elif kind <= 5 then
        let! id = genId
        let! title = genSmallString
        return Edit (id, title)
    elif kind <= 8 then
        let! at = Gen.int32 (Range.linear 0 24)
        let! removed = Gen.int32 (Range.linear 0 6)
        let! inserted = genSmallString
        return SpliceBody (at, removed, inserted)
    else
        let! value = genSmallString
        return SetNote value
}

let private genSchedule =
    Gen.list (Range.linear 1 12) (gen {
        let! replica = Gen.int32 (Range.linear 0 1)
        let! op = genOp
        return { Replica = replica; Op = op }
    })

// -----------------------------------------------------------------------------
// The invariants, checked per schedule.
// -----------------------------------------------------------------------------

let private checkSchedule (delivery : Delivery) (ops : ReplicaOp list) =
    let d = differential sutFactory delivery ops
    if not d.Converged then
        failwith
            $"replicas DIVERGED under %A{delivery}.\nschedule: %A{ops}\nreplica 0: %A{Model.normalize d.Sut.Final.[0]}\nreplica 1: %A{Model.normalize d.Sut.Final.[1]}"
    if not d.MatchesOracle then
        failwith
            $"SUT does not match the raw-Yjs oracle under %A{delivery}.\nschedule: %A{ops}\nsut:    %A{d.SutFinal}\noracle: %A{d.OracleFinal}\nlost ids: %A{d.Lost}"
    if not (Set.isEmpty d.Lost) then
        failwith
            $"ids LOST under %A{delivery}: %A{d.Lost}\nschedule: %A{ops}"
    d

let tests = testList "Stress (plan 0002 Step 9)" [

    test "100 random schedules, Concurrent delivery: converge, match the oracle, lose nothing" {
        Property.check <| property {
            let! ops = genSchedule
            let _ = checkSchedule Concurrent ops
            return true
        }
    }

    test "100 random schedules, Immediate delivery: ditto, and equal the sequential fold" {
        Property.check <| property {
            let! ops = genSchedule
            let d = checkSchedule Immediate ops
            // No concurrency window ⇒ no CRDT tiebreaks ⇒ the converged state
            // must equal the plain sequential fold of the schedule. This
            // catches semantic drift between the consumer update and the
            // harness spec that SUT-vs-oracle (both op-driven) cannot.
            let expected =
                ops |> List.fold (fun m ro -> applyOp m ro.Op) Model.empty |> Model.normalize
            if d.SutFinal <> expected then
                failwith
                    $"Immediate delivery diverged from the sequential fold.\nschedule: %A{ops}\nsut:      %A{d.SutFinal}\nexpected: %A{expected}"
            return true
        }
    }
]
