module Ylmish.Adaptive.Assumptions

// Plan 0002, Step 1 — re-pin the Adaptify delta characterization (A1/L1).
//
// Characterization tests: no Ylmish production code is under test. Adapted
// from the reference branch's Adaptive.Spike.fs (its 0006 Step 1), whose
// findings plan 0002 imports as lesson L1:
//
//   Adaptify list deltas are POSITIONAL, not keyed. The idiomatic rebuild
//   `{ m with Items = recompute () }` yields O(n) positional value-rewrites,
//   and ChangeableModelList rebinds nested adaptive objects by position — a
//   nested collaborative type hung off item i would be hijacked by whatever
//   lands at position i.
//
// That is why `Encode.list` is restricted to value sequences by its type, and
// why entities with identity go in `Encode.map` over HashMap — whose Adaptify
// reconcile (ChangeableModelMap) is KEYED by construction, which the second
// half of this file pins.
//
// What the generated code does (Example.g.fs): an IndexList field becomes a
// clist and `Update` assigns the whole new IndexList; the delta is whatever
// `ChangeableIndexList.Value <- new` computes by comparing Index keys. A
// HashMap field becomes a ChangeableModelMap keyed by the map key.

open FSharp.Data.Adaptive

open Example

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

type private DeltaCount = { Sets : int; Removes : int } with
    member c.Total = c.Sets + c.Removes

let private countOps (ops : ('k * ElementOperation<'v>) list) : DeltaCount =
    let sets = ops |> List.filter (fun (_, op) -> match op with Set _ -> true | _ -> false) |> List.length
    let removes = ops |> List.filter (fun (_, op) -> match op with Remove -> true | _ -> false) |> List.length
    { Sets = sets; Removes = removes }

// -----------------------------------------------------------------------------
// IndexList (positional): drive an AdaptiveModel from one whole model to the
// next and count the IndexListDelta operations emitted on PropD.
// -----------------------------------------------------------------------------

let private mk (propD : IndexList<string>) : Model =
    { PropA = ""; PropB = None; PropC = IndexList.empty
      PropD = propD; PropE = { Prop0 = "" }; PropF = None }

/// Count the delta PropD emits when the model goes from `before` to `after`.
let private measurePropD (before : IndexList<string>) (after : IndexList<string>) : DeltaCount =
    let am = AdaptiveModel.Create (mk before)
    let reader = am.PropD.GetReader ()
    // Prime: the first GetChanges reports the whole initial content; discard it
    // so we measure only the incremental delta caused by the reassignment.
    reader.GetChanges AdaptiveToken.Top |> ignore
    transact (fun () -> am.Update (mk after))
    reader.GetChanges AdaptiveToken.Top |> IndexListDelta.toList |> List.map (fun (i, op) -> box i, op) |> countOps

// -----------------------------------------------------------------------------
// HashMap (keyed): same measurement over MapModel.ItemsByKey.
// -----------------------------------------------------------------------------

let private measureItems (before : HashMap<string, Submodel>) (after : HashMap<string, Submodel>) : DeltaCount =
    let am = AdaptiveMapModel.Create { ItemsByKey = before }
    let reader = am.ItemsByKey.GetReader ()
    reader.GetChanges AdaptiveToken.Top |> ignore
    transact (fun () -> am.Update { ItemsByKey = after })
    reader.GetChanges AdaptiveToken.Top |> HashMapDelta.toList |> List.map (fun (k, op) -> box k, op) |> countOps

let private start = IndexList.ofList [ "a"; "b"; "c" ]

let tests = testList "Adaptive.Assumptions" [

    // ---- A1, identity-PRESERVING regime: structural ops on the previous
    // IndexList keep Index identity, so each logical change is a minimal delta.

    testList "A1 IndexList structural ops (identity-preserving)" [
        test "append via .Add preserves Index: 1 Set" {
            let c = measurePropD start (start.Add "d")
            Expect.equal c { Sets = 1; Removes = 0 }
                "appending one item touches exactly one Index — an O(1) delta"
        }

        test "prepend via .Prepend preserves Index: 1 Set" {
            let c = measurePropD start (start.Prepend "x")
            Expect.equal c { Sets = 1; Removes = 0 }
                "prepend is also O(1): a/b/c keep their Indices, x gets a new one"
        }

        test "remove-middle via .RemoveAt preserves Index: 1 Remove" {
            let c = measurePropD start (start.RemoveAt 1)
            Expect.equal c { Sets = 0; Removes = 1 }
                "removing one item is an O(1) delta — only the dropped Index is touched"
        }
    ]

    // ---- A1, identity-DESTROYING regimes: rebuild-from-scratch and reorder
    // degrade to positional value-rewrites. This is the L1 evidence for
    // restricting Encode.list to value sequences.

    testList "A1 IndexList rebuild (positional, identity-destroying)" [
        test "rebuild via IndexList.ofList: positional rewrite, not minimal" {
            // The idiomatic `{ model with Items = recompute () }`: the SAME
            // logical prepend as the .Prepend case above — but fresh Indices.
            let c = measurePropD start (IndexList.ofList [ "x"; "a"; "b"; "c" ])
            // OBSERVED (pinned): 4 Sets, 0 Removes. computeDelta aligns the two
            // lists POSITIONALLY, so a prepend reads as "position 0 changed a→x,
            // position 1 b→a, position 2 c→b, position 3 added c". A logically
            // O(1) prepend costs O(n) value-rewrites and loses item identity.
            Expect.equal (c.Sets, c.Removes) (4, 0)
                "rebuild-from-scratch defeats the diff: positional value-rewrite, not identity-stable"
        }

        test "reorder has no identity-preserving form: positional churn, never a move" {
            let c = measurePropD start (IndexList.ofList [ "c"; "b"; "a" ])
            // OBSERVED (pinned): reversing [a;b;c] is 2 Sets (positions 0 and 2
            // swap values), 0 Removes — a positional value-rewrite, not a move.
            Expect.equal (c.Sets, c.Removes) (2, 0)
                "a reorder is positional value-churn — relevant to the structural-move limits (U13/L9)"
        }

        test "rebuilt list of submodels rebinds nested adaptive objects by POSITION, not identity" {
            // The decisive nested-state result: would a collaborative type hung
            // off item i survive a rebuild? No — position i's adaptive object is
            // reused for whatever lands there.
            let mkC (items : string list) : Model =
                { PropA = ""; PropB = None
                  PropC = items |> List.map (fun p -> { Prop0 = p }) |> IndexList.ofList
                  PropD = IndexList.empty; PropE = { Prop0 = "" }; PropF = None }
            let am = AdaptiveModel.Create (mkC [ "a"; "b" ])
            let at0Before = am.PropC |> AList.force |> IndexList.toList |> List.item 0
            // Prepend "x" by REBUILDING the list (fresh Indices) — the idiomatic update.
            transact (fun () -> am.Update (mkC [ "x"; "a"; "b" ]))
            let at0After = am.PropC |> AList.force |> IndexList.toList |> List.item 0
            Expect.isTrue (System.Object.ReferenceEquals (at0Before, at0After))
                "position 0's adaptive object is REUSED — nested state is rebound to the new positional occupant"
            Expect.equal (at0After.Prop0 |> AVal.force) "x"
                "the object that was item 'a' now holds 'x' — item a's nested state was hijacked"
        }
    ]

    // ---- The contrast that makes Encode.map the identity primitive: a HashMap
    // field reconciles BY KEY (ChangeableModelMap), so wholesale rebuilds emit
    // O(delta) ops and nested adaptive objects stay with their key.

    testList "A1 HashMap keyed reconcile (identity-preserving by construction)" [
        test "wholesale rebuild adding one key: 1 Set — keyed, O(delta)" {
            let before = HashMap.ofList [ "a", { Prop0 = "a" }; "b", { Prop0 = "b" } ]
            let after = HashMap.ofList [ "a", { Prop0 = "a" }; "b", { Prop0 = "b" }; "x", { Prop0 = "x" } ]
            let c = measureItems before after
            Expect.equal c { Sets = 1; Removes = 0 }
                "unchanged keys emit nothing; only the added key produces an op"
        }

        test "wholesale rebuild removing one key: 1 Remove" {
            let before = HashMap.ofList [ "a", { Prop0 = "a" }; "b", { Prop0 = "b" } ]
            let after = HashMap.ofList [ "a", { Prop0 = "a" } ]
            let c = measureItems before after
            Expect.equal c { Sets = 0; Removes = 1 }
                "only the removed key produces an op"
        }

        test "rebuilt HashMap keeps each key's adaptive object — identity follows the KEY" {
            let am = AdaptiveMapModel.Create {
                ItemsByKey = HashMap.ofList [ "a", { Prop0 = "a" }; "b", { Prop0 = "b" } ]
            }
            let aBefore = am.ItemsByKey |> AMap.force |> HashMap.find "a"
            // Rebuild wholesale, adding "x" and editing "b" — the same shape of
            // update that hijacked nested state in the IndexList case.
            transact (fun () -> am.Update {
                ItemsByKey = HashMap.ofList [ "a", { Prop0 = "a" }; "b", { Prop0 = "b2" }; "x", { Prop0 = "x" } ]
            })
            let aAfter = am.ItemsByKey |> AMap.force |> HashMap.find "a"
            Expect.isTrue (System.Object.ReferenceEquals (aBefore, aAfter))
                "key 'a' keeps its adaptive object across a wholesale rebuild"
            Expect.equal (aAfter.Prop0 |> AVal.force) "a"
                "and it still holds item a's value — no positional hijack"
            Expect.equal (am.ItemsByKey |> AMap.force |> HashMap.find "b" |> fun m -> m.Prop0 |> AVal.force) "b2"
                "the edited key's object was updated in place"
        }
    ]
]
