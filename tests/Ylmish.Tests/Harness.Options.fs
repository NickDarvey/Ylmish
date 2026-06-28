module Ylmish.HarnessOptions

// =============================================================================
// Plan 0006 — Step 2: per-option falsifiers on the representative datatype.
//
// Step 1 already settled the two hardest options on evidence:
//   • Option A (lean into Adaptive's deltas) is conditionally dead — Adaptive's
//     reassignment delta is POSITIONAL, not keyed; it never expresses a move and
//     degrades to O(n) value-churn whenever the app rebuilds its list.
//   • Option B (cut Adaptive, keyed diff) is fairly priced — Adaptive only buys a
//     positional diff we'd replace anyway.
//
// This file runs the *positive* experiment the decision needs: a concrete
// **keyed element-wise bridge** — a stable `Y.Array` of item ids reconciled by id
// against each new model — and validates it on the ordered representative
// datatype (add / remove / **move**) through the Step 0 harness. This bridge is
// the mechanism Options B and E both ultimately produce (B sources the intent
// from the previous model, E by reconciling against live Yjs; both APPLY the same
// id-keyed array ops). So validating it validates the family.
//
// It also pins the one thing that genuinely separates the options — **moves** —
// and the Yjs limit underneath them: `Y.Array` has no native move, so a move is
// delete+insert, which (a) duplicates under concurrent moves and (b) would orphan
// any per-item nested state (the Step 3 question). We pin both as findings.
// =============================================================================

open Yjs

open Ylmish.Harness

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// -----------------------------------------------------------------------------
// The keyed element-wise reconcile: drive a Y.Array's *live* contents to the
// target id order using by-id ops — delete removed ids, insert added ids at their
// target slot, move survivors whose position changed. Correct by construction for
// sequential edits; concurrent adds merge (Yjs inserts compose); concurrent moves
// hit the no-native-move limit (delete+insert) — pinned below.
// -----------------------------------------------------------------------------

let private liveIds (arr: Y.Array<obj>) = arr.toArray () |> Seq.map string |> Seq.toList

let reconcileArray (arr: Y.Array<obj>) (target: string list) =
    let targetSet = Set.ofList target
    // 1. delete ids no longer present, highest index first.
    liveIds arr
    |> List.mapi (fun i id -> i, id)
    |> List.filter (fun (_, id) -> not (targetSet.Contains id))
    |> List.map fst
    |> List.sortDescending
    |> List.iter (fun i -> arr.delete (float i, 1.0))
    // 2. place each target id at its slot (insert if missing, move if displaced).
    target
    |> List.iteri (fun i wantId ->
        let cur = liveIds arr
        let atI = if i < List.length cur then Some (List.item i cur) else None
        if atI <> Some wantId then
            match List.tryFindIndex ((=) wantId) cur with
            | Some j ->
                arr.delete (float j, 1.0)
                arr.insert (float i, [| box wantId |])
            | None -> arr.insert (float i, [| box wantId |]))

/// The keyed element-wise bridge (the B/E application mechanism). Stateless:
/// reconciles the live `Y.Array` to the new model by id, so it is robust to
/// remote updates landing between local ops (it always diffs against live Yjs).
let keyed : BridgeFactory =
    fun doc ->
        let arr : Y.Array<obj> = doc.getArray "items"
        { Name = "keyed"
          Doc = doc
          Read = fun () -> liveIds arr |> List.map (fun id -> { Id = id; Text = "" })
          Apply = fun m -> doc.transact (fun _ -> reconcileArray arr (m |> List.map (fun i -> i.Id))) }

// -----------------------------------------------------------------------------
// Ordered ground truth + helpers
// -----------------------------------------------------------------------------

let private idOrder (m: Model) = m |> List.map (fun i -> i.Id)

/// Folding the ops in issue-order over a single shared model is the exact
/// converged state under `Immediate` delivery with the read-back loop (each op
/// builds on the prior converged model) — a clean sequential ground truth.
let private foldOps (ops: ReplicaOp list) : Model =
    ops |> List.fold (fun m r -> applyOp m r.Op) []

let private add r id = { Replica = r; Op = Add (id, "") }
let private remove r id = { Replica = r; Op = Remove id }
let private move r id i = { Replica = r; Op = Move (id, i) }

let tests = testList "Options (0006 Step 2)" [
    testList "keyed element-wise bridge (the B/E mechanism)" [

        // SEQUENTIAL ORDERED CORRECTNESS: add + remove + move, delivered eagerly,
        // must converge to exactly the model fold — order included.
        test "sequential add/remove/move converges to the ordered model fold" {
            let ops = [ add 0 "a"; add 1 "b"; add 0 "c"; move 1 "a" 2; remove 0 "b" ]
            let r = run keyed Immediate ops
            Expect.isTrue r.Converged "replicas converge"
            Expect.equal (idOrder r.Final.[0]) (idOrder (foldOps ops))
                "element-wise keyed reconcile reproduces the sequential ordered result (a,c order with a moved to the end, b removed → [c; a])"
        }

        // NO-LOSS UNDER CONCURRENCY (the bug the hybrid fails): two concurrent adds
        // both survive, matching the raw-Yjs membership oracle — and converge.
        test "concurrent adds preserve both items (matches the oracle; no LWW)" {
            let ops = [ add 0 "a"; add 1 "b" ]
            let r = run keyed Concurrent ops
            let oracle = run RawYjs.factory Concurrent ops
            Expect.isTrue r.Converged "replicas converge"
            Expect.equal r.Ids.[0] oracle.Ids.[0] "keyed bridge matches the element-wise oracle membership"
            Expect.equal r.Ids.[0] (Set.ofList [ "a"; "b" ]) "both concurrent adds survive"
        }

        // MINIMALITY (M5 / P4): an add on top of 20 items is O(Δ) — a single insert,
        // not a re-materialized whole array (contrast the hybrid in Harness.fs).
        test "minimality: add is O(Δ), independent of existing size" {
            let models =
                [ 0 .. 20 ]
                |> List.map (fun n ->
                    [ 0 .. n - 1 ] |> List.map (fun k -> { Id = sprintf "item-%02d" k; Text = "" }))
            let sizes = measureApplies keyed models
            let small = List.item 1 sizes      // add the 1st item (empty doc)
            let large = List.last sizes        // add the 21st item (20 already present)
            Expect.isTrue (large <= small * 3)
                (sprintf "adding to a big list stays O(Δ): 21st add=%d bytes vs 1st add=%d bytes" large small)
        }

        // MOVE (sequential): reorder keeps the item and reaches the target order.
        test "a sequential move reorders without losing the item" {
            let ops = [ add 0 "a"; add 0 "b"; add 0 "c"; move 0 "c" 0 ]
            let r = run keyed Immediate ops
            Expect.equal (idOrder r.Final.[0]) [ "c"; "a"; "b" ] "c moved to the front"
            Expect.isTrue r.Converged "converges"
        }

        // PINNED LIMIT: Y.Array has no native move, so a move is delete+insert.
        // Under *concurrent* moves of the same item this can duplicate. The bridge
        // still CONVERGES (P1), but no-loss/no-dup is a known Yjs-array limit, not
        // a bridge bug — this is the wall every list-diff option hits, and the
        // reason Step 5 should consider a keyed-map + fractional-order layout
        // (0004) where a move is a key update, not delete+insert.
        test "concurrent same-item moves: converges (P1) but may duplicate — documented limit" {
            let it id = { Id = id; Text = "" }
            let model = List.map it
            let sync (from: Y.Doc) (into: Y.Doc) =
                Y.applyUpdate (into, Y.encodeStateAsUpdate from, box "remote")
            let d0 = Y.Doc.Create ()
            let d1 = Y.Doc.Create ()
            let b0 = keyed d0
            let b1 = keyed d1
            // Establish a shared base [a;b;c] on both docs.
            b0.Apply (model [ "a"; "b"; "c" ])
            sync d0 d1
            // Concurrently move 'a': peer 0 to the end, peer 1 to the middle.
            b0.Apply (model [ "b"; "c"; "a" ])
            b1.Apply (model [ "b"; "a"; "c" ])
            // Exchange both ways and re-read.
            sync d0 d1
            sync d1 d0
            let f0 = idOrder (b0.Read ())
            let f1 = idOrder (b1.Read ())
            Expect.equal f0 f1 "P1: both replicas converge to the SAME state"
            Expect.isTrue (List.contains "a" f0)
                "membership preserved — 'a' is not lost (it may appear twice: the no-native-move limit)"
        }
    ]
]
