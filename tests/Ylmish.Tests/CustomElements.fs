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
// =============================================================================

// sample:begin editor-surface
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
// sample:end editor-surface

// =============================================================================
// CONSUMER CODE — a rich-text surface: hands the live, integrated Y.XmlFragment
// to a structured editor (a y-prosemirror binding). The editor mutates the
// fragment directly; the model receives the merged structure through decode.
// The observable Value here is the child count — a stand-in for "the document".
// =============================================================================

type XmlEditorSurface () =
    let mutable fragment : Y.XmlFragment option = None
    /// The live Y.XmlFragment — what you would hand to y-prosemirror.
    member _.Fragment = Option.get fragment
    interface CustomElement with
        member _.Connect ctx =
            fragment <- Some (ctx.GetXmlFragment ())
            { new IDisposable with member _.Dispose () = () }
        member _.Value =
            match fragment with
            | Some f -> box ((f.toArray ()).Count)
            | None -> box 0

// =============================================================================
// Wiring (test-side): a model with a counter, an editor draft, and a register.
// =============================================================================

open FSharp.Data.Adaptive   // test-side only: the hand-written adaptive model

type Model = { Hits : int; Draft : string; Note : string; Body : int }

module Model =
    let init = { Hits = 0; Draft = ""; Note = ""; Body = 0 }

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
    Body : XmlEditorSurface
    Dispatcher : Elmish.Program.ElmishDispatcher<Model, Ylmish.Program.Message<Model, Msg>>
}

let private mkPeer () =
    let doc = Y.Doc.Create ()
    let counter = GrowOnlyCounter ()
    let editor = EditorSurface ()
    let body = XmlEditorSurface ()
    let update msg (m : Model) =
        match msg with
        | Bump -> { m with Hits = m.Hits + 1 }, Elmish.Cmd.ofEffect (fun _ -> counter.Bump ())
        | SetNote v -> { m with Note = v }, Elmish.Cmd.none
    let encode (am : AdaptiveModel) : Encoded =
        Encode.object [
            "hits", Encode.custom counter
            "draft", Encode.custom editor
            "body", Encode.custom body
            "note", Encode.string am.Note
        ]
    let decode : Decoder<Model, Model> =
        Decode.object {
            let! model = Decode.ask
            let! hits = Decode.object.required "hits" Decode.custom
            let! draft = Decode.object.required "draft" Decode.custom
            let! bodyCount = Decode.object.required "body" Decode.custom
            let! note = Decode.object.optional "note" Decode.string
            return { model with Hits = hits; Draft = draft; Body = bodyCount; Note = defaultArg note model.Note }
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
      Body = body
      Dispatcher = Elmish.Program.test program }

let private user msg = Ylmish.Program.Message.User msg

let private syncBoth (a : Y.Doc) (b : Y.Doc) =
    Y.applyUpdate (b, Y.encodeStateAsUpdate a, box "test-remote")
    Y.applyUpdate (a, Y.encodeStateAsUpdate b, box "test-remote")

/// Append one element child to a fragment — a y-prosemirror stand-in.
let private pushChild (f : Y.XmlFragment) (name : string) =
    f.push [| Fable.Core.U2.Case1 (Y.XmlElement.Create name) |]

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

    test "the xml editor scenario: external edits to the handed-out Y.XmlFragment flow into the model and merge" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        use _d1 = p1.Dispatcher
        use _d2 = p2.Dispatcher

        // The "editor" (a y-prosemirror stand-in) writes DIRECTLY to the live
        // Y.XmlFragment, knowing nothing of Ylmish.
        pushChild p1.Body.Fragment "paragraph"
        Expect.equal p1.Dispatcher.Model.Body 1
            "the editor's own insert flowed into the model through the ordinary decode path"

        syncBoth p1.Doc p2.Doc
        Expect.equal p2.Dispatcher.Model.Body 1 "and reached the peer's model"

        // Concurrent inserts from both editors both survive (fragment inserts
        // merge like array inserts, U8) — a structural merge no register gives.
        pushChild p1.Body.Fragment "heading"
        pushChild p2.Body.Fragment "list"
        syncBoth p1.Doc p2.Doc
        Expect.equal p1.Dispatcher.Model.Body 3 "both concurrent inserts kept, not last-writer-wins"
        Expect.equal p2.Dispatcher.Model.Body p1.Dispatcher.Model.Body "and both models converge"
    }

    test "a nested custom anchors its Y.XmlFragment in the parent map (ensureXmlFragment)" {
        let doc = Y.Doc.Create ()
        let mutable frag : Y.XmlFragment option = None
        let bodyEl =
            { new CustomElement with
                member _.Connect ctx =
                    frag <- Some (ctx.GetXmlFragment ())
                    { new IDisposable with member _.Dispose () = () }
                member _.Value = box 0 }
        // "body" is a custom nested inside the "draft" object — this routes the
        // BindContext through attachCustom (the nested Get* site).
        use _att =
            Ylmish.Internal.Binding.attach doc
                (Encode.object [ "draft", Encode.object [ "body", Encode.custom bodyEl ] ])
        pushChild (Option.get frag) "paragraph"
        // The very fragment the editor mutated lives at draft.body in the doc.
        let draft : Y.Map<obj> = doc.getMap "draft"
        match draft.get "body" with
        | Some v -> Expect.equal ((unbox<Y.XmlFragment> v).toArray().Count) 1
                        "the edited fragment is anchored under draft.body"
        | None -> failwith "expected a Y.XmlFragment under draft.body"
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
