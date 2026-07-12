module Ylmish.NorthStar

// Plan 0002, Step 2b — the north-star acceptance tests, written against the
// TARGET public API and skipped (testCase) until Step 7 un-skips them. Their
// job today is to force the API surface to exist and to read well as consumer
// code; their job at Step 7 is to close issue #83 by test.
//
// The model here is hand-written adaptive (cval/cmap), NOT Adaptify-generated:
// Step 3 owns proving `[<ModelType>]` works with a Text field, and 2b must not
// take on codegen risk. The shape mirrors the plan's consumer sketch.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// --- The consumer's model: plain immutable record; Text is the only Ylmish type.

type Model = {
    Body : Text                      // collaborative: concurrent edits merge
    Note : Text option               // optional collaborative text: None = key absent
    Todos : HashMap<string, string>  // keyed by app-minted id ⇒ element-wise merge
    Filter : string                  // app-only: never encoded, never synced
}

module Model =
    let init = { Body = Text.empty; Note = None; Todos = HashMap.empty; Filter = "" }

type Msg =
    | EditBody of (Text -> Text)
    | AddTodo of id : string * title : string
    | SetFilter of string

let update (msg : Msg) (m : Model) =
    match msg with
    | EditBody f -> { m with Body = f m.Body }, Elmish.Cmd.none
    | AddTodo (id, title) -> { m with Todos = HashMap.add id title m.Todos }, Elmish.Cmd.none
    | SetFilter f -> { m with Filter = f }, Elmish.Cmd.none

// --- Hand-written adaptive companion (what Adaptify generates from Step 3 on).

type AdaptiveModel (m : Model) =
    let body = cval m.Body
    let note = cval m.Note
    let todos = cmap (HashMap.toSeq m.Todos)
    member _.Body = body :> aval<Text>
    member _.Note = note :> aval<Text option>
    member _.Todos = todos :> amap<string, string>
    member _.Update (m : Model) =
        body.Value <- m.Body
        note.Value <- m.Note
        todos.Value <- m.Todos
    static member Create (m : Model) = AdaptiveModel m

// --- The codec: one word per field is the merge choice; Filter is absent.
// Quoted verbatim by doc/guides/codec.md (the Encode.option shape).

module Codec =
    let encode (am : AdaptiveModel) : Encoded =
        Encode.object [
            "body", Encode.text am.Body
            "note", Encode.option Encode.text am.Note
            "todos", Encode.map (fun (title : string) -> Encode.string (AVal.constant title)) am.Todos
        ]

    let decode : Decoder<Model, Model> =
        Decode.object {
            let! model = Decode.ask
            let! body = Decode.object.optional "body" Decode.text
            let! note = Decode.object.optional "note" Decode.text
            let! todos = Decode.object.optional "todos" (Decode.map Decode.string)
            return
                { model with
                    Body = defaultArg body Text.empty
                    Note = note
                    Todos = defaultArg todos HashMap.empty }
        }

// --- Wiring: two independent programs over two docs, synced explicitly.

let private makeProgram (doc : Y.Doc) =
    Elmish.Program.mkProgram (fun () -> Model.init, Elmish.Cmd.none) update (fun _ _ -> ())
    |> Ylmish.Program.withYlmish {
        Doc = doc
        Create = AdaptiveModel.Create
        Update = fun am m -> am.Update m
        Encode = Codec.encode
        Decode = Codec.decode
        OnError = Ylmish.Program.OnError.log
    }

let private user msg = Ylmish.Program.Message.User msg

let private syncBoth (a : Y.Doc) (b : Y.Doc) =
    Y.applyUpdate (b, Y.encodeStateAsUpdate a)
    Y.applyUpdate (a, Y.encodeStateAsUpdate b)

let tests = testList "NorthStar" [

    // Issue #83's acceptance: concurrent edits to the same text field converge
    // to an interleaved result IN THE ELMISH MODELS, not just the docs.
    // Quoted verbatim by doc/guides/text.md.
    testCase "concurrent Text edits converge interleaved across two withYlmish programs" (fun () ->
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (makeProgram d1)
        use p2 = Elmish.Program.test (makeProgram d2)

        // Shared starting text, created by one peer and synced.
        p1.Dispatch (user (EditBody (Text.edit "hello")))
        syncBoth d1 d2

        // Offline: both edit the same field concurrently...
        p1.Dispatch (user (EditBody (Text.insert 5 " world")))
        p2.Dispatch (user (EditBody (Text.insert 0 "oh, ")))
        // ...then the network heals.
        syncBoth d1 d2

        Expect.equal (Text.toString p1.Model.Body) "oh, hello world"
            "both peers' edits survive, interleaved — the issue #83 headline"
        Expect.equal (Text.toString p2.Model.Body) (Text.toString p1.Model.Body)
            "both models converge")

    // The accepted-limitation rule, demonstrated positively: offline creation
    // is safe because todos are keyed by app-minted unique ids (Encode.map).
    testCase "keyed-map concurrent adds both survive (offline creation with app-minted ids)" (fun () ->
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (makeProgram d1)
        use p2 = Elmish.Program.test (makeProgram d2)

        p1.Dispatch (user (AddTodo ("id-1", "buy milk")))
        p2.Dispatch (user (AddTodo ("id-2", "walk dog")))
        syncBoth d1 d2

        Expect.equal (HashMap.count p1.Model.Todos) 2 "both offline creations survive on p1"
        Expect.equal p1.Model.Todos p2.Model.Todos "and the models converge")

    // Leave what you don't own (U15): a full session against a doc carrying a
    // key this codec doesn't know must not destroy that key.
    testCase "unknown keys survive a full withYlmish session" (fun () ->
        let d1 = Y.Doc.Create ()
        (d1.getMap () : Y.Map<obj>).set ("someone-elses-key", box "v1-data") |> ignore

        use p1 = Elmish.Program.test (makeProgram d1)
        p1.Dispatch (user (EditBody (Text.edit "hello")))
        p1.Dispatch (user (AddTodo ("id-1", "buy milk")))
        p1.Dispatch (user (SetFilter "done"))   // app-only: must not sync either

        let root : Y.Map<obj> = d1.getMap ()
        Expect.equal (root.get "someone-elses-key" |> Option.map string) (Some "v1-data")
            "keys the encoder doesn't mention are never touched"
        Expect.isFalse (root.has "filter") "app-only fields never reach the doc")
]
