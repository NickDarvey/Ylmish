module Ylmish.Y.Assumptions

// Plan 0002, Step 1 — pin the Yjs assumptions the redesign depends on.
//
// These characterization tests exercise RAW Yjs (no Ylmish production code) so
// every "design consequence" claimed in doc/plans/0002-ylmish-redesign.md rests
// on observed behaviour, not on docs. If a Yjs upgrade changes any of these
// semantics, CI fails loudly and we revisit the design rather than shipping on a
// stale assumption.
//
// The U-ids match the "Validated assumptions" table in the plan. Each test's
// ground truth was captured by running doc/plans/0002-assumptions/*.mjs against
// the pinned yjs (13.6.x). The load-bearing results:
//   U2a  nested concurrent create clobbers wholesale  → create lazily, keyed
//   U3   shared nested Y.Text interleaves & converges  → bind, never replace
//   U3b  concurrent plain-string set is LWW            → the #83 failure mode
//   U5b  re-parenting an integrated type corrupts       → API forbids it
//   U6   transaction origins propagate with `local`    → origin echo-suppression
//   U11  replacing a Y.Text drops concurrent edits     → why materialize can't merge
//   U15  unknown keys survive an update exchange        → never delete foreign keys

open Yjs

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// `Y` is overloaded (the Yjs module, the exported Yjs class, and Ylmish.Y), so
// spell the raw Yjs types out for annotations and casts.
type private Doc = Yjs.Utils.Doc.Doc
type private YText = Yjs.Types.YText.YText
type private YMap<'a> = Yjs.Types.YMap.YMap<'a>
type private YArray<'a> = Yjs.Types.YArray.YArray<'a>

/// A doc with a deterministic clientID (real docs randomise it; concurrency
/// tests need the tiebreak to be predictable — see U4).
let private mkDoc (clientId : int) : Doc =
    let d = Y.Doc.Create ()
    d.clientID <- float clientId
    d

/// The unnamed root map (U1: `getMap()` is the root named "").
let private root (d : Doc) : YMap<obj> = d.getMap ()

/// Push all of `src`'s state into `dst` (one direction of a network round-trip).
let private sync (src : Doc) (dst : Doc) =
    Y.applyUpdate (dst, Y.encodeStateAsUpdate src)

/// Exchange state in both directions until the two docs converge.
let private syncBoth (a : Doc) (b : Doc) =
    sync a b
    sync b a

let tests = testList "Y.Assumptions" [

    // U1 — argless getMap is the stable root; every call returns the same instance.
    // Consequence: root binding needs no consumer-named root.
    testList "U1 root map identity" [
        test "getMap() returns the same instance on repeated calls" {
            let d = Y.Doc.Create ()
            let m1 = root d
            let m2 = root d
            Expect.isTrue (System.Object.ReferenceEquals (m1, m2))
                "getMap() must be idempotent — the root map is a stable binding target"
        }
    ]

    // U2a — THE offline-create race. Two peers each create a fresh nested type
    // under the SAME map key, then sync: one subtree wins WHOLESALE, the other's
    // data is silently discarded. Consequence: never create containers eagerly;
    // offline-creatable entities need consumer-supplied unique keys (Encode.map).
    testList "U2a nested concurrent create clobbers wholesale" [
        test "each peer creates its own Y.Array under the same key — one side is lost" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            let a1 : YArray<obj> = Y.Array.Create ()
            a1.push [| box "from-d1" |]
            (root d1).set ("todos", box a1) |> ignore
            let a2 : YArray<obj> = Y.Array.Create ()
            a2.push [| box "from-d2" |]
            (root d2).set ("todos", box a2) |> ignore

            syncBoth d1 d2

            let read (d : Doc) =
                ((root d).get "todos").Value :?> YArray<obj>
                |> fun a -> a.toArray () |> Seq.map (fun o -> unbox<string> o) |> Seq.toList
            let s1 = read d1
            let s2 = read d2
            let hasD1 = List.contains "from-d1" s1
            let hasD2 = List.contains "from-d2" s1
            Expect.equal s1 s2 "docs must converge (CRDT guarantees agreement)"
            Expect.isTrue (hasD1 || hasD2) "one peer's subtree survives"
            Expect.isFalse (hasD1 && hasD2)
                "U2a: nested concurrent create clobbers — both subtrees cannot survive"
        }
    ]

    // U2b — the SAME race, but with root-level types (getArray by name): these
    // merge by name, no wholesale loss. Consequence: root types are get-or-create
    // safe; only nested-under-a-key creation races.
    testList "U2b root-level types merge by name" [
        test "two peers naming the same root array — both items survive" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            (d1.getArray "todos" : YArray<string>).push [| "from-d1" |]
            (d2.getArray "todos" : YArray<string>).push [| "from-d2" |]

            syncBoth d1 d2

            let s1 = (d1.getArray "todos" : YArray<string>).toArray () |> Seq.toList
            Expect.isTrue (List.contains "from-d1" s1 && List.contains "from-d2" s1)
                "root types with the same name merge — no wholesale loss (contrast U2a)"
        }
    ]

    // U3 — concurrent edits to ONE shared nested Y.Text (created once, then
    // synced) interleave and converge. This is the whole point of the redesign.
    testList "U3 shared nested Y.Text interleaves and converges" [
        test "concurrent inserts on a shared Y.Text both survive" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            let t = Y.Text.Create "hello"
            (root d1).set ("body", box t) |> ignore
            syncBoth d1 d2 // both now share the same text

            ((root d1).get "body").Value :?> YText |> fun t -> t.insert (5, " world")
            ((root d2).get "body").Value :?> YText |> fun t -> t.insert (0, "oh, ")
            syncBoth d1 d2

            let read (d : Doc) = (((root d).get "body").Value :?> YText).toString ()
            let s1 = read d1
            let s2 = read d2
            Expect.equal s1 s2 "shared-text docs must converge"
            Expect.isTrue (s1.Contains "world" && s1.Contains "oh, ")
                "both peers' insertions survive on a shared nested Y.Text (interleaved, not clobbered)"
        }
    ]

    // U3b — issue #83's failure mode. A string stored as a plain map value is an
    // LWW register: concurrent sets clobber, one side's edit is lost.
    testList "U3b concurrent plain-string set is LWW" [
        test "two peers set the same key to different strings — one is lost" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            (root d1).set ("body", box "hello") |> ignore
            syncBoth d1 d2
            (root d1).set ("body", box "hello world") |> ignore
            (root d2).set ("body", box "oh, hello") |> ignore
            syncBoth d1 d2

            let s1 = unbox<string> ((root d1).get "body").Value
            let s2 = unbox<string> ((root d2).get "body").Value
            Expect.equal s1 s2 "register converges"
            Expect.isTrue (s1 = "hello world" || s1 = "oh, hello")
                "exactly one concurrent set wins — a plain string is an LWW register (issue #83)"
        }
    ]

    // U4 — the LWW winner is deterministic and order-independent: the HIGHER
    // clientID wins, not wall-clock, not apply-order. Consequence: document "last
    // writer" honestly as an arbitrary-but-convergent tiebreak.
    testList "U4 LWW winner is the higher clientID" [
        test "winner is order-independent and picks the higher clientID" {
            for (c1, c2) in [ (1, 2); (2, 1); (100, 5) ] do
                let d1 = mkDoc c1
                let d2 = mkDoc c2
                (root d1).set ("k", box ("v-from-" + string c1)) |> ignore
                (root d2).set ("k", box ("v-from-" + string c2)) |> ignore
                syncBoth d1 d2
                let winner = unbox<string> ((root d1).get "k").Value
                let loser = unbox<string> ((root d2).get "k").Value
                Expect.equal winner loser "both docs agree on the winner"
                Expect.equal winner ("v-from-" + string (max c1 c2))
                    "the higher clientID wins — deterministic, not wall-clock"
        }
    ]

    // U5b — re-parenting an already-integrated shared type CORRUPTS the doc: the
    // call site may not throw, but a later sync throws and content is lost.
    // Consequence: the public API must make re-parenting impossible by
    // construction (bind each field to exactly one instance, never reuse).
    testList "U5b re-parenting an integrated type corrupts the doc" [
        test "re-setting an integrated Y.Text under another key breaks a later sync" {
            let d1 = Y.Doc.Create ()
            let t = Y.Text.Create "x"
            (root d1).set ("a", box t) |> ignore

            let mutable setThrew = false
            try (root d1).set ("b", box t) |> ignore
            with _ -> setThrew <- true

            let mutable syncThrew = false
            let d2 = Y.Doc.Create ()
            try sync d1 d2
            with _ -> syncThrew <- true

            // Content survives ONLY if re-parenting were safe; here it is not.
            let contentLost =
                try
                    let a = (root d2).get "a"
                    match a with
                    | Some v -> (v :?> YText).toString () <> "x"
                    | None -> true
                with _ -> true

            Expect.isTrue (setThrew || syncThrew || contentLost)
                "re-parenting an integrated type is unsafe: it throws or corrupts/loses content. \
                 If this ever becomes safe, the 'bind, never re-parent' API constraint can be relaxed."
        }
    ]

    // U6 — transaction origins propagate to observers with a correct `local`
    // flag, and remote applyUpdate origins are visible with local=false.
    // Consequence: echo suppression via a per-binding origin token (not booleans).
    testList "U6 transaction origins propagate" [
        test "explicit origin and remote-apply origin reach observers with local flags" {
            let d1 = Y.Doc.Create ()
            let seen = ResizeArray<obj option * bool> ()
            (root d1).observe (fun _ (tr : Yjs.Utils.Transaction.Transaction) ->
                seen.Add (tr.origin, tr.local))

            d1.transact ((fun _ -> (root d1).set ("k", box 1) |> ignore), "my-origin")
            (root d1).set ("k", box 2) |> ignore // no explicit origin
            let d2 = Y.Doc.Create ()
            (root d2).set ("k", box 3) |> ignore
            Y.applyUpdate (d1, Y.encodeStateAsUpdate d2, "remote-origin")

            Expect.equal seen.Count 3 "observer fires once per transaction"
            let originStr (o : obj option) = match o with Some v -> unbox<string> v | None -> "<none>"
            let (o0, l0) = seen.[0]
            Expect.equal (originStr o0) "my-origin" "explicit transaction origin propagates"
            Expect.isTrue l0 "a local transaction is flagged local=true"
            let (o2, l2) = seen.[2]
            Expect.equal (originStr o2) "remote-origin" "applyUpdate origin propagates"
            Expect.isFalse l2 "a remote-applied transaction is flagged local=false"
        }
    ]

    // U7 — one observeDeep on the root sees nested edits: editing a nested Y.Text
    // fires the root's deep observer, and the event targets that nested type.
    // Consequence: a single root observer covers the whole bound tree.
    testList "U7 root observeDeep sees nested edits" [
        test "a nested Y.Text edit fires the root's observeDeep, targeting the text" {
            let d = Y.Doc.Create ()
            let t = Y.Text.Create ""
            (root d).set ("body", box t) |> ignore

            let targets = ResizeArray<obj> ()
            (root d).observeDeep (fun evts _ ->
                for e in evts do targets.Add (box e.target))

            t.insert (0, "hi")

            Expect.isTrue (targets.Count > 0)
                "editing a nested type must reach the single root observeDeep (paths e.g. [\"body\"] per the .mjs spike)"
            Expect.isTrue (targets |> Seq.exists (fun o -> System.Object.ReferenceEquals (o, t)))
                "the deep event targets the nested Y.Text itself — no per-child observer needed"
        }
    ]

    // U8 — concurrent inserts into a shared Y.Array both survive, deterministic
    // order. Consequence: lists merge; a safe default for value sequences.
    testList "U8 concurrent array inserts both survive" [
        test "two peers insert at the same position — both items survive" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            let arr : YArray<obj> = Y.Array.Create ()
            arr.push [| box "base" |]
            (root d1).set ("xs", box arr) |> ignore
            syncBoth d1 d2

            (((root d1).get "xs").Value :?> YArray<obj>).insert (0, [| box "from-d1" |])
            (((root d2).get "xs").Value :?> YArray<obj>).insert (0, [| box "from-d2" |])
            syncBoth d1 d2

            let read (d : Doc) =
                (((root d).get "xs").Value :?> YArray<obj>).toArray ()
                |> Seq.map (fun o -> unbox<string> o) |> Seq.toList
            let s1 = read d1
            Expect.equal (read d1) (read d2) "array docs converge"
            Expect.isTrue (List.contains "from-d1" s1 && List.contains "from-d2" s1)
                "both concurrent inserts survive"
        }
    ]

    // U9 — deleting a nested item beats a concurrent edit inside it: the delete
    // wins, the edit is lost. Documented, pinned limit.
    testList "U9 delete beats concurrent edit-inside" [
        test "one peer deletes an item while the other edits inside it — delete wins" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            let item : YMap<obj> = Y.Map.Create ()
            item.set ("title", box "a") |> ignore
            let arr : YArray<obj> = Y.Array.Create ()
            arr.push [| box item |]
            (root d1).set ("xs", box arr) |> ignore
            syncBoth d1 d2

            (((root d1).get "xs").Value :?> YArray<obj>).delete (0, 1)               // d1 deletes
            let item2 = (((root d2).get "xs").Value :?> YArray<obj>).get 0 :?> YMap<obj>
            item2.set ("title", box "b") |> ignore                                    // d2 edits inside
            syncBoth d1 d2

            let len (d : Doc) =
                (((root d).get "xs").Value :?> YArray<obj>).toArray () |> Seq.length
            Expect.equal (len d1) 0 "deleted item stays deleted on d1"
            Expect.equal (len d2) 0 "delete wins over the concurrent edit-inside on d2"
        }
    ]

    // U10 — Y.Map holds any JSON primitive (number, bool, null, string, object,
    // array), not only strings. Consequence: v2 elements carry typed primitives.
    testList "U10 Y.Map holds typed primitives" [
        test "number, bool, null and string round-trip through a Y.Map value" {
            let d = Y.Doc.Create ()
            let m = root d
            m.set ("num", box 42) |> ignore
            m.set ("bool", box true) |> ignore
            m.set ("nul", null) |> ignore
            m.set ("str", box "s") |> ignore
            Expect.equal (unbox<int> (m.get "num").Value) 42 "number survives"
            Expect.equal (unbox<bool> (m.get "bool").Value) true "bool survives"
            Expect.equal (unbox<string> (m.get "str").Value) "s" "string survives"
            // A stored null is a proper Y.Map value (JS distinguishes it from a
            // missing key); Fable collapses it to `None` at the option boundary, so
            // assert presence via `has` rather than reading `.Value`.
            Expect.isTrue (m.has "nul") "null is a proper stored value, not a missing key"
        }
    ]

    // U11 — replacing a nested Y.Text with a fresh instance wins over a peer's
    // concurrent edit to the OLD text; the peer's edit is lost. This is exactly
    // why materialize-per-update can never merge. Consequence: bind, never replace.
    testList "U11 replacing a Y.Text drops concurrent edits" [
        test "replacement wins; a concurrent edit of the old text is lost" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            let t1 = Y.Text.Create ""
            t1.insert (0, "v1")
            (root d1).set ("body", box t1) |> ignore
            syncBoth d1 d2
            let t2ref = ((root d2).get "body").Value :?> YText

            let tNew = Y.Text.Create ""
            tNew.insert (0, "v2")
            (root d1).set ("body", box tNew) |> ignore // d1 REPLACES the text
            t2ref.insert (2, "!")                       // d2 edits the OLD text
            syncBoth d1 d2

            let read (d : Doc) = (((root d).get "body").Value :?> YText).toString ()
            let s1 = read d1
            Expect.equal (read d1) (read d2) "docs converge"
            Expect.isTrue (s1.Contains "v2") "the replacement wins"
            Expect.isFalse (s1.Contains "!") "the concurrent edit of the replaced text is lost"
        }
    ]

    // U13 — a concurrent "move" (delete+insert) of the same item duplicates it.
    // Consequence: prefer keyed-map + order-field over structural moves.
    testList "U13 concurrent move duplicates the item" [
        test "both peers move the same item to the end — it appears twice" {
            let d1 = mkDoc 1
            let d2 = mkDoc 2
            let arr : YArray<obj> = Y.Array.Create ()
            arr.push [| box "a"; box "b"; box "c" |]
            (root d1).set ("xs", box arr) |> ignore
            syncBoth d1 d2

            let move (d : Doc) =
                let xs = ((root d).get "xs").Value :?> YArray<obj>
                d.transact (fun _ ->
                    let v = xs.get 0
                    xs.delete (0, 1)
                    xs.push [| v |])
            move d1
            move d2
            syncBoth d1 d2

            let read (d : Doc) =
                (((root d).get "xs").Value :?> YArray<obj>).toArray ()
                |> Seq.map (fun o -> unbox<string> o) |> Seq.toList
            let s1 = read d1
            Expect.equal (read d1) (read d2) "docs converge"
            Expect.equal (s1 |> List.filter ((=) "a") |> List.length) 2
                "the concurrently-moved item is duplicated (Yjs v13 structural-move limit)"
        }
    ]

    // U14 — a single doc.transact spanning many nested types produces exactly ONE
    // observeDeep batch. Consequence: a remote transaction is one Elmish Set.
    testList "U14 one transaction is one observeDeep batch" [
        test "a transact touching text, array and a map key fires one deep batch" {
            let d = Y.Doc.Create ()
            let t = Y.Text.Create ""
            let a : YArray<obj> = Y.Array.Create ()
            (root d).set ("t", box t) |> ignore
            (root d).set ("a", box a) |> ignore

            let mutable batches = 0
            let mutable evtsTotal = 0
            (root d).observeDeep (fun evts _ ->
                batches <- batches + 1
                evtsTotal <- evtsTotal + evts.Count)

            d.transact (fun _ ->
                t.insert (0, "hi")
                a.push [| box 1 |]
                (root d).set ("k", box "v") |> ignore)

            Expect.equal batches 1 "one transaction yields one observeDeep batch (⇒ one Elmish Set)"
            Expect.isTrue (evtsTotal > 0) "the batch carries the per-type events"
        }
    ]

    // U15 — applying an update carrying keys a client doesn't understand is clean:
    // unknown keys are preserved and readable. Consequence: forward compatibility
    // is free IF the runtime stops deleting unknown keys (foundation for migration).
    testList "U15 unknown keys survive an update exchange" [
        test "an old client applies an update adding an unknown key without loss" {
            let oldc = Y.Doc.Create ()
            let newc = Y.Doc.Create ()
            (root oldc).set ("v1", box "old-data") |> ignore
            syncBoth oldc newc

            let t = Y.Text.Create ""
            t.insert (0, "new-data")
            (root newc).set ("v2", box t) |> ignore // a key the old client never knew

            let mutable threw = false
            try syncBoth oldc newc
            with _ -> threw <- true

            Expect.isFalse threw "applying an update with unknown keys must not throw"
            Expect.equal (unbox<string> ((root oldc).get "v1").Value) "old-data"
                "the old client's own data is untouched"
            Expect.isTrue ((root oldc).has "v2")
                "the unknown key is preserved and readable (foundation for schema migration)"
        }
    ]
]
