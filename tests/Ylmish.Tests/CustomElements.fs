module Ylmish.Tests.CustomElements

// Plan 0002, Step 8 — the escape hatch, proven end-to-end under withYlmish.
// The deliverable is CONSUMER code: `GrowOnlyCounter` and `EditorSurface`
// below are written exactly as a library user would write them. Their measure
// is their `open` list — Yjs and Ylmish.Codec only: no FSharp.Data.Adaptive,
// no Ylmish.Internal (the encapsulation acceptance criterion).

open System

open Yjs

open Ylmish
open Ylmish.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// =============================================================================
// CONSUMER CODE — a grow-only counter over a Y.Array of ticks. Concurrent
// increments from different peers both survive (array inserts merge, U8), so
// the merged value is the SUM — a merge no built-in encoding provides.
// =============================================================================

type GrowOnlyCounter () =
    let mutable ticks : Y.Array<obj> option = None
    let mutable origin : obj = null

    /// Push one tick. Call from a Cmd effect after an optimistic increment;
    /// the authoritative count comes back through Decode.custom.
    member _.Bump () =
        match ticks with
        | Some arr ->
            match arr.doc with
            | Some doc -> Y.transact (doc, (fun _ -> arr.push [| box 1 |]), origin)
            | None -> ()
        | None -> ()

    interface CustomElement with
        member _.Connect ctx =
            ticks <- Some (ctx.GetArray ())
            origin <- ctx.Origin
            { new IDisposable with member _.Dispose () = () }
        member _.Value =
            match ticks with
            | Some arr -> box ((arr.toArray ()).Count)
            | None -> box 0

// =============================================================================
// CONSUMER CODE — an editor surface: hands the live, integrated Y.Text to
// something that wants the real thing (a CodeMirror/Monaco binding). The
// editor writes to it directly; the model receives the merged content through
// the ordinary decode path.
//
// Quoted verbatim by doc/guides/custom-elements.md.
// =============================================================================

type EditorSurface () =
    let mutable text : Y.Text option = None
    /// The live Y.Text — what you would hand to the editor component.
    member _.Text = Option.get text
    interface CustomElement with
        member _.Connect ctx =
            text <- Some (ctx.GetText ())
            { new IDisposable with member _.Dispose () = () }
        member _.Value =
            match text with
            | Some t -> box (t.toString ())
            | None -> box ""

// =============================================================================
// Wiring (test-side): a model with a counter, an editor draft, and a register.
// =============================================================================

open FSharp.Data.Adaptive   // test-side only: the hand-written adaptive model

type Model = { Hits : int; Draft : string; Note : string }

module Model =
    let init = { Hits = 0; Draft = ""; Note = "" }

type Msg =
    | Bump
    | SetNote of string

type AdaptiveModel (m : Model) =
    let note = cval m.Note
    member _.Note = note :> aval<string>
    member _.Update (m : Model) = note.Value <- m.Note

type private Peer = {
    Doc : Y.Doc
    Counter : GrowOnlyCounter
    Editor : EditorSurface
    Dispatcher : Elmish.Program.ElmishDispatcher<Model, Ylmish.Program.Message<Model, Msg>>
}

let private mkPeer () =
    let doc = Y.Doc.Create ()
    let counter = GrowOnlyCounter ()
    let editor = EditorSurface ()
    let update msg (m : Model) =
        match msg with
        | Bump -> { m with Hits = m.Hits + 1 }, Elmish.Cmd.ofEffect (fun _ -> counter.Bump ())
        | SetNote v -> { m with Note = v }, Elmish.Cmd.none
    let encode (am : AdaptiveModel) : Encoded =
        Encode.object [
            "hits", Encode.custom counter
            "draft", Encode.custom editor
            "note", Encode.string am.Note
        ]
    let decode : Decoder<Model, Model> =
        Decode.object {
            let! model = Decode.ask
            let! hits = Decode.object.required "hits" Decode.custom
            let! draft = Decode.object.required "draft" Decode.custom
            let! note = Decode.object.optional "note" Decode.string
            return { model with Hits = hits; Draft = draft; Note = defaultArg note model.Note }
        }
    let program =
        Elmish.Program.mkProgram (fun () -> Model.init, Elmish.Cmd.none) update (fun _ _ -> ())
        |> Ylmish.Program.withYlmish {
            Doc = doc
            Create = AdaptiveModel
            Update = fun (am : AdaptiveModel) m -> am.Update m
            Encode = encode
            Decode = decode
            OnError = Ylmish.Program.OnError.log
        }
    { Doc = doc
      Counter = counter
      Editor = editor
      Dispatcher = Elmish.Program.test program }

let private user msg = Ylmish.Program.Message.User msg

let private syncBoth (a : Y.Doc) (b : Y.Doc) =
    Y.applyUpdate (b, Y.encodeStateAsUpdate a, box "test-remote")
    Y.applyUpdate (a, Y.encodeStateAsUpdate b, box "test-remote")

let tests = testList "CustomElement (the escape hatch, end-to-end)" [

    test "a consumer counter SUMS concurrent increments across two withYlmish peers" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        use _d1 = p1.Dispatcher
        use _d2 = p2.Dispatcher

        // Offline: p1 bumps twice, p2 bumps once — concurrently.
        p1.Dispatcher.Dispatch (user Bump)
        p1.Dispatcher.Dispatch (user Bump)
        p2.Dispatcher.Dispatch (user Bump)
        Expect.equal p1.Dispatcher.Model.Hits 2 "optimistic local count on p1"
        Expect.equal p2.Dispatcher.Model.Hits 1 "optimistic local count on p2"

        syncBoth p1.Doc p2.Doc

        Expect.equal p1.Dispatcher.Model.Hits 3 "the SUM, not last-writer-wins — a merge no built-in gives"
        Expect.equal p2.Dispatcher.Model.Hits 3 "and both Elmish models converge on it"
    }

    test "the editor scenario: external edits to the handed-out Y.Text flow into the model" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        use _d1 = p1.Dispatcher
        use _d2 = p2.Dispatcher

        // The "editor" (CodeMirror stand-in) writes DIRECTLY to the live
        // Y.Text — in its own transactions, knowing nothing of Ylmish.
        p1.Editor.Text.insert (0, "hello")
        Expect.equal p1.Dispatcher.Model.Draft "hello"
            "the editor's own write flowed into the model through the ordinary decode path"

        syncBoth p1.Doc p2.Doc
        Expect.equal p2.Dispatcher.Model.Draft "hello" "and reached the peer's model"

        // Concurrent edits from both editors interleave like any Y.Text.
        p1.Editor.Text.insert (5, " world")
        p2.Editor.Text.insert (0, "oh, ")
        syncBoth p1.Doc p2.Doc
        Expect.equal p1.Dispatcher.Model.Draft "oh, hello world" "interleaved (U3)"
        Expect.equal p2.Dispatcher.Model.Draft p1.Dispatcher.Model.Draft "converged"
    }

    test "BindContext never double-integrates: repeated Get* return the one instance (U5)" {
        let doc = Y.Doc.Create ()
        let mutable seen : Y.Array<obj> list = []
        let probing =
            { new CustomElement with
                member _.Connect ctx =
                    seen <- [ ctx.GetArray (); ctx.GetArray () ]
                    { new IDisposable with member _.Dispose () = () }
                member _.Value = box 0 }
        use _att = Ylmish.Internal.Binding.attach doc (Encode.object [ "probe", Encode.custom probing ])
        match seen with
        | [ a; b ] -> Expect.isTrue (Object.ReferenceEquals (a, b)) "one integrated instance, ever (U5)"
        | _ -> failwith "expected two GetArray calls"
    }
]
