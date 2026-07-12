module Ylmish.Tests.BindingDecode

// Plan 0002, Step 6 — the decode direction: one afterTransaction hook →
// schema-directed read → decode. Pins: own-origin echoes filtered (U6), one
// remote transaction = one notification regardless of how many types it spans
// (U14), remote text decodes intent-free, customs ride the same read (L6),
// decode failures are path-tracked values and the subscription survives them,
// and the two crash regressions imported from the reference branch (L4).

open System

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Codec
open Ylmish.Internal

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let private sync (src : Y.Doc) (dst : Y.Doc) =
    Y.applyUpdate (dst, Y.encodeStateAsUpdate src, box "test-remote")

// --- A consumer-shaped model exercising every codec kind -----------------------

type Model = {
    Name : string
    Body : Text
    Todos : HashMap<string, string * bool>
    Tags : IndexList<string>
    Note : Text option
}

module Model =
    let init = {
        Name = ""
        Body = Text.empty
        Todos = HashMap.empty
        Tags = IndexList.empty
        Note = None
    }

type AdaptiveModel (m : Model) =
    let name = cval m.Name
    let body = cval m.Body
    let todos = cmap (HashMap.toSeq m.Todos)
    let tags = clist m.Tags
    let note = cval m.Note
    member _.Name = name
    member _.Body = body
    member _.Todos = todos
    member _.Tags = tags
    member _.Note = note
    member _.Update (m : Model) =
        name.Value <- m.Name
        body.Value <- m.Body
        todos.Value <- m.Todos
        tags.Value <- m.Tags
        note.Value <- m.Note

let private encode (am : AdaptiveModel) : Encoded =
    Encode.object [
        "name", Encode.string am.Name
        "body", Encode.text am.Body
        "todos", Encode.map (fun ((title, don) : string * bool) ->
            Encode.object [
                "title", Encode.string (AVal.constant title)
                "done", Encode.bool (AVal.constant don)
            ]) am.Todos
        "tags", Encode.list Value.Encode.string am.Tags
        "note", Encode.option Encode.text am.Note
    ]

let private decode : Decoder<Model, Model> =
    Decode.object {
        let! model = Decode.ask
        let! name = Decode.object.optional "name" Decode.string
        let! body = Decode.object.optional "body" Decode.text
        let! todos =
            Decode.object.optional "todos" (Decode.map (Decode.object {
                let! title = Decode.object.required "title" Decode.string
                let! don = Decode.object.required "done" Decode.bool
                return (title, don)
            }))
        let! tags = Decode.object.optional "tags" (Decode.list Value.Decode.string)
        let! note = Decode.object.optional "note" Decode.text
        return
            { model with
                Name = defaultArg name model.Name
                Body = defaultArg body model.Body
                Todos = defaultArg todos HashMap.empty
                Tags = defaultArg tags IndexList.empty
                Note = note }
    }

/// A peer: doc + adaptive model + attachment.
type private Peer = {
    Doc : Y.Doc
    Am : AdaptiveModel
    Att : Binding.Attachment
}

let private mkPeer () =
    let doc = Y.Doc.Create ()
    let am = AdaptiveModel Model.init
    let att = Binding.attach doc (encode am)
    { Doc = doc; Am = am; Att = att }

let private readModel (p : Peer) (current : Model) : Result<Model, Error list> =
    Decode.runElement current decode (Binding.read p.Doc (encode p.Am))

let tests = testList "Binding (decode direction)" [

    test "full bidirectional round trip at the adaptive layer (Step 6 acceptance)" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()

        transact (fun () ->
            p1.Am.Name.Value <- "groceries"
            p1.Am.Body.Value <- Text.edit "hello world" p1.Am.Body.Value
            p1.Am.Todos.[ "id-1" ] <- ("buy milk", false)
            p1.Am.Todos.[ "id-2" ] <- ("walk dog", true)
            p1.Am.Tags.Append "urgent" |> ignore
            p1.Am.Note.Value <- Some (Text.ofString "remember"))
        sync p1.Doc p2.Doc

        match readModel p2 Model.init with
        | Error e -> failwithf "decode failed: %A" e
        | Ok m ->
            Expect.equal m.Name "groceries" "register round-trips"
            Expect.equal (Text.toString m.Body) "hello world" "text round-trips"
            Expect.isEmpty (Text.pending m.Body) "decoded text is intent-free"
            Expect.equal m.Todos (HashMap.ofList [ "id-1", ("buy milk", false); "id-2", ("walk dog", true) ])
                "keyed items round-trip"
            Expect.equal (IndexList.toList m.Tags) [ "urgent" ] "list round-trips"
            Expect.equal (m.Note |> Option.map Text.toString) (Some "remember") "option round-trips"
    }

    test "one remote transaction spanning many types = exactly one notification (U14)" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        let mutable notifications = 0
        use _s = Binding.subscribe p2.Doc p2.Att.Origin (fun () -> notifications <- notifications + 1)

        // Several local transactions on peer 1, shipped as ONE update.
        transact (fun () -> p1.Am.Name.Value <- "a")
        transact (fun () -> p1.Am.Body.Value <- Text.edit "text" p1.Am.Body.Value)
        transact (fun () -> p1.Am.Todos.[ "id-1" ] <- ("t", false))
        sync p1.Doc p2.Doc

        Expect.equal notifications 1
            "one applyUpdate = one transaction = one notification, however many types it touched"
    }

    test "own-origin transactions are filtered: local edits never echo (U6)" {
        let p = mkPeer ()
        let mutable notifications = 0
        use _s = Binding.subscribe p.Doc p.Att.Origin (fun () -> notifications <- notifications + 1)

        transact (fun () -> p.Am.Name.Value <- "local")
        transact (fun () -> p.Am.Todos.[ "id-1" ] <- ("t", false))
        Expect.equal notifications 0 "the attachment's own writes are invisible to its decode direction"

        Y.transact (p.Doc, (fun _ -> (p.Doc.getMap () : Y.Map<obj>).set ("foreign", box 1) |> ignore), box "someone-else")
        Expect.equal notifications 1 "a foreign write on the same doc does notify"
    }

    test "custom elements ride the same read — merged value, no side channel (L6)" {
        let mkCustomPeer () =
            let doc = Y.Doc.Create ()
            let mutable text : Y.Text option = None
            let custom =
                { new CustomElement with
                    member _.Connect ctx =
                        text <- Some (ctx.GetText ())
                        { new IDisposable with member _.Dispose () = () }
                    member _.Value =
                        match text with
                        | Some t -> box (t.toString ())
                        | None -> box "" }
            let encoded = Encode.object [ "shout", Encode.custom custom ]
            let att = Binding.attach doc encoded
            doc, encoded, att, (fun () -> Option.get text)
        let d1, _, a1, text1 = mkCustomPeer ()
        let d2, enc2, _a2, _ = mkCustomPeer ()

        // Peer 1's custom writes through ITS OWN captured instance — which is
        // the named root (race-free even though both peers' Connect created it
        // eagerly), origin-tagged.
        Y.transact (d1, (fun _ -> (text1 ()).insert (0, "hi")), a1.Origin)
        sync d1 d2

        let el = Binding.read d2 enc2
        let dec = Decode.object { return! Decode.object.required "shout" Decode.custom }
        match Decode.runElement () dec el with
        | Ok v -> Expect.equal v "hi" "the remote edit surfaces through the custom's merged Value, via the same read"
        | Error e -> failwithf "decode failed: %A" e
    }

    test "a decode failure is a path-tracked value and the subscription survives it" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        let mutable notifications = 0
        use _s = Binding.subscribe p2.Doc p2.Att.Origin (fun () -> notifications <- notifications + 1)

        // A foreign client wrote a number where this schema expects a string.
        Y.transact (p1.Doc, (fun _ -> (p1.Doc.getMap () : Y.Map<obj>).set ("name", box 42) |> ignore), box "v99-client")
        sync p1.Doc p2.Doc
        Expect.equal notifications 1 "notified"
        match readModel p2 Model.init with
        | Error [ UnexpectedValue (path, _) ] -> Expect.equal path [ ObjectKey "name" ] "the failure names its path"
        | r -> failwithf "expected a single path-tracked error, got %A" r

        // The loop is not dead: when the foreign client heals the register,
        // the next notification decodes cleanly.
        Y.transact (p1.Doc, (fun _ -> (p1.Doc.getMap () : Y.Map<obj>).set ("name", box "healed") |> ignore), box "v99-client")
        sync p1.Doc p2.Doc
        Expect.equal notifications 2 "the subscription survived the failed decode"
        match readModel p2 Model.init with
        | Ok m -> Expect.equal m.Name "healed" "and later decodes still work"
        | Error e -> failwithf "decode failed: %A" e
    }

    test "L4 regression: a remote apply never triggers a re-entrant local write" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        // Count EVERY transaction on peer 2's doc (nothing filtered).
        let mutable p2Transactions = 0
        use _all = Binding.subscribe p2.Doc (box (obj ())) (fun () -> p2Transactions <- p2Transactions + 1)
        // Peer 2's decode loop: on remote change, read + decode + fold into its model.
        use _s = Binding.subscribe p2.Doc p2.Att.Origin (fun () ->
            match readModel p2 Model.init with
            | Ok m -> transact (fun () -> p2.Am.Update m)
            | Error _ -> ())

        transact (fun () ->
            p1.Am.Body.Value <- Text.edit "hello" p1.Am.Body.Value
            p1.Am.Name.Value <- "n")
        sync p1.Doc p2.Doc

        Expect.equal p2Transactions 1
            "exactly the remote apply — folding the decoded model back in produced NO local writes \
             (decoded values are content-equal, so the encode direction stays quiet)"
        Expect.equal (Text.toString (p2.Am.Body.Value)) "hello" "and the model took the remote state"
    }

    test "L4 regression: adaptive mutation outside transact throws (the constraint Step 7 must respect)" {
        let p = mkPeer ()
        Expect.throws (fun () -> p.Am.Name.Value <- "boom")
            "changeable values demand a transaction — the reference branch crashed on exactly this"
    }
]
