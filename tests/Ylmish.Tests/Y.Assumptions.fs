module Ylmish.Y.Assumptions

// Plan 0002, Step 0 — spike the Yjs assumptions the `connect` design depends on.
//
// These are throwaway-but-keep tests that pin raw Yjs behaviour (no Ylmish
// production code under test) so the design rests on observed facts rather than
// docs. Each test states the assumption (A1..A6 in doc/plans/0002), what it
// asserts, and what a failure would mean for the plan.
//
// The headline result is A3: Yjs resolves concurrent writes to the same Y.Map
// key as a register ("only one change will prevail"), so two peers that each
// create a *fresh* nested shared type at the same key clobber — one survives and
// the other's edits are lost. That is why Step 5 uses flattened top-level names
// (relying on A1) instead of nested get-or-create.

open Yjs

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// Unambiguous aliases — `Y` is overloaded across the Yjs module, the Yjs class,
// and Ylmish.Y, so spell the raw Yjs types out for annotations and casts.
type private Doc = Yjs.Utils.Doc.Doc
type private YText = Yjs.Types.YText.YText
type private YMap<'a> = Yjs.Types.YMap.YMap<'a>

/// Push all of `src`'s state into `dst` (one direction of a network round-trip).
let private sync (src : Doc) (dst : Doc) =
    Y.applyUpdate (dst, Y.encodeStateAsUpdate src)

let tests = testList "Y.Assumptions" [

    // A1 — top-level get-or-create is idempotent and convergent.
    // RELIED ON by every root in `connect`. If false, connect must create roots
    // once and cache them rather than re-fetch.
    testList "A1 root get-or-create" [
        test "same name returns the same instance (idempotent)" {
            let doc = Y.Doc.Create ()
            let a = doc.getText "shared"
            let b = doc.getText "shared"
            Expect.isTrue (System.Object.ReferenceEquals(a, b))
                "doc.getText(name) must return the same instance on repeated calls"
        }

        test "two peers naming the same root text converge AND both edits survive" {
            // This is also the Step 4 seed: the *safe* collaborative pattern is a
            // shared top-level type, where concurrent edits interleave.
            let d1 = Y.Doc.Create ()
            let d2 = Y.Doc.Create ()
            (d1.getText "body").insert (0, "AAA")
            (d2.getText "body").insert (0, "BBB")

            // exchange both directions
            sync d1 d2
            sync d2 d1

            let s1 = (d1.getText "body").toString ()
            let s2 = (d2.getText "body").toString ()
            Expect.equal s1 s2 "shared-root docs must converge"
            Expect.isTrue (s1.Contains "AAA" && s1.Contains "BBB")
                "both peers' insertions must survive on a shared top-level Y.Text (interleaved, not clobbered)"
        }
    ]

    // A2 — defining one root name under two different types is rejected loudly.
    // We WANT a throw (loud failure beats silent drift). connect records a kind
    // per root name; this confirms Yjs backs that up.
    testList "A2 root type conflict" [
        test "re-fetching a root under a different type throws" {
            let doc = Y.Doc.Create ()
            let _ = doc.getText "x"
            let mutable threw = false
            try
                (doc.getMap "x" : YMap<obj>) |> ignore
            with _ ->
                threw <- true
            Expect.isTrue threw
                "getMap on a name already defined as Y.Text must throw (loud failure is the safe outcome)"
        }
    ]

    // A3 — THE load-bearing result. Nested get-or-create does NOT converge: two
    // peers each creating a fresh nested Y.Text at the same key clobber.
    // This test PROVES the clobber (one side's text is lost), which is why the
    // plan takes the flattened-top-level-name path in Step 5.
    testList "A3 nested concurrent create (expected to CLOBBER)" [
        test "two peers create the same nested text key independently — one side's edits are lost" {
            let d1 = Y.Doc.Create ()
            let d2 = Y.Doc.Create ()

            // Naive nested get-or-create, performed independently before any sync:
            // both peers find "body" absent and each create a brand-new Y.Text.
            let r1 : YMap<obj> = d1.getMap "root"
            r1.set ("body", box (Y.Text.Create "")) |> ignore
            ((r1.get "body").Value :?> YText).insert (0, "AAA")

            let r2 : YMap<obj> = d2.getMap "root"
            r2.set ("body", box (Y.Text.Create "")) |> ignore
            ((r2.get "body").Value :?> YText).insert (0, "BBB")

            // exchange both directions
            sync d1 d2
            sync d2 d1

            let read (d : Doc) =
                (((d.getMap "root" : YMap<obj>).get "body").Value :?> YText).toString ()
            let s1 = read d1
            let s2 = read d2

            Expect.equal s1 s2 "docs still converge (CRDT guarantees agreement)..."
            Expect.isFalse (s1.Contains "AAA" && s1.Contains "BBB")
                "...but A3 is FALSE: nested concurrent create clobbers — both texts cannot survive"
            Expect.isTrue (s1 = "AAA" || s1 = "BBB")
                "exactly one peer's freshly-created Y.Text survives; the other's edits are discarded"
        }
    ]

    // A4 — structural deletes happen at the PARENT map level, so an attach
    // lifecycle must subscribe to the parent, not just the child type.
    testList "A4 structural delete propagates via the parent" [
        test "remote delete of a key holding a nested type removes it on the peer" {
            let d1 = Y.Doc.Create ()
            let d2 = Y.Doc.Create ()
            let r1 : YMap<obj> = d1.getMap "root"
            r1.set ("body", box (Y.Text.Create "hi")) |> ignore
            sync d1 d2
            Expect.isTrue ((d2.getMap "root" : YMap<obj>).has "body")
                "peer should have the nested key after the first sync"

            r1.delete "body"
            sync d1 d2
            Expect.isFalse ((d2.getMap "root" : YMap<obj>).has "body")
                "remote delete of the parent key must propagate (attach must watch the parent, not only the child)"
        }
    ]

    // A6 — a Y.Text created standalone and then set into a parent keeps its
    // content and is integrated in place; subsequent edits must use that handle.
    testList "A6 create-then-parent keeps content and identity" [
        test "set integrates the text in place, content survives, edits work" {
            let doc = Y.Doc.Create ()
            let standalone = Y.Text.Create "hello"
            let root : YMap<obj> = doc.getMap "root"
            root.set ("k", box standalone) |> ignore

            let integrated = (root.get "k").Value :?> YText
            Expect.equal (integrated.toString ()) "hello" "content must survive parenting"
            Expect.isSome integrated.doc "integrated text must be attached to the parent doc"

            integrated.insert (5, "!")
            Expect.equal (integrated.toString ()) "hello!" "must be editable after parenting"
            Expect.equal (standalone.toString ()) "hello!"
                "the pre-insert handle is integrated in place — it IS the same type as the one read back"
        }
    ]
]
