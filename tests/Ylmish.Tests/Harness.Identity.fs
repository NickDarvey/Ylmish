module Ylmish.HarnessIdentity

// =============================================================================
// Plan 0006 — Step 3: identity across model versions, and the decisive
// move-preserves-nested-state proof.
//
// Step 1 killed positional `Index` as an identity source; Step 2 showed a value
// array has no native move, so a move is delete+insert that ORPHANS any per-item
// nested CRDT state. Step 3 pins the identity decision and proves it fixes moves:
//
//   DECISION: an item's identity for diffing/naming is an explicit, stable
//   per-item id (a guid the model carries), and *nested* collaborative state is
//   named by that id — exactly 0004's `Scheme.byKey` / `PathSegment.KeyById`. NOT
//   positional Index (mutable, Step 1), NOT a fractional order key (that is for
//   *ordering*, and changes on every reorder — 0004's immutable-id-vs-mutable-
//   order split). Because a nested root's NAME is the item id, a reorder never
//   touches it, so the item's nested state survives the move and still merges.
//
// The two tests below are the proof and its counter-example: id-keyed nested
// text survives a concurrent reorder+edit; position-keyed nested text is
// corrupted by the very same schedule. This is the front-runner representation
// (keyed map of items + id-named nested state) from Step 2, shown end-to-end.
// =============================================================================

open Yjs

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let private sync (from: Y.Doc) (into: Y.Doc) =
    Y.applyUpdate (into, Y.encodeStateAsUpdate from, box "remote")

let private order (d: Y.Doc) =
    (d.getArray "items").toArray () |> Seq.map string |> Seq.toList

let tests = testList "Identity (0006 Step 3)" [

    // The decision, proven: nested state named by ITEM ID survives a move and
    // merges a concurrent edit onto the right item.
    test "id-keyed nested text: concurrent reorder + edit merges onto the right item" {
        let d0 = Y.Doc.Create ()
        let d1 = Y.Doc.Create ()
        // Base: items [a;b], each with an id-named text root.
        let a0 = d0.getArray "items"
        a0.push [| box "a" |]
        a0.push [| box "b" |]
        (d0.getText "text/a").insert (0, "A")
        (d0.getText "text/b").insert (0, "B")
        sync d0 d1

        // Concurrently: peer 0 moves 'a' to the end (delete+insert in the id array);
        // peer 1 edits 'a's text. Different roots, keyed by id — they must compose.
        let a0 = d0.getArray "items"
        a0.delete (0.0, 1.0)
        a0.insert (1.0, [| box "a" |])
        (d1.getText "text/a").insert (1, " edited")

        sync d0 d1
        sync d1 d0

        Expect.equal (order d0) (order d1) "order converges across peers"
        Expect.equal (order d0) [ "b"; "a" ] "the move is applied"
        Expect.equal ((d0.getText "text/a").toString ()) "A edited"
            "a's concurrent edit merged onto item 'a' (named by id) despite the reorder"
        Expect.equal ((d1.getText "text/a").toString ()) "A edited" "and converges on the other peer"
        Expect.equal ((d0.getText "text/b").toString ()) "B" "b's nested state is untouched by the move"
    }

    // The counter-example that justifies the decision: the SAME schedule with
    // nested state keyed by POSITION corrupts — the edit sticks to the slot, not
    // the item, so after the reorder it mislabels the wrong row.
    test "position-keyed nested text: the same reorder+edit corrupts (why identity must be the id)" {
        let d0 = Y.Doc.Create ()
        let d1 = Y.Doc.Create ()
        let a0 = d0.getArray "items"
        a0.push [| box "a" |]
        a0.push [| box "b" |]
        // Nested text named by POSITION: text/0 belongs to slot 0, text/1 to slot 1.
        (d0.getText "text/0").insert (0, "A")  // slot 0 currently holds 'a'
        (d0.getText "text/1").insert (0, "B")  // slot 1 currently holds 'b'
        sync d0 d1

        // peer 0 reorders to [b;a]; peer 1 edits slot 0's text (meaning to edit 'a').
        let a0 = d0.getArray "items"
        a0.delete (0.0, 1.0)
        a0.insert (1.0, [| box "a" |])
        (d1.getText "text/0").insert (1, " edited")

        sync d0 d1
        sync d1 d0

        Expect.equal (order d0) [ "b"; "a" ] "order converges to the moved layout"
        // slot 0 now holds 'b', but text/0 carries the edit meant for 'a': corruption.
        Expect.equal ((d0.getText "text/0").toString ()) "A edited"
            "the edit stuck to SLOT 0 — but slot 0 is now 'b', so 'b's row shows 'a's edited text"
        Expect.equal (List.head (order d0)) "b"
            "position-keying mislabels the row after a move: this is why identity must be the item id"
    }
]
