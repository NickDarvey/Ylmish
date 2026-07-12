module Ylmish.Y.Assumptions

// Plan 0002, Step 1 — pin the Yjs semantics the redesign depends on.
//
// These are characterization tests: no Ylmish production code is under test.
// Each test ports one runnable experiment from doc/plans/0002-assumptions/
// (experiments.mjs / experiments2.mjs) and pins the observed behaviour, so a
// Yjs upgrade that changes semantics fails CI loudly. Test names carry the
// assumption ids (U1..U15) from the plan's "Validated assumptions" table;
// each comment states the design consequence that rests on the result.

open Yjs

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// Unambiguous aliases — `Y` is overloaded between the Yjs module and the Yjs
// class, so spell the raw Yjs types out for annotations and casts.
type private Doc = Yjs.Utils.Doc.Doc
type private YText = Yjs.Types.YText.YText
type private YMap<'a> = Yjs.Types.YMap.YMap<'a>
type private YArray<'a> = Yjs.Types.YArray.YArray<'a>

/// Push all of `src`'s state into `dst` (one direction of a network round-trip).
let private sync (src : Doc) (dst : Doc) =
    Y.applyUpdate (dst, Y.encodeStateAsUpdate src)

/// Full exchange, both directions.
let private exchange (a : Doc) (b : Doc) =
    sync a b
    sync b a

/// A doc with a deterministic clientID (Yjs breaks LWW ties by clientID, U4).
let private docWithClient (id : float) : Doc =
    let d = Y.Doc.Create ()
    d.clientID <- id
    d

let private root (d : Doc) : YMap<obj> = d.getMap ()

let tests = testList "Y.Assumptions" [

    // U1 — argless doc.getMap() returns the root map named "", same instance
    // every call. Consequence: root binding is stable; consumers need not name
    // a root map.
    testList "U1 root map identity" [
        test "getMap() is idempotent and equals getMap(\"\")" {
            let doc = Y.Doc.Create ()
            let m1 : YMap<obj> = doc.getMap ()
            let m2 : YMap<obj> = doc.getMap ()
            let m3 : YMap<obj> = doc.getMap ""
            Expect.isTrue (System.Object.ReferenceEquals (m1, m2))
                "argless getMap must return the same instance on repeated calls"
            Expect.isTrue (System.Object.ReferenceEquals (m1, m3))
                "argless getMap must be the root map named the empty string"
        }
    ]

    // U2a — the nested first-create race. Two clients each create a fresh
    // nested type under the same map key, then sync: one client's ENTIRE
    // subtree wins; the other's data is silently discarded. Consequence: never
    // create containers eagerly at init; the residual race is the documented
    // app-design limitation (offline-creatable entities need unique keys).
    testList "U2a nested first-create race" [
        test "concurrent fresh nested arrays under one key: one subtree wins wholesale" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            let a1 : YArray<string> = Y.Array.Create ()
            a1.push [| "from-d1" |]
            (root d1).set ("todos", box a1) |> ignore
            let a2 : YArray<string> = Y.Array.Create ()
            a2.push [| "from-d2" |]
            (root d2).set ("todos", box a2) |> ignore

            exchange d1 d2

            let read (d : Doc) =
                ((root d).get "todos" |> Option.get :?> YArray<string>).toArray () |> List.ofSeq
            let r1 = read d1
            let r2 = read d2
            Expect.equal r1 r2 "docs converge (CRDT guarantees agreement)..."
            Expect.equal r1 [ "from-d2" ]
                "...but only ONE subtree survives — the higher clientID's (the same \
                 tiebreak as U4) — and the loser's items are LOST"
        }
    ]

    // U2b — the same race with ROOT-level types merges by name: both sides'
    // items survive. Consequence: root types are safe to get-or-create.
    testList "U2b root-level create race" [
        test "concurrent root arrays with the same name merge, both items survive" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            (d1.getArray "todos" : YArray<string>).push [| "from-d1" |]
            (d2.getArray "todos" : YArray<string>).push [| "from-d2" |]

            exchange d1 d2

            let read (d : Doc) = (d.getArray "todos" : YArray<string>).toArray () |> List.ofSeq
            Expect.equal (read d1) (read d2) "docs converge"
            Expect.equal (read d1 |> List.sort) [ "from-d1"; "from-d2" ]
                "root types with the same name merge by name — no wholesale loss"
        }
    ]

    // U3 — concurrent edits to ONE shared nested Y.Text interleave and
    // converge. This is the whole point of the redesign: text must bind to an
    // existing instance, created once.
    testList "U3 shared nested Y.Text" [
        test "concurrent edits interleave and converge" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            let t = Y.Text.Create "hello"
            (root d1).set ("body", box t) |> ignore
            exchange d1 d2 // both now share the same text

            let text (d : Doc) = (root d).get "body" |> Option.get :?> YText
            (text d1).insert (5, " world")
            (text d2).insert (0, "oh, ")
            exchange d1 d2

            let s1 = (text d1).toString ()
            let s2 = (text d2).toString ()
            Expect.equal s1 s2 "shared nested Y.Text converges"
            Expect.equal s1 "oh, hello world"
                "both peers' insertions survive and interleave (observed: 'oh, hello world')"
        }
    ]

    // U3b — a string field stored as a plain map value resolves concurrent
    // sets by LWW: issue #83's failure mode. Consequence: plain values are
    // honest LWW registers; the codec must let consumers choose text vs
    // register per field.
    testList "U3b plain-string map value" [
        test "concurrent set clobbers — LWW register, not a merge" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            (root d1).set ("body", box "hello") |> ignore
            exchange d1 d2

            (root d1).set ("body", box "hello world") |> ignore
            (root d2).set ("body", box "oh, hello") |> ignore
            exchange d1 d2

            let v (d : Doc) = (root d).get "body" |> Option.get |> unbox<string>
            Expect.equal (v d1) (v d2) "docs converge"
            Expect.equal (v d1) "oh, hello"
                "exactly one write survives — the higher clientID's (see U4) — the other is clobbered, not merged"
        }
    ]

    // U4 — what decides the LWW winner: deterministic and order-independent,
    // higher clientID wins among concurrent sets. NOT wall-clock. Consequence:
    // document honestly — "last writer" is an arbitrary-but-convergent tiebreak.
    testList "U4 LWW tiebreak" [
        test "higher clientID wins, independent of sync order" {
            for (c1, c2) in [ 1.0, 2.0; 2.0, 1.0; 100.0, 5.0 ] do
                let d1 = docWithClient c1
                let d2 = docWithClient c2
                (root d1).set ("k", box $"v-from-{c1}") |> ignore
                (root d2).set ("k", box $"v-from-{c2}") |> ignore
                exchange d1 d2
                let expected = $"v-from-{max c1 c2}"
                let v (d : Doc) = (root d).get "k" |> Option.get |> unbox<string>
                Expect.equal (v d1) expected $"clientIDs ({c1}, {c2}): d1 must hold the higher client's write"
                Expect.equal (v d2) expected $"clientIDs ({c1}, {c2}): d2 must agree"
        }
    ]

    // U5b — re-setting an already-integrated Y type under another key does NOT
    // throw at the call site; the doc corrupts and a later sync to a peer
    // throws. Consequence: the public API must make re-parenting impossible by
    // construction — you cannot guard it with try/catch at the write site.
    // (Yjs also logs an internal TypeError to the console here; that noise is
    // expected and harmless.)
    testList "U5b re-parenting an integrated type" [
        test "no throw at the call site, but the doc becomes unsyncable" {
            let d1 = Y.Doc.Create ()
            let t = Y.Text.Create "x"
            (root d1).set ("a", box t) |> ignore

            let mutable setThrew = false
            try (root d1).set ("b", box t) |> ignore
            with _ -> setThrew <- true
            Expect.isFalse setThrew
                "the corrupting re-set does NOT throw at the call site — no local guard is possible"

            let d2 = Y.Doc.Create ()
            let mutable syncThrew = false
            try sync d1 d2
            with _ -> syncThrew <- true
            Expect.isTrue syncThrew
                "a later applyUpdate on a peer throws — the corruption surfaces far from its cause"
        }
    ]

    // U6 — transaction origins propagate to observers: doc.transact's origin
    // and applyUpdate's origin both reach observers, with a `local` flag.
    // Consequence: echo suppression via a per-binding origin token replaces
    // boolean reentrancy flags.
    testList "U6 transaction origins" [
        test "transact origin, default origin and applyUpdate origin all reach observers" {
            let d1 = Y.Doc.Create ()
            let seen = ResizeArray<string * bool> ()
            (root d1).observe (fun _e tr ->
                let origin = tr.origin |> Option.map string |> Option.defaultValue "null"
                seen.Add (origin, tr.local))

            d1.transact ((fun _ -> (root d1).set ("k", box 1) |> ignore), box "my-origin")
            (root d1).set ("k", box 2) |> ignore
            let d2 = Y.Doc.Create ()
            (root d2).set ("k", box 3) |> ignore
            Y.applyUpdate (d1, Y.encodeStateAsUpdate d2, box "remote-origin")

            Expect.equal (List.ofSeq seen)
                [ "my-origin", true; "null", true; "remote-origin", false ]
                "origins propagate verbatim, applyUpdate carries its own origin, and `local` distinguishes remote applies"
        }
    ]

    // U7 — observeDeep on the root sees nested edits (typed events reaching
    // the root from arbitrarily deep). Consequence: ONE observer covers the
    // whole bound tree, including custom elements (L4, L6).
    testList "U7 observeDeep coverage" [
        test "one deep observer on the root sees nested text, array and map edits" {
            let d1 = Y.Doc.Create ()
            let t = Y.Text.Create ""
            (root d1).set ("body", box t) |> ignore
            let mutable events = 0
            let mutable textTargeted = false
            (root d1).observeDeep (fun evts _tr ->
                events <- events + evts.Count
                for e in evts do
                    if System.Object.ReferenceEquals (box e.target, box t) then textTargeted <- true)

            t.insert (0, "hi")                               // nested text edit
            let arr : YArray<obj> = Y.Array.Create ()
            (root d1).set ("list", box arr) |> ignore        // root map edit
            let inner : YMap<obj> = Y.Map.Create ()
            arr.push [| box inner |]                         // nested array edit
            inner.set ("inner", box "v") |> ignore           // doubly-nested map edit

            Expect.equal events 4
                "every edit — root-level and nested — surfaces through the single deep observer"
            // The Fable binding doesn't expose YEvent.path, so pin routing via the
            // event's TARGET instead: the nested text's event carries the Y.Text
            // instance itself. (Step 6's decode direction will want `path` bound.)
            Expect.isTrue textTargeted
                "the nested text edit's deep event targets the Y.Text instance — the root observer can route without paths"
        }
    ]

    // U8 — concurrent Y.Array inserts at the same position: both survive, in a
    // deterministic order. Consequence: lists merge; safe default for value
    // sequences.
    testList "U8 concurrent array inserts" [
        test "both inserts at position 0 survive with deterministic order" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            let arr : YArray<string> = Y.Array.Create ()
            arr.push [| "base" |]
            (root d1).set ("xs", box arr) |> ignore
            exchange d1 d2

            let xs (d : Doc) = (root d).get "xs" |> Option.get :?> YArray<string>
            (xs d1).insert (0, [| "from-d1" |])
            (xs d2).insert (0, [| "from-d2" |])
            exchange d1 d2

            let r1 = (xs d1).toArray () |> List.ofSeq
            let r2 = (xs d2).toArray () |> List.ofSeq
            Expect.equal r1 r2 "docs converge"
            Expect.equal r1 [ "from-d1"; "from-d2"; "base" ]
                "both concurrent inserts survive, in the deterministic converged order \
                 (observed: lower clientID's insert first) — the order IS the pin; \
                 a flipped insert tiebreak must fail here"
        }
    ]

    // U9 — deleting an item vs a concurrent edit inside it: delete wins, the
    // edit is lost. Documented limit.
    testList "U9 delete vs edit-inside" [
        test "concurrent delete of a container beats an edit inside it" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            let item : YMap<obj> = Y.Map.Create ()
            item.set ("title", box "a") |> ignore
            let arr : YArray<obj> = Y.Array.Create ()
            arr.push [| box item |]
            (root d1).set ("xs", box arr) |> ignore
            exchange d1 d2

            let xs (d : Doc) = (root d).get "xs" |> Option.get :?> YArray<obj>
            (xs d1).delete (0, 1)                                        // d1 deletes the item
            ((xs d2).get 0 :?> YMap<obj>).set ("title", box "b") |> ignore // d2 edits inside it
            exchange d1 d2

            Expect.equal ((xs d1).toArray().Count) 0 "delete wins on d1"
            Expect.equal ((xs d2).toArray().Count) 0 "delete wins on d2 — the concurrent edit is lost"
        }
    ]

    // U10 — Y.Map holds any JSON value, not just strings. Consequence: the v1
    // codec's Element<string> restriction is unnecessary; v2 elements carry
    // typed primitives. (The .mjs run also pins plain objects and undefined;
    // here we pin the payloads the v2 Value sub-language needs, plus null.)
    testList "U10 typed primitives in Y.Map" [
        test "numbers, floats, bools, strings, arrays and null are stored" {
            let d = Y.Doc.Create ()
            let m = root d
            m.set ("num", box 42) |> ignore
            m.set ("float", box 1.5) |> ignore
            m.set ("bool", box true) |> ignore
            m.set ("str", box "s") |> ignore
            // NB: an F# int[] compiles to a JS Int32Array under Fable, which Yjs
            // rejects ("Unexpected content type") — a plain JS array is required.
            m.set ("arr", box [| box 1; box 2 |]) |> ignore
            m.set ("nul", null) |> ignore

            Expect.equal (m.get "num" |> Option.get |> unbox<int>) 42 "int survives"
            Expect.equal (m.get "float" |> Option.get |> unbox<float>) 1.5 "float survives"
            Expect.equal (m.get "bool" |> Option.get |> unbox<bool>) true "bool survives"
            Expect.equal (m.get "str" |> Option.get |> unbox<string>) "s" "string survives"
            Expect.equal (m.get "arr" |> Option.get |> unbox<obj[]> |> Array.map unbox<int> |> List.ofArray) [ 1; 2 ]
                "plain arrays survive"
            // A stored JS null is a real Y.Map value, but the Fable binding
            // collapses `get` to 'T option, so null reads back as None —
            // indistinguishable from a missing key. Pin presence via `has`;
            // the codec's option semantics must NOT rely on null through `get`.
            Expect.isTrue (m.has "nul")
                "null is stored as a real value (readable only via has under Fable)"
        }
    ]

    // U11 — replacing a nested Y.Text with a fresh instance while a peer edits
    // the old one: the replacement wins and the peer's edits are lost. This is
    // precisely why materialize-per-update can never merge. Bind, never replace.
    testList "U11 replace vs concurrent edit" [
        test "replacing a nested Y.Text discards the peer's concurrent edits" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            let t1 = Y.Text.Create "v1"
            (root d1).set ("body", box t1) |> ignore
            exchange d1 d2

            let oldRef = (root d2).get "body" |> Option.get :?> YText
            // d1 replaces the Y.Text wholesale (exactly what materialize does today)
            (root d1).set ("body", box (Y.Text.Create "v2")) |> ignore
            // d2 concurrently edits the OLD text
            oldRef.insert (2, "!")
            exchange d1 d2

            let read (d : Doc) = ((root d).get "body" |> Option.get :?> YText).toString ()
            Expect.equal (read d1) "v2" "the replacement wins on d1"
            Expect.equal (read d2) "v2" "the replacement wins on d2 — the '!' edit is lost"
        }
    ]

    // U13 — a concurrent "move" (delete+insert) of the same list item
    // duplicates it. Known Yjs v13 limit; the keyed-map + order-field pattern
    // avoids structural moves entirely (L9).
    testList "U13 concurrent structural move" [
        test "both peers move the same item: it is duplicated" {
            let d1 = docWithClient 1.0
            let d2 = docWithClient 2.0
            let arr : YArray<string> = Y.Array.Create ()
            arr.push [| "a"; "b"; "c" |]
            (root d1).set ("xs", box arr) |> ignore
            exchange d1 d2

            let move (d : Doc) =
                let xs = (root d).get "xs" |> Option.get :?> YArray<string>
                d.transact (fun _ ->
                    let v = xs.get 0
                    xs.delete (0, 1)
                    xs.push [| v |])
            move d1
            move d2
            exchange d1 d2

            let read (d : Doc) =
                ((root d).get "xs" |> Option.get :?> YArray<string>).toArray () |> List.ofSeq
            Expect.equal (read d1) (read d2) "docs converge"
            Expect.equal (read d1) [ "b"; "c"; "a"; "a" ]
                "the moved item is DUPLICATED — delete+insert is not a move"
        }
    ]

    // U14 — one doc.transact spanning many nested types yields ONE observeDeep
    // batch containing all events. Consequence: remote changes apply as a
    // single Elmish Set per transaction — atomic model updates.
    testList "U14 transaction batching" [
        test "one transact over text + array + map = one deep-observer batch of 3 events" {
            let d = Y.Doc.Create ()
            let t = Y.Text.Create ""
            let a : YArray<obj> = Y.Array.Create ()
            (root d).set ("t", box t) |> ignore
            (root d).set ("a", box a) |> ignore

            let mutable batches = 0
            let mutable events = 0
            (root d).observeDeep (fun evts _tr ->
                batches <- batches + 1
                events <- events + evts.Count)

            d.transact (fun _ ->
                t.insert (0, "hi")
                a.push [| box 1 |]
                (root d).set ("k", box "v") |> ignore)

            Expect.equal batches 1 "all changes inside one transact arrive as ONE batch"
            Expect.equal events 3 "the batch carries one event per changed type"
        }
    ]

    // U15 — updates containing keys a client doesn't understand apply cleanly;
    // unknown keys are preserved and readable. Consequence: forward
    // compatibility is free if the runtime stops deleting unknown keys —
    // the foundation for the dual-key migration recipe.
    testList "U15 unknown keys" [
        test "a client applies and preserves keys it does not understand" {
            let oldClient = Y.Doc.Create ()
            let newClient = Y.Doc.Create ()
            (root oldClient).set ("v1", box "old-data") |> ignore
            exchange oldClient newClient

            let t = Y.Text.Create "new-data"
            (root newClient).set ("v2", box t) |> ignore
            let mutable err = false
            try exchange oldClient newClient
            with _ -> err <- true

            Expect.isFalse err "syncing a doc with unknown keys must not throw"
            Expect.isTrue ((root oldClient).has "v2")
                "the unknown key is preserved on the old client"
            Expect.equal ((root oldClient).get "v1" |> Option.get |> unbox<string>) "old-data"
                "the old client's own data is untouched"
        }
    ]
]
