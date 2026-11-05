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

// --- Y types the codec has no Element case for --------------------------------
// A structural read classifies a live Y value by runtime kind. Anything outside
// Text/Map/Array used to fall into the plain-object arm and be walked field by
// field through its internals (`_item`, `_start`, `parent`, `doc`, …), which are
// cyclic — so the read never returned. These pin that such a value is skipped.

let unrepresentable = testList "Y types the codec cannot represent" [

    test "Decode.run skips an undeclared root type it cannot represent" {
        let doc = Y.Doc.Create ()
        // The codec's own root, plus the sibling root a y-prosemirror editor is
        // normally anchored to — which no decoder here asks for.
        Y.transact (doc, (fun _ ->
            (doc.getText "title").insert (0, "groceries")
            (doc.getXmlFragment "body").push [| Fable.Core.U2.Case1 (Y.XmlElement.Create "paragraph") |]), box "app")

        let decoder =
            Decode.object {
                let! title = Decode.object.optional "title" Decode.text
                let! body = Decode.object.optional "body" Decode.text
                return (title |> Option.map Text.toString), (body |> Option.map Text.toString)
            }

        match Decode.run () decoder doc with
        | Ok (title, body) ->
            Expect.equal title (Some "groceries") "the declared root decodes"
            Expect.equal body None "the fragment is absent, not walked into its cyclic internals"
        | Error e -> failwithf "decode failed: %A" e
    }

    test "Binding.read: a fragment in a slot the schema types as text is a decoder error, not a crash" {
        let doc = Y.Doc.Create ()
        let encoded = Encode.object [ "draft", Encode.object [ "body", Encode.text (cval Text.empty) ] ]
        // A peer speaking a different schema put an XmlFragment where this
        // encoding says text.
        Y.transact (doc, (fun _ ->
            let m : Y.Map<obj> = doc.getMap "draft"
            let f = Y.XmlFragment.Create ()
            m.set ("body", box f) |> ignore
            f.push [| Fable.Core.U2.Case1 (Y.XmlElement.Create "paragraph") |]), box "v99-client")

        let decoder =
            Decode.object {
                return! Decode.object.required "draft" (Decode.object {
                    return! Decode.object.required "body" Decode.text
                })
            }

        match Decode.runElement () decoder (Binding.read doc encoded) with
        | Error [ MissingProperty path ] ->
            Expect.equal path [ ObjectKey "body"; ObjectKey "draft" ] "the failure names the slot's path"
        | r -> failwithf "expected a path-tracked decoder error, got %A" r
    }

    test "Encode.atomic still round-trips through a live doc read" {
        let profile = cval ("", false)
        let encoded =
            Encode.object [
                "profile", Encode.atomic (Encode.object [
                    "name", Encode.string (profile |> AVal.map fst)
                    "admin", Encode.bool (profile |> AVal.map snd)
                ])
            ]
        let d1 = Y.Doc.Create ()
        let _att = Binding.attach d1 encoded
        transact (fun () -> profile.Value <- ("nick", true))
        let d2 = Y.Doc.Create ()
        sync d1 d2

        let decoder =
            Decode.object {
                return! Decode.object.required "profile" (Decode.atomic (Decode.object {
                    let! name = Decode.object.required "name" Decode.string
                    let! admin = Decode.object.required "admin" Decode.bool
                    return name, admin
                }))
            }

        match Decode.runElement () decoder (Binding.read d2 encoded) with
        | Ok v -> Expect.equal v ("nick", true) "the plain object Encode.atomic wrote reads back as an atomic"
        | Error e -> failwithf "decode failed: %A" e
    }

    test "an empty doc still decodes to the decoder's empty" {
        match Decode.run Model.init decode (Y.Doc.Create ()) with
        | Ok m -> Expect.equal m Model.init "nothing in the doc, nothing claimed"
        | Error e -> failwithf "decode failed: %A" e
    }
]

// --- Bound roots: a signal only for transactions that change what read reads ----
// The same Y.Doc carries roots the codec never binds — an editor's own
// Y.XmlFragment, typically, written on every keystroke. `subscribeBound`
// signals only for transactions that change a root in the codec's layout:
// "" (top-level registers, options, atomics) plus each top-level container or
// custom's named root. The receiving doc in these tests never instantiates
// anything locally, so every bound root arrives as a peer-created placeholder
// — the case `changedParentTypes` cannot see.

let private noopCustom =
    { new CustomElement with
        member _.Connect _ = { new IDisposable with member _.Dispose () = () }
        member _.Value = box 0 }

/// One top-level prop of every kind the codec has, nesting under "obj".
let private everyKind : Encoded =
    Encode.object [
        "reg", Encode.string (AVal.constant "")
        "opt", Encode.option Encode.text (AVal.constant None)
        "atom", Encode.atomic (Encode.object [ "a", Encode.int (AVal.constant 0) ])
        "obj", Encode.object [ "inner", Encode.object [ "deep", Encode.text (AVal.constant Text.empty) ] ]
        "map", Encode.map (fun (s : string) -> Encode.string (AVal.constant s)) AMap.empty
        "list", Encode.list Value.Encode.string AList.empty
        "text", Encode.text (AVal.constant Text.empty)
        "custom", Encode.custom noopCustom
    ]

/// A fresh receiver that has instantiated nothing, counting `subscribeBound`
/// signals for `everyKind`.
let private mkReceiver () =
    let doc = Y.Doc.Create ()
    let count = ref 0
    let sub = Binding.subscribeBound doc (box (obj ())) everyKind (fun () -> count.Value <- count.Value + 1)
    doc, count, sub

let private remote (src : Y.Doc) (f : unit -> unit) =
    Y.transact (src, (fun _ -> f ()), box "peer")

let private paragraph () = Fable.Core.U2.Case1 (Y.XmlElement.Create "paragraph")

let boundRoots = testList "Binding.subscribeBound (bound roots only)" [

    // Every place the codec's layout puts state, written by a remote peer into
    // a doc that never instantiated it: one transaction, one signal.
    let cases : (string * (Y.Doc -> unit)) list = [
        "a top-level register (argless root map)", fun d -> (d.getMap () : Y.Map<obj>).set ("reg", box "x") |> ignore
        "a top-level option (argless root map)", fun d -> (d.getMap () : Y.Map<obj>).set ("opt", box (Y.Text.Create ())) |> ignore
        "a top-level atomic (argless root map)", fun d -> (d.getMap () : Y.Map<obj>).set ("atom", box 1) |> ignore
        "a top-level object", fun d -> (d.getMap "obj" : Y.Map<obj>).set ("k", box 1) |> ignore
        "a top-level keyed map", fun d -> (d.getMap "map" : Y.Map<obj>).set ("id-1", box "t") |> ignore
        "a top-level list", fun d -> (d.getArray "list" : Y.Array<obj>).push [| box "x" |]
        "a top-level text", fun d -> (d.getText "text" : Y.Text).insert (0, "x")
        "a top-level custom (an XML fragment)", fun d -> (d.getXmlFragment "custom").push [| paragraph () |]
    ]
    for (label, write) in cases do
        test $"a remote write to {label} the receiver never instantiated: one signal" {
            let peer = Y.Doc.Create ()
            let doc, count, sub = mkReceiver ()
            use _ = sub
            remote peer (fun () -> write peer)
            sync peer doc
            Expect.equal count.Value 1 "a bound root changed: exactly one signal"
        }

    test "the remote-created root really is a placeholder here — matched by name, not type" {
        let peer = Y.Doc.Create ()
        let doc, count, sub = mkReceiver ()
        use _ = sub
        Expect.isEmpty (Interop.shareKeys doc |> Array.toList) "the receiver instantiated nothing"
        remote peer (fun () -> (peer.getMap "map" : Y.Map<obj>).set ("id-1", box "t") |> ignore)
        sync peer doc
        Expect.isNone (Y.Map.tryOf (doc.share.get "map")) "the root arrived as a placeholder, not a Y.Map"
        Expect.equal count.Value 1 "and it still signalled"
    }

    // An 'update' listener registered before the signal's can type the
    // peer-created root first. Yjs then swaps the placeholder in `doc.share`
    // for a new Y.Map, so the type the transaction recorded no longer resolves
    // to any name. Unresolvable must count as bound, or the edit is lost.
    test "a bound root another listener typed first, unresolvable by then, still signals" {
        let peer = Y.Doc.Create ()
        let doc = Y.Doc.Create ()
        doc.onUpdate (Y.UpdateHandler (fun _ _ _ _ -> (doc.getMap "map" : Y.Map<obj>) |> ignore))
        let mutable count = 0
        use _s = Binding.subscribeBound doc (box (obj ())) everyKind (fun () -> count <- count + 1)
        remote peer (fun () -> (peer.getMap "map" : Y.Map<obj>).set ("id-1", box "t") |> ignore)
        sync peer doc
        Expect.equal count 1 "an unresolvable root counts as bound: exactly one signal"
    }

    test "a change nested deep inside a bound root signals once" {
        let peer = Y.Doc.Create ()
        let doc, count, sub = mkReceiver ()
        use _ = sub
        let deep : Y.Text = Y.Text.Create ()
        remote peer (fun () ->
            let inner : Y.Map<obj> = Y.Map.Create ()
            (peer.getMap "obj" : Y.Map<obj>).set ("inner", box inner) |> ignore
            inner.set ("deep", box deep) |> ignore)
        sync peer doc
        Expect.equal count.Value 1 "creating the nested structure: one signal"

        // Now ONLY the deep text changes: the transaction's changed set holds
        // just that Y.Text, two levels below its root.
        remote peer (fun () -> deep.insert (0, "hi"))
        sync peer doc
        Expect.equal count.Value 2 "a deep-only edit walks up to its bound root: one more signal"
    }

    test "a remote write only to roots the codec does not bind: no signal" {
        let peer = Y.Doc.Create ()
        let doc, count, sub = mkReceiver ()
        use _ = sub
        remote peer (fun () ->
            (peer.getXmlFragment "draft").push [| paragraph () |]
            (peer.getText "elsewhere").insert (0, "typing")
            (peer.getMap "unbound" : Y.Map<obj>).set ("k", box 1) |> ignore)
        sync peer doc
        Expect.equal count.Value 0 "an editor's own roots are invisible to the codec's signal"

        // ...nested changes under an unbound root too.
        remote peer (fun () ->
            let m : Y.Map<obj> = Y.Map.Create ()
            (peer.getMap "unbound" : Y.Map<obj>).set ("nested", box m) |> ignore
            m.set ("x", box 1) |> ignore)
        sync peer doc
        Expect.equal count.Value 0 "however deep"
    }

    test "a local foreign write only to an unbound root: no signal" {
        let doc, count, sub = mkReceiver ()
        use _ = sub
        Y.transact (doc, (fun _ -> (doc.getXmlFragment "draft").push [| paragraph () |]), box "y-prosemirror")
        Expect.equal count.Value 0 "the editor typing into its own fragment does not signal"
    }

    test "one transaction touching unbound AND several bound roots: exactly one signal (U14)" {
        let peer = Y.Doc.Create ()
        let doc, count, sub = mkReceiver ()
        use _ = sub
        remote peer (fun () ->
            (peer.getXmlFragment "draft").push [| paragraph () |]
            (peer.getMap () : Y.Map<obj>).set ("reg", box "x") |> ignore
            (peer.getText "text" : Y.Text).insert (0, "x")
            (peer.getMap "map" : Y.Map<obj>).set ("id-1", box "t") |> ignore)
        sync peer doc
        Expect.equal count.Value 1 "one transaction, one signal, however many roots it spans"
    }

    test "own-origin writes to bound roots still never signal (U6)" {
        let doc = Y.Doc.Create ()
        let own = box (obj ())
        let mutable count = 0
        use _s = Binding.subscribeBound doc own everyKind (fun () -> count <- count + 1)
        Y.transact (doc, (fun _ -> (doc.getMap () : Y.Map<obj>).set ("reg", box "x") |> ignore), own)
        Y.transact (doc, (fun _ -> (doc.getText "text" : Y.Text).insert (0, "x")), own)
        Expect.equal count 0 "echo suppression is unchanged"
    }
]
