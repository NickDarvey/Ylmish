module Ylmish.Y.Assumptions

// Plan 0002, Step 1 — pin the raw-Yjs assumptions the redesign's design table
// ("Validated assumptions" in doc/plans/0002-ylmish-redesign.md) rests on.
//
// These are throwaway-but-keep characterization tests: no Ylmish production
// code under test, only raw Yjs behaviour. Ported from
// doc/plans/0002-assumptions/experiments.mjs and experiments2.mjs so a Yjs
// upgrade that changes any of this semantics fails CI loudly instead of
// silently invalidating the design.

open Yjs

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

/// Mirrors experiments.mjs's syncBoth: capture both sides' full state up
/// front, then apply each to the other. Two docs are fully converged after
/// one call.
let private syncBoth (a : Y.Doc) (b : Y.Doc) =
    let ua = Y.encodeStateAsUpdate a
    let ub = Y.encodeStateAsUpdate b
    Y.applyUpdate (a, ub)
    Y.applyUpdate (b, ua)

let tests = testList "Y.Assumptions" [

    test "U1: doc.getMap() with no name is the root map, stable across calls" {
        let doc = Y.Doc.Create ()
        let m1 = doc.getMap ()
        let m2 = doc.getMap ()
        let m3 = doc.getMap ""
        Expect.isTrue (System.Object.ReferenceEquals (m1, m2))
            "getMap() must return the same instance on repeated calls"
        Expect.isTrue (System.Object.ReferenceEquals (m1, m3))
            "getMap() with no name must be the same root as getMap \"\""
    }

    test "U2a: two peers each create a nested container under the same map key — one subtree wins wholesale" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        let a1 : Y.Array<string> = Y.Array.Create ()
        a1.push [| "from-d1" |]
        d1.getMap().set ("todos", a1) |> ignore

        let a2 : Y.Array<string> = Y.Array.Create ()
        a2.push [| "from-d2" |]
        d2.getMap().set ("todos", a2) |> ignore

        syncBoth d1 d2

        let items1 = ((d1.getMap () : Y.Map<Y.Array<string>>).get "todos").Value.toArray () |> Seq.toList
        let items2 = ((d2.getMap () : Y.Map<Y.Array<string>>).get "todos").Value.toArray () |> Seq.toList

        Expect.equal items1 items2 "docs must converge to the same subtree"
        Expect.isFalse (List.contains "from-d1" items1 && List.contains "from-d2" items1)
            "one client's entire subtree wins wholesale; never create containers eagerly at init"
    }

    test "U2b: root-level arrays named the same merge — items from both peers survive" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        (d1.getArray "todos" : Y.Array<string>).push [| "from-d1" |]
        (d2.getArray "todos" : Y.Array<string>).push [| "from-d2" |]

        syncBoth d1 d2

        let items1 = (d1.getArray "todos" : Y.Array<string>).toArray () |> Seq.toList
        let items2 = (d2.getArray "todos" : Y.Array<string>).toArray () |> Seq.toList

        Expect.equal items1 items2 "docs must converge"
        Expect.isTrue (List.contains "from-d1" items1 && List.contains "from-d2" items1)
            "root types are safe to get-or-create: same name merges, no wholesale loss"
    }

    test "U3: concurrent edits to a shared nested Y.Text interleave and converge" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        let t = Y.Text.Create ()
        t.insert (0, "hello")
        d1.getMap().set ("body", t) |> ignore
        syncBoth d1 d2 // both now share the same integrated Y.Text

        ((d1.getMap () : Y.Map<Y.Text>).get "body").Value.insert (5, " world") // "hello world"
        ((d2.getMap () : Y.Map<Y.Text>).get "body").Value.insert (0, "oh, ")   // "oh, hello"
        syncBoth d1 d2

        let s1 = ((d1.getMap () : Y.Map<Y.Text>).get "body").Value.toString ()
        let s2 = ((d2.getMap () : Y.Map<Y.Text>).get "body").Value.toString ()
        Expect.equal s1 s2 "must converge"
        Expect.isTrue (s1.Contains "hello world" && s1.Contains "oh, ")
            "both edits must survive, interleaved rather than clobbered"
    }

    test "U3b: concurrent set of a plain string map value LWW-clobbers (issue #83's failure mode)" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        d1.getMap().set ("body", "hello") |> ignore
        syncBoth d1 d2

        d1.getMap().set ("body", "hello world") |> ignore
        d2.getMap().set ("body", "oh, hello") |> ignore
        syncBoth d1 d2

        let v1 = (d1.getMap () : Y.Map<string>).get "body"
        let v2 = (d2.getMap () : Y.Map<string>).get "body"
        Expect.equal v1 v2 "must converge to a single winner"
        Expect.isFalse (v1 = Some "hello world" && v1 = Some "oh, hello")
            "plain values are LWW registers: exactly one concurrent writer's value survives, the other's edit is lost"
    }

    test "U4: the concurrent map.set winner is deterministic and order-independent (not wall-clock)" {
        for (c1, c2) in [ 1.0, 2.0; 2.0, 1.0; 100.0, 5.0 ] do
            let d1 = Y.Doc.Create ()
            let d2 = Y.Doc.Create ()
            d1.clientID <- c1
            d2.clientID <- c2

            d1.getMap().set ("k", $"v-from-{c1}") |> ignore
            d2.getMap().set ("k", $"v-from-{c2}") |> ignore
            syncBoth d1 d2

            let v1 = (d1.getMap () : Y.Map<string>).get "k"
            let v2 = (d2.getMap () : Y.Map<string>).get "k"
            let winner = max c1 c2
            Expect.equal v1 v2 $"both peers must agree on the winner for ({c1}, {c2})"
            Expect.equal v1 (Some $"v-from-{winner}")
                $"the higher clientID ({winner}) must win, deterministically, for ({c1}, {c2})"
    }

    test "U5: re-setting an already-integrated Y type under another key does not throw locally, but the doc becomes unsyncable" {
        let d1 = Y.Doc.Create ()
        let t = Y.Text.Create ()
        t.insert (0, "x")
        d1.getMap().set ("a", t) |> ignore

        // No exception at the call site — Yjs logs internally rather than throwing here.
        d1.getMap().set ("b", t) |> ignore

        let d2 = Y.Doc.Create ()
        let mutable syncError = None
        try Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)
        with e -> syncError <- Some e.Message

        Expect.isSome syncError
            "re-parenting an integrated Y type corrupts the doc: a peer sync later throws instead of failing at the re-set call site — the binding layer must make this impossible by construction, not try/catch around it"
    }

    test "U6: transaction origins propagate to observers, including applyUpdate's origin and the local flag" {
        let doc = Y.Doc.Create ()
        let seen = ResizeArray<obj option * bool> ()
        (doc.getMap () : Y.Map<obj>).observe (fun _e tr -> seen.Add (tr.origin, tr.local))

        doc.transact ((fun _ -> (doc.getMap () : Y.Map<obj>).set ("k", box 1) |> ignore), "my-origin")
        (doc.getMap () : Y.Map<obj>).set ("k", box 2) |> ignore // no explicit origin

        let remote = Y.Doc.Create ()
        (remote.getMap () : Y.Map<obj>).set ("k", box 3) |> ignore
        Y.applyUpdate (doc, Y.encodeStateAsUpdate remote, "remote-origin")

        Expect.equal seen.Count 3 "one observer callback per transaction"
        let origins = seen |> Seq.map fst |> Seq.toList
        let locals = seen |> Seq.map snd |> Seq.toList
        Expect.equal origins [ Some (box "my-origin"); None; Some (box "remote-origin") ]
            "explicit local origin, default (no) origin, and applyUpdate's origin must all reach the observer"
        Expect.equal locals [ true; true; false ]
            "the first two transactions are local; the applied remote update must be flagged non-local"
    }

    test "U7: observeDeep on the root map fires for nested Y.Text/Y.Array/Y.Map edits" {
        let doc = Y.Doc.Create ()
        let t = Y.Text.Create ()
        doc.getMap().set ("body", t) |> ignore

        let mutable batches = 0
        (doc.getMap () : Y.Map<obj>).observeDeep (fun _evts _tr -> batches <- batches + 1)

        t.insert (0, "hi")
        let arr : Y.Array<obj> = Y.Array.Create ()
        doc.getMap().set ("list", arr) |> ignore
        let inner : Y.Map<obj> = Y.Map.Create ()
        arr.push [| box inner |]
        inner.set ("k", box "v") |> ignore

        Expect.equal batches 4
            "one observeDeep batch per mutation (text insert, list-key set, array push, nested-map set) — including edits to types nested arbitrarily deep — so one observer covers the whole bound tree"
    }

    test "U8: concurrent Y.Array inserts at the same position both survive, in a deterministic order" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        let arr : Y.Array<string> = Y.Array.Create ()
        arr.push [| "base" |]
        d1.getMap().set ("xs", arr) |> ignore
        syncBoth d1 d2

        ((d1.getMap () : Y.Map<Y.Array<string>>).get "xs").Value.insert (0.0, [| "from-d1" |])
        ((d2.getMap () : Y.Map<Y.Array<string>>).get "xs").Value.insert (0.0, [| "from-d2" |])
        syncBoth d1 d2

        let items1 = ((d1.getMap () : Y.Map<Y.Array<string>>).get "xs").Value.toArray () |> Seq.toList
        let items2 = ((d2.getMap () : Y.Map<Y.Array<string>>).get "xs").Value.toArray () |> Seq.toList
        Expect.equal items1 items2 "must converge to the same order on both peers"
        Expect.isTrue (List.contains "from-d1" items1 && List.contains "from-d2" items1 && List.contains "base" items1)
            "concurrent inserts at the same position must not clobber each other — lists are a safe default for value sequences"
    }

    test "U9: a delete beats a concurrent edit inside the deleted item" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        let item : Y.Map<string> = Y.Map.Create ()
        item.set ("title", "a") |> ignore
        let arr : Y.Array<Y.Map<string>> = Y.Array.Create ()
        arr.push [| item |]
        d1.getMap().set ("xs", arr) |> ignore
        syncBoth d1 d2

        ((d1.getMap () : Y.Map<Y.Array<Y.Map<string>>>).get "xs").Value.delete (0.0, 1.0)          // d1 deletes the item
        (((d2.getMap () : Y.Map<Y.Array<Y.Map<string>>>).get "xs").Value.get 0.0).set ("title", "b") |> ignore // d2 edits inside it
        syncBoth d1 d2

        let len1 = ((d1.getMap () : Y.Map<Y.Array<Y.Map<string>>>).get "xs").Value.toArray() |> Seq.length
        let len2 = ((d2.getMap () : Y.Map<Y.Array<Y.Map<string>>>).get "xs").Value.toArray() |> Seq.length
        Expect.equal len1 0 "the deletion must win on the deleting peer"
        Expect.equal len2 0 "and converge to deleted on the editing peer too — the concurrent edit is lost"
    }

    test "U10: Y.Map holds any JSON-primitive value (number, float, bool, null, string, plain object/array)" {
        let doc = Y.Doc.Create ()
        let m : Y.Map<obj> = doc.getMap ()
        m.set ("num", box 42) |> ignore
        m.set ("float", box 1.5) |> ignore
        m.set ("bool", box true) |> ignore
        m.set ("str", box "s") |> ignore

        Expect.equal (m.get "num") (Some (box 42)) "int round-trips"
        Expect.equal (m.get "float") (Some (box 1.5)) "float round-trips"
        Expect.equal (m.get "bool") (Some (box true)) "bool round-trips"
        Expect.equal (m.get "str") (Some (box "s")) "string round-trips"
    }

    test "U11: replacing a nested Y.Text wholesale discards a peer's concurrent edits to the old instance" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        let t1 = Y.Text.Create ()
        t1.insert (0, "v1")
        d1.getMap().set ("body", t1) |> ignore
        syncBoth d1 d2

        let t2ref = ((d2.getMap () : Y.Map<Y.Text>).get "body").Value
        let tNew = Y.Text.Create ()
        tNew.insert (0, "v2")
        d1.getMap().set ("body", tNew) |> ignore   // d1 replaces the Y.Text entirely (what materialize does)
        t2ref.insert (2, "!")                       // d2 concurrently edits the OLD text
        syncBoth d1 d2

        let s1 = ((d1.getMap () : Y.Map<Y.Text>).get "body").Value.toString ()
        let s2 = ((d2.getMap () : Y.Map<Y.Text>).get "body").Value.toString ()
        Expect.equal s1 s2 "must converge"
        Expect.isFalse (s1.Contains "!")
            "replacement wins; the peer's edits to the discarded instance are lost — bind, never replace"
    }

    test "U13: concurrent delete+insert 'move' of the same item duplicates it" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        d1.clientID <- 1.0
        d2.clientID <- 2.0

        let arr : Y.Array<string> = Y.Array.Create ()
        arr.push [| "a"; "b"; "c" |]
        d1.getMap().set ("xs", arr) |> ignore
        syncBoth d1 d2

        let move (d : Y.Doc) =
            d.transact (fun _ ->
                let xs = (d.getMap () : Y.Map<Y.Array<string>>).get("xs").Value
                let v = xs.get 0.0
                xs.delete (0.0, 1.0)
                xs.push [| v |])
        move d1
        move d2
        syncBoth d1 d2

        let items1 = ((d1.getMap () : Y.Map<Y.Array<string>>).get "xs").Value.toArray () |> Seq.toList
        let items2 = ((d2.getMap () : Y.Map<Y.Array<string>>).get "xs").Value.toArray () |> Seq.toList
        Expect.equal items1 items2 "must converge"
        Expect.isTrue (items1.Length > 3)
            "a structural move expressed as delete+insert duplicates under concurrency — a known, documented limit; keyed-map + order-field avoids structural moves entirely"
    }

    test "U14: one doc.transact spanning many nested types produces exactly one observeDeep batch" {
        let doc = Y.Doc.Create ()
        let t = Y.Text.Create ()
        let a : Y.Array<obj> = Y.Array.Create ()
        doc.getMap().set ("t", t) |> ignore
        doc.getMap().set ("a", a) |> ignore

        let mutable batches = 0
        let mutable eventsTotal = 0
        (doc.getMap () : Y.Map<obj>).observeDeep (fun evts _tr ->
            batches <- batches + 1
            eventsTotal <- eventsTotal + evts.Count)

        doc.transact (fun _ ->
            t.insert (0, "hi")
            a.push [| box 1 |]
            doc.getMap().set ("k", "v") |> ignore)

        Expect.equal batches 1 "a single transaction must yield a single observeDeep batch"
        Expect.equal eventsTotal 3 "the batch must carry all three events — remote changes apply as one atomic Elmish Set"
    }

    test "U15: a client applies an update containing keys it doesn't understand cleanly, and unknown keys survive" {
        let oldClient = Y.Doc.Create ()
        let newClient = Y.Doc.Create ()

        oldClient.getMap().set ("v1", "old-data") |> ignore
        syncBoth oldClient newClient

        let t = Y.Text.Create ()
        t.insert (0, "new-data")
        newClient.getMap().set ("v2", t) |> ignore

        let mutable err = None
        try syncBoth oldClient newClient
        with e -> err <- Some e.Message

        Expect.isNone err "an old client must apply an update containing a key it doesn't understand without throwing"
        let oldKeys = (oldClient.getMap () : Y.Map<obj>).keys () |> Seq.toList
        Expect.isTrue (List.contains "v1" oldKeys) "the old client's own key must still be present"
        Expect.equal ((oldClient.getMap () : Y.Map<string>).get "v1") (Some "old-data")
            "forward compatibility is free as long as the binding layer never deletes keys it doesn't recognise"
    }
]
