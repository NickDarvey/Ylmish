module Ylmish.Adaptive.Assumptions

// Plan 0002, Step 1 — pin the FSharp.Data.Adaptive / Adaptify delta behaviour the
// redesign depends on (assumption A1, lesson L1 in doc/plans/0002-ylmish-redesign.md).
//
// THE DECISIVE CHARACTERIZATION. The plan restricts `Encode.list` to VALUE
// sequences (identity lives in `Encode.map` keys) precisely because Adaptify's
// list deltas are POSITIONAL, not keyed. This file measures that and pins the
// observed op counts so the finding can't silently drift under an Adaptify or
// FSharp.Data.Adaptive upgrade.
//
// What the generated code does (common/Example.g.fs): an `IndexList<'T>` field
// becomes a plain `clist`, and `Update` sets the WHOLE new IndexList. So the
// emitted delta is whatever `ChangeableIndexList.Value <- new` computes by
// comparing the two IndexLists by their `Index` keys:
//   - derive the next list by STRUCTURAL ops (.Add/.Prepend/.RemoveAt) → Indices
//     are preserved → minimal, identity-stable delta.
//   - REBUILD from scratch (IndexList.ofList [...], the idiomatic
//     `{ m with Items = recompute () }`) → every Index is fresh → positional
//     value-rewrite (O(n)), and nested adaptive objects are rebound BY POSITION.
//   - a REORDER has no identity-preserving form → always churns.
//
// Consequence in the plan: entities with identity belong in `Encode.map`
// (HashMap → keyed amap reconcile), never in `Encode.list`.

open FSharp.Data.Adaptive

open Example

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// -----------------------------------------------------------------------------
// Measurement: drive an AdaptiveModel from one whole model to the next and count
// the IndexListDelta operations emitted on the list field (PropD : IndexList<string>).
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
    reader.GetChanges AdaptiveToken.Top |> ignore
    transact (fun () -> am.Update (mk after))
    let delta = reader.GetChanges AdaptiveToken.Top
    let ops = delta |> IndexListDelta.toList
    let sets = ops |> List.filter (fun (_, op) -> match op with Set _ -> true | _ -> false) |> List.length
    let removes = ops |> List.filter (fun (_, op) -> match op with Remove -> true | _ -> false) |> List.length
    { Sets = sets; Removes = removes }

let private start = IndexList.ofList [ "a"; "b"; "c" ]

let tests = testList "Adaptive.Assumptions (A1 / L1)" [

    // ---- Identity-PRESERVING regime: structural ops on the previous IndexList.
    // Here Adaptive earns its keep: each logical change is a minimal delta.

    test "append via .Add preserves Index → 1 Set (minimal, identity-stable)" {
        let c = measurePropD start (start.Add "d")
        Expect.equal c { Sets = 1; Removes = 0 }
            "appending one item touches exactly one Index — an O(1) delta"
    }

    test "prepend via .Prepend preserves Index → 1 Set (minimal, identity-stable)" {
        let c = measurePropD start (start.Prepend "x")
        Expect.equal c { Sets = 1; Removes = 0 }
            "prepend is O(1): a/b/c keep their Indices, x gets a new one"
    }

    test "remove-middle via .RemoveAt preserves Index → 1 Remove (minimal)" {
        let c = measurePropD start (start.RemoveAt 1)
        Expect.equal c { Sets = 0; Removes = 1 }
            "removing one item is an O(1) delta — only the dropped Index is touched"
    }

    // ---- Identity-DESTROYING regimes: rebuild-from-scratch and reorder. Here
    // Adaptive degrades to positional value-rewrite — a list of entities would be
    // corrupted (nested state rebound by position), which is why L1 bans it.

    test "rebuild via IndexList.ofList (fresh Indices) → positional rewrite, NOT minimal" {
        // The idiomatic `{ m with Items = recompute () }`: same logical prepend as
        // the .Prepend case above, but a brand-new list with fresh Indices.
        let after = IndexList.ofList [ "x"; "a"; "b"; "c" ]
        let c = measurePropD start after
        // computeDelta aligns POSITIONALLY: a prepend reads as pos0 a→x, pos1 b→a,
        // pos2 c→b, pos3 +c. A logically-O(1) prepend costs O(n) value-rewrites and
        // item identity is lost. (Ground truth captured/pinned in Step 1.)
        Expect.equal (c.Sets, c.Removes) (4, 0)
            "rebuild-from-scratch defeats Adaptive's diff: positional value-rewrite, not identity-stable"
        Expect.isTrue (c.Total > 1) "not minimal"
    }

    test "reorder has NO identity-preserving form → churns (relevant to nested state)" {
        // Reverse the list. No IndexList op *moves* an element keeping its Index, so
        // a reorder is always fresh Indices.
        let after = IndexList.ofList [ "c"; "b"; "a" ]
        let c = measurePropD start after
        Expect.equal (c.Sets, c.Removes) (2, 0)
            "a reorder is positional value-churn, never an identity-preserving move"
        Expect.isTrue (c.Total > 1) "not minimal"
    }

    // ---- Nested-state identity (PropC : IndexList<Submodel> → ChangeableModelList).
    // Does a rebuilt list reuse the inner AdaptiveSubmodel objects (so a nested
    // Y.Text/custom hung off an item would survive), or rebind them by position
    // (orphaning state)? This is the exact hazard L1 protects against.

    test "rebuilt list of submodels rebinds inner adaptive objects BY POSITION, not by id" {
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
        // ChangeableModelList reconciles by position: position 0's adaptive object is
        // REUSED and updated in place from "a" to "x". The object that WAS item "a"
        // now holds "x" — identity follows POSITION, not the item.
        Expect.isTrue (System.Object.ReferenceEquals (at0Before, at0After))
            "position 0's adaptive object is reused — a rebuilt list rebinds inner state to the new positional occupant"
        Expect.equal (at0After.Prop0 |> AVal.force) "x"
            "and it now holds the prepended item's value: item 'a's nested state was hijacked by 'x'"
    }
]
