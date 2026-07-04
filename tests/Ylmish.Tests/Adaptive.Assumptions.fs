module Ylmish.Adaptive.Assumptions

// =============================================================================
// Plan 0002, Step 1 — pin FSharp.Data.Adaptive's reassignment deltas (A1/L1 in
// doc/plans/0002-ylmish-redesign.md). Adapted from the `claude/github-issues-
// visibility-8k12g3` branch's Adaptive.Spike.fs (its plan 0006 Step 1) — the
// numbers and characterization carry over unchanged; the design consequence
// here is different: `Encode.list` is restricted to value sequences by
// construction, and identity lives in `HashMap` keys (`Encode.map`).
//
// What the generated code does (Example.g.fs): an `IndexList<'T>` field becomes a
// plain `clist`, and `Update` does `_PropD_.Value <- value.PropD` — it sets the
// WHOLE new IndexList. So the delta is whatever `ChangeableIndexList.Value <- new`
// computes, which compares the two IndexLists by their `Index` keys. The whole
// question therefore reduces to: does the *new* immutable IndexList preserve the
// `Index` identity of unchanged elements?
//
//   - If the app derives the next list by STRUCTURAL ops on the previous one
//     (`.Add` / `.Remove` / `.InsertAt`), Indices are preserved → minimal delta.
//   - If the app REBUILDS the list from scratch (`IndexList.ofList [...]`, the
//     idiomatic `{ model with Items = recompute () }`), every Index is fresh →
//     clear-all + add-all. Adaptive buys you nothing; you are back to LWW churn.
//   - A REORDER has no identity-preserving form in IndexList at all (a moved
//     element gets a new Index), so it always churns.
//
// We MEASURE these (recording observed op counts) and pin them so the finding
// can't silently drift under an Adaptify/FSharp.Data.Adaptive upgrade.
// =============================================================================

open FSharp.Data.Adaptive

open Example

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// -----------------------------------------------------------------------------
// Measurement: drive an AdaptiveModel from one whole model to the next and count
// the IndexListDelta operations emitted on the list field (PropD : string list).
// -----------------------------------------------------------------------------

type private DeltaCount = { Sets : int; Removes : int } with
    member c.Total = c.Sets + c.Removes

let private mk (propD : IndexList<string>) : Model =
    { PropA = ""; PropB = None; PropC = IndexList.empty
      PropD = propD; PropE = { Prop0 = "" }; PropF = None }

/// Count the delta PropD emits when the model goes from `before` to `after`.
let private measurePropD (before : IndexList<string>) (after : IndexList<string>) : DeltaCount =
    let am = AdaptiveModel.Create (mk before)
    let reader = am.PropD.GetReader ()
    // Prime: the first GetChanges reports the whole initial content; discard it so
    // we measure only the incremental delta caused by the reassignment.
    reader.GetChanges FSharp.Data.Adaptive.AdaptiveToken.Top |> ignore
    transact (fun () -> am.Update (mk after))
    let delta = reader.GetChanges FSharp.Data.Adaptive.AdaptiveToken.Top
    let ops = delta |> IndexListDelta.toList
    let sets = ops |> List.filter (fun (_, op) -> match op with Set _ -> true | _ -> false) |> List.length
    let removes = ops |> List.filter (fun (_, op) -> match op with Remove -> true | _ -> false) |> List.length
    { Sets = sets; Removes = removes }

let private start = IndexList.ofList [ "a"; "b"; "c" ]

let tests = testList "Adaptive.Assumptions" [

    // ---- Identity-PRESERVING regime: structural ops on the previous IndexList.
    // Here Adaptive earns its keep: each logical change is a minimal delta.

    test "A1a: append via .Add preserves Index → 1 Set (minimal, identity-stable)" {
        let after = start.Add "d"
        let c = measurePropD start after
        Expect.equal c { Sets = 1; Removes = 0 }
            "appending one item touches exactly one Index — Adaptive emits an O(1) delta"
    }

    test "A1b: prepend via .Prepend preserves Index → 1 Set (minimal, identity-stable)" {
        let after = start.Prepend "x"
        let c = measurePropD start after
        Expect.equal c { Sets = 1; Removes = 0 }
            "prepend is also O(1): the existing a/b/c keep their Indices, x gets a new one"
    }

    test "A1c: remove-middle via .RemoveAt preserves Index → 1 Remove (minimal)" {
        let after = start.RemoveAt 1
        let c = measurePropD start after
        Expect.equal c { Sets = 0; Removes = 1 }
            "removing one item is an O(1) delta — only the dropped Index is touched"
    }

    // ---- Identity-DESTROYING regimes: rebuild-from-scratch and reorder. Here
    // Adaptive degrades to clear-all + add-all — a naive "lean on Adaptive's list
    // deltas" design buys nothing over LWW, which is why Encode.list is restricted
    // to value sequences and identity lives in Encode.map/HashMap keys instead.

    test "A1d: rebuild via IndexList.ofList (fresh Indices) → clear-all + add-all, NOT minimal" {
        // The idiomatic `{ model with Items = recompute () }`: a brand-new list with
        // the SAME logical prepend as the .Prepend case above — but fresh Indices.
        let after = IndexList.ofList [ "x"; "a"; "b"; "c" ]
        let c = measurePropD start after
        // OBSERVED (pinned): 4 Sets, 0 Removes. `computeDelta` aligns the two lists
        // POSITIONALLY by Index, so a prepend reads as "position 0 changed a→x,
        // position 1 changed b→a, position 2 changed c→b, position 3 added c" — it
        // does NOT recognise that a/b/c survived. A logically-O(1) prepend cost O(n)
        // value-rewrites, and item identity is lost (a/b/c were rebound to new slots).
        Expect.equal (c.Sets, c.Removes) (4, 0)
            "rebuild-from-scratch defeats Adaptive's diff: positional value-rewrite, not identity-stable"
        Expect.isTrue (c.Total > 1) "not minimal"
    }

    test "A1e: reorder has NO identity-preserving form → churns (relevant to move/nested state)" {
        // Reverse the list. There is no IndexList operation that *moves* an element
        // while keeping its Index, so a reorder is always expressed as fresh Indices.
        let after = IndexList.ofList [ "c"; "b"; "a" ]
        let c = measurePropD start after
        // OBSERVED (pinned): reversing [a;b;c] → [c;b;a] is 2 Sets (positions 0 and 2
        // swap value), 0 Removes — again a positional value-rewrite, not a move.
        Expect.equal (c.Sets, c.Removes) (2, 0)
            "a reorder is positional value-churn, never an identity-preserving move"
        Expect.isTrue (c.Total > 1) "not minimal"
    }

    // ---- Nested-state identity (PropC : IndexList<Submodel> → ChangeableModelList).
    // Does a rebuilt list reuse the inner AdaptiveSubmodel objects (so a Y.Text/
    // custom hung off an item would survive), or replace them (orphaning state)?

    test "A1f: rebuilt list of submodels reuses inner adaptive objects POSITIONALLY, not by id" {
        let mkC (items : string list) : Model =
            { PropA = ""; PropB = None
              PropC = items |> List.map (fun p -> { Prop0 = p }) |> IndexList.ofList
              PropD = IndexList.empty; PropE = { Prop0 = "" }; PropF = None }
        let am = AdaptiveModel.Create (mkC [ "a"; "b" ])
        // Capture the adaptive submodel object sitting at position 0.
        let at0Before = am.PropC |> AList.force |> IndexList.toList |> List.item 0
        // Prepend "x" by REBUILDING the list (fresh Indices) — the idiomatic update.
        transact (fun () -> am.Update (mkC [ "x"; "a"; "b" ]))
        let at0After = am.PropC |> AList.force |> IndexList.toList |> List.item 0
        // ChangeableModelList reconciles by position/Index: position 0's adaptive
        // object is REUSED and updated in place from "a" to "x". So the object that
        // *was* item "a" is now item "x" — identity follows POSITION, not the item.
        Expect.isTrue (System.Object.ReferenceEquals (at0Before, at0After))
            "position 0's adaptive object is reused — a rebuilt list rebinds inner state to the new positional occupant"
        Expect.equal (at0After.Prop0 |> AVal.force) "x"
            "and it now holds the prepended item's value: 'a' item's nested state was hijacked by 'x'"
    }
]

// Note: the Example model here has no HashMap field, so keyed (as opposed to
// positional) reconcile identity is not re-pinned in this file — Step 5b's
// keyed-map binding tests are where HashMap-element identity-by-key gets
// pinned end-to-end, against a real Y.Map binding rather than a bare Adaptify
// characterization.
