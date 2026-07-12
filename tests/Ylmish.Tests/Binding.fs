module Ylmish.Tests.Binding

// Plan 0002, Step 5 — the binding layer, encode direction. Pins the
// behavioural contract: no eager writes, bind-never-replace, leave what you
// don't own, origin-tagged transactions, structured schema drift, O(delta),
// and the semantic creations (map-item add, option None→Some) that flush.

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

let private root (d : Y.Doc) : Y.Map<obj> = d.getMap ()

let private sync (a : Y.Doc) (b : Y.Doc) =
    Y.applyUpdate (b, Y.encodeStateAsUpdate a, box "test-remote")
    Y.applyUpdate (a, Y.encodeStateAsUpdate b, box "test-remote")

let private rootKeys (d : Y.Doc) = (root d).keys () |> Seq.toList

/// A consumer-shaped keyed item: collaborative title, LWW done flag.
type private Item = { Title : cval<Text>; Done : cval<bool> }

let private item (title : string) =
    { Title = cval (Text.ofString title); Done = cval false }

let private encodeItem (i : Item) : Encoded =
    Encode.object [
        "title", Encode.text i.Title
        "done", Encode.bool i.Done
    ]

let tests = testList "Binding (encode direction)" [

    test "attach writes nothing — state flows only on local edits" {
        let doc = Y.Doc.Create ()
        let name = cval "nick"
        let encoded = Encode.object [ "name", Encode.string name ]
        use _att = Binding.attach doc encoded
        Expect.isEmpty (rootKeys doc) "no eager writes at attach (decode-empty = init)"

        transact (fun () -> name.Value <- "nickd")
        Expect.equal ((root doc).get "name" |> Option.map string) (Some "nickd")
            "the first local edit creates the entry"
    }

    test "writes are transacted under the attachment's origin token" {
        let doc = Y.Doc.Create ()
        let name = cval "a"
        use att = Binding.attach doc (Encode.object [ "name", Encode.string name ])
        let origins = ResizeArray<obj option> ()
        (root doc).observe (fun _e tr -> origins.Add tr.origin)
        transact (fun () -> name.Value <- "b")
        Expect.equal origins.Count 1 "one write, one transaction"
        Expect.equal origins.[0] (Some att.Origin) "tagged with the attachment's origin"
    }

    test "no-op re-stamps are skipped (a same-value set would clobber a concurrent peer)" {
        let doc = Y.Doc.Create ()
        let name = cval "a"
        use _att = Binding.attach doc (Encode.object [ "name", Encode.string name ])
        // A "remote" write lands the value the local model is about to set.
        (root doc).set ("name", box "b") |> ignore
        let mutable events = 0
        (root doc).observe (fun _ _ -> events <- events + 1)
        transact (fun () -> name.Value <- "b")
        Expect.equal events 0 "the equal value is not re-stamped"
        transact (fun () -> name.Value <- "c")
        Expect.equal events 1 "a genuinely new value still writes"
    }

    test "top-level records merge per-field across peers, even on concurrent FIRST edits (L8 + root anchoring)" {
        let mkPeer () =
            let doc = Y.Doc.Create ()
            let name = cval ""
            let bio = cval ""
            let encoded =
                Encode.object [
                    "author", Encode.object [
                        "name", Encode.string name
                        "bio", Encode.string bio
                    ]
                ]
            doc, name, bio, Binding.attach doc encoded
        let d1, name1, _, a1 = mkPeer ()
        let d2, _, bio2, a2 = mkPeer ()
        use _a1 = a1
        use _a2 = a2

        // Top-level containers anchor to NAMED root types (doc.getMap "author"),
        // which merge by name (U2b) — so even the very first, fully concurrent
        // offline edits cannot clobber each other. (A record nested DEEPER, at a
        // fixed path inside another container, still has the U2a first-create
        // residual — documented.)
        transact (fun () -> name1.Value <- "nick")   // peer 1's first-ever edit
        transact (fun () -> bio2.Value <- "fsharp")  // peer 2's first-ever edit, concurrent
        sync d1 d2

        let author (d : Y.Doc) : Y.Map<obj> = d.getMap "author"
        for d in [ d1; d2 ] do
            Expect.equal ((author d).get "name" |> Option.map string) (Some "nick") "name survives on both"
            Expect.equal ((author d).get "bio" |> Option.map string) (Some "fsharp") "bio survives on both"
    }

    test "text binds to the existing instance — a peer's concurrent edits survive (the U11 anti-test)" {
        let d1 = Y.Doc.Create ()
        let body = cval (Text.ofString "")
        use _att = Binding.attach d1 (Encode.object [ "body", Encode.text body ])
        transact (fun () -> body.Value <- Text.edit "hello" body.Value)
        let instanceBefore = d1.getText "body"

        // A second, raw peer gets the text and edits it concurrently.
        let d2 = Y.Doc.Create ()
        sync d1 d2
        let t2 : Y.Text = d2.getText "body"
        t2.insert (0, "oh, ")                                        // remote edit
        transact (fun () -> body.Value <- Text.insert 5 " world" body.Value) // local edit
        sync d1 d2

        let instanceAfter = d1.getText "body"
        Expect.isTrue (Object.ReferenceEquals (instanceBefore, instanceAfter))
            "the local edit adopted the integrated root Y.Text; it never replaced it"
        let s (d : Y.Doc) = (d.getText "body" : Y.Text).toString ()
        Expect.equal (s d1) "oh, hello world" "both peers' edits interleave (U3) — the #83 mechanics, fixed"
        Expect.equal (s d2) (s d1) "converged"
    }

    test "text intents flow as splices, not clear+reinsert" {
        let doc = Y.Doc.Create ()
        let body = cval (Text.ofString "")
        use _att = Binding.attach doc (Encode.object [ "body", Encode.text body ])
        transact (fun () -> body.Value <- Text.edit "hello world" body.Value)

        let ytext : Y.Text = doc.getText "body"
        let deltas = ResizeArray<int> ()
        ytext.observe (fun e _ -> deltas.Add e.delta.Count)
        // One splice: "hello world" -> "hello brave world" inserts " brave".
        transact (fun () -> body.Value <- Text.insert 5 " brave" body.Value)
        Expect.equal (ytext.toString ()) "hello brave world" "content correct"
        Expect.equal (Seq.toList deltas) [ 2 ] "one event: retain + insert — a splice, not a rewrite"
    }

    test "keys the encoder does not mention are never touched (U15)" {
        let doc = Y.Doc.Create ()
        (root doc).set ("someone-elses", box "v1-data") |> ignore
        let name = cval "a"
        use _att = Binding.attach doc (Encode.object [ "name", Encode.string name ])
        transact (fun () -> name.Value <- "b")
        Expect.equal ((root doc).get "someone-elses" |> Option.map string) (Some "v1-data")
            "foreign keys survive a full attach-and-edit session"
    }

    test "kind drift against existing doc state raises a structured, path-tracked error (L5)" {
        let doc = Y.Doc.Create ()
        // Another (older/newer) client stored a plain value where this schema
        // expects the item's object to live.
        (doc.getMap "todos" : Y.Map<obj>).set ("id-1", box "i am a string") |> ignore
        let items : cmap<string, Item> = cmap ()
        use _att = Binding.attach doc (Encode.object [ "todos", Encode.map encodeItem items ])
        let mutable caught = None
        try transact (fun () -> items.[ "id-1" ] <- item "buy milk")
        with Binding.SchemaDrift err -> caught <- Some err
        match caught with
        | Some (UnexpectedKind (path, _)) ->
            Expect.equal path [ MapKey "id-1"; ObjectKey "todos" ] "the error names the drifted path"
        | other -> failwithf "expected a SchemaDrift UnexpectedKind, got %A" other
    }

    test "keyed map: an add flushes the whole item, and concurrent adds both survive" {
        let mkPeer () =
            let doc = Y.Doc.Create ()
            let items : cmap<string, Item> = cmap ()
            doc, items, Binding.attach doc (Encode.object [ "todos", Encode.map encodeItem items ])
        let d1, items1, a1 = mkPeer ()
        let d2, items2, a2 = mkPeer ()
        use _a1 = a1
        use _a2 = a2

        transact (fun () -> items1.[ "id-1" ] <- item "buy milk")
        transact (fun () -> items2.[ "id-2" ] <- item "walk dog")

        // The add wrote ALL the item's fields, not just changed ones.
        let todos (d : Y.Doc) : Y.Map<obj> = d.getMap "todos"
        let itemMap (d : Y.Doc) k = (todos d).get k |> Option.get |> unbox<Y.Map<obj>>
        Expect.equal ((itemMap d1 "id-1").get "title" |> Option.map (fun t -> (unbox<Y.Text> t).toString ())) (Some "buy milk")
            "the added item's title was flushed"
        Expect.equal ((itemMap d1 "id-1").get "done" |> Option.map unbox<bool>) (Some false)
            "the added item's done flag was flushed"

        sync d1 d2
        for d in [ d1; d2 ] do
            Expect.equal ((todos d).keys () |> Seq.toList |> List.sort) [ "id-1"; "id-2" ]
                "offline creations with app-minted keys never race (the accepted-limitation rule, positively)"
    }

    test "keyed map: a nested text edit survives a concurrent membership change (the identity headline)" {
        let mkPeer () =
            let doc = Y.Doc.Create ()
            let items : cmap<string, Item> = cmap ()
            doc, items, Binding.attach doc (Encode.object [ "todos", Encode.map encodeItem items ])
        let d1, items1, a1 = mkPeer ()
        let d2, items2, a2 = mkPeer ()
        use _a1 = a1
        use _a2 = a2

        transact (fun () -> items1.[ "id-1" ] <- item "buy milk")
        sync d1 d2
        // Peer 2 must adopt the synced item into its model for its edit to bind:
        // simulate the decode step by pointing its cmap at an Item whose edits
        // flow to the SAME containers (adopted, not recreated).
        transact (fun () -> items2.[ "id-1" ] <- item "buy milk")

        // Concurrently: peer 1 adds another item; peer 2 edits the first item's title.
        transact (fun () -> items1.[ "id-2" ] <- item "walk dog")
        let editTitle (items : cmap<string, Item>) k f =
            transact (fun () ->
                let i = items.[ k ]
                i.Title.Value <- f i.Title.Value)
        editTitle items2 "id-1" (Text.insert 8 " oat")
        sync d1 d2

        let todos (d : Y.Doc) : Y.Map<obj> = d.getMap "todos"
        for d in [ d1; d2 ] do
            let title =
                (todos d).get "id-1" |> Option.get |> unbox<Y.Map<obj>>
                |> fun m -> m.get "title" |> Option.get |> unbox<Y.Text>
            Expect.equal (title.toString ()) "buy milk oat" "the edit stayed with its item"
            Expect.equal ((todos d).keys () |> Seq.toList |> List.sort) [ "id-1"; "id-2" ]
                "the membership change landed too"
    }

    test "keyed map: removal deletes the key" {
        let doc = Y.Doc.Create ()
        let items : cmap<string, Item> = cmap ()
        use _att = Binding.attach doc (Encode.object [ "todos", Encode.map encodeItem items ])
        transact (fun () -> items.[ "id-1" ] <- item "buy milk")
        transact (fun () -> items.Remove "id-1" |> ignore)
        let todos : Y.Map<obj> = doc.getMap "todos"
        Expect.isFalse (todos.has "id-1") "the item's key is gone"
    }

    test "list: local changes flow as index deltas; concurrent inserts both survive (U8)" {
        let mkPeer () =
            let doc = Y.Doc.Create ()
            let tags : clist<string> = clist []
            doc, tags, Binding.attach doc (Encode.object [ "tags", Encode.list Value.Encode.string tags ])
        let d1, tags1, a1 = mkPeer ()
        let d2, tags2, a2 = mkPeer ()
        use _a1 = a1
        use _a2 = a2

        transact (fun () -> tags1.Append "base" |> ignore)
        sync d1 d2
        // NB: peer 2's MODEL is not reconciled with the synced doc here — that
        // read-back is Step 6/7's job. Its local prepend applies against its
        // own (empty) prior state, which lands at index 0 of the shared array.
        transact (fun () -> tags1.Prepend "from-d1" |> ignore)
        transact (fun () -> tags2.Prepend "from-d2" |> ignore)
        sync d1 d2

        let read (d : Y.Doc) =
            (d.getArray "tags" : Y.Array<obj>).toArray () |> Seq.map string |> Seq.toList
        Expect.equal (read d1) (read d2) "converged"
        Expect.equal (read d1 |> List.sort) [ "base"; "from-d1"; "from-d2" ] "both concurrent inserts survive"
    }

    test "option: None→Some flushes at the transition, Some→None deletes, nothing while None" {
        let doc = Y.Doc.Create ()
        let note : cval<Text option> = cval None
        use _att = Binding.attach doc (Encode.object [ "note", Encode.option Encode.text note ])
        Expect.isEmpty (rootKeys doc) "None writes nothing"

        transact (fun () -> note.Value <- Some (Text.ofString "remember"))
        Expect.equal ((root doc).get "note" |> Option.map (fun t -> (unbox<Y.Text> t).toString ())) (Some "remember")
            "the Some transition creates the backing text with its content"

        transact (fun () -> note.Value <- Some (Text.insert 8 " milk" (Text.ofString "remember")))
        Expect.equal ((root doc).get "note" |> Option.map (fun t -> (unbox<Y.Text> t).toString ())) (Some "remember milk")
            "edits inside the Some window flow live"

        transact (fun () -> note.Value <- None)
        Expect.isFalse ((root doc).has "note") "Some→None deletes the key (absence, never null)"
    }

    test "option inside a replaced keyed-map item: Some→None deletes the key on both peers" {
        // Consumer-shaped: items are immutable records, so a one-field edit
        // replaces the item's WHOLE encoding — the option's own transition
        // callback never fires; the re-flush must reconcile the key itself.
        let encodeOptItem ((title, due) : string * string option) : Encoded =
            Encode.object [
                "title", Encode.string (AVal.constant title)
                "due", Encode.option Encode.string (AVal.constant due)
            ]
        let mkPeer () =
            let doc = Y.Doc.Create ()
            let items : cmap<string, string * string option> = cmap ()
            doc, items, Binding.attach doc (Encode.object [ "todos", Encode.map encodeOptItem items ])
        let d1, items1, a1 = mkPeer ()
        let d2, items2, a2 = mkPeer ()
        use _a1 = a1
        use _a2 = a2
        let itemMap (d : Y.Doc) k =
            (d.getMap "todos" : Y.Map<obj>).get k |> Option.get |> unbox<Y.Map<obj>>

        transact (fun () -> items1.[ "id-1" ] <- ("buy milk", None))
        Expect.isFalse ((itemMap d1 "id-1").has "due") "a fresh item's None writes nothing"

        // None→Some via wholesale item replacement creates the key.
        transact (fun () -> items1.[ "id-1" ] <- ("buy milk", Some "friday"))
        Expect.equal ((itemMap d1 "id-1").get "due" |> Option.map string) (Some "friday")
            "None→Some via item replacement flushes the key"

        sync d1 d2
        // Peer 2 adopts the synced item into its model (the decode step's job).
        transact (fun () -> items2.[ "id-1" ] <- ("buy milk", Some "friday"))

        // Some→None via wholesale item replacement must DELETE the key.
        transact (fun () -> items1.[ "id-1" ] <- ("buy milk", None))
        Expect.isFalse ((itemMap d1 "id-1").has "due")
            "Some→None via item replacement deletes the key locally"

        sync d1 d2
        for d in [ d1; d2 ] do
            Expect.isFalse ((itemMap d "id-1").has "due")
                "the deletion propagates — absence, never a stale value"
            Expect.equal ((itemMap d "id-1").get "title" |> Option.map string) (Some "buy milk")
                "the untouched field survives the replacement"
    }

    test "atomic: any inner change re-stamps the subtree wholesale" {
        let doc = Y.Doc.Create ()
        let name = cval "nick"
        let bio = cval "fsharp"
        let encoded =
            Encode.object [
                "profile", Encode.atomic (Encode.object [
                    "name", Encode.string name
                    "bio", Encode.string bio
                ])
            ]
        use _att = Binding.attach doc encoded
        transact (fun () -> name.Value <- "nickd")
        let profile = (root doc).get "profile" |> Option.get
        Expect.isFalse (isNull profile) "the whole subtree was stamped as one plain value"
    }

    test "custom: BindContext adopts a stable instance and the hatch's writes work" {
        let doc = Y.Doc.Create ()
        let mutable seen : Y.Text list = []
        let custom =
            { new CustomElement with
                member _.Connect ctx =
                    let t = ctx.GetText ()
                    seen <- t :: seen
                    let t2 = ctx.GetText ()
                    seen <- t2 :: seen
                    Y.transact (doc, (fun _ -> t.insert (0, "hi")), ctx.Origin)
                    { new IDisposable with member _.Dispose () = () }
                member _.Value = box 0 }
        use _att = Binding.attach doc (Encode.object [ "hits", Encode.custom custom ])
        match seen with
        | [ a; b ] -> Expect.isTrue (Object.ReferenceEquals (a, b)) "GetText is get-or-adopt: one integrated instance (U5)"
        | _ -> failwith "expected two GetText calls"
        Expect.equal ((doc.getText "hits" : Y.Text).toString ()) "hi"
            "the custom's Connect-driven write landed on the named root (race-free for eager Connects)"
    }

    test "the differential harness goes green where materialize failed (Step 2a's red, flipped)" {
        // A binding-backed bridge over the harness's membership model.
        let factory : Ylmish.Harness.BridgeFactory =
            fun doc ->
                let items : cmap<string, string> = cmap ()
                let encoded =
                    Encode.object [
                        "items", Encode.map (fun (id : string) -> Encode.string (AVal.constant id)) items
                    ]
                let _att = Binding.attach doc encoded
                { Name = "binding"
                  Doc = doc
                  Apply = fun _ m ->
                    transact (fun () ->
                        items.Value <- HashMap.ofList [ for i in m.Items -> i.Id, i.Text ])
                  Read = fun () ->
                    let ymap : Y.Map<obj> = doc.getMap "items"
                    { Ylmish.Harness.Model.Items =
                        [ for id in ymap.keys () ->
                            { Ylmish.Harness.Item.Id = id
                              Ylmish.Harness.Item.Text = ymap.get id |> Option.map string |> Option.defaultValue "" } ]
                      Ylmish.Harness.Model.Body = ""
                      Ylmish.Harness.Model.Note = "" } }

        let add r id : Ylmish.Harness.ReplicaOp = { Replica = r; Op = Ylmish.Harness.Add (id, "") }
        let d = Ylmish.Harness.differential factory Ylmish.Harness.Concurrent [ add 0 "a"; add 1 "b" ]
        Expect.isTrue d.Converged "binding replicas converge"
        Expect.isTrue d.MatchesOracle
            "the binding keeps BOTH concurrent adds — the exact schedule the materialize path failed"
        Expect.isEmpty (Set.toList d.Lost) "nothing lost"

        let seq = Ylmish.Harness.differential factory Ylmish.Harness.Immediate [ add 0 "a"; add 1 "b" ]
        Expect.isTrue seq.MatchesOracle "and sequential delivery still matches (discrimination intact)"
    }
]
