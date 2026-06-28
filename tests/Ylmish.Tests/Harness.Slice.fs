module Ylmish.HarnessSlice

// =============================================================================
// Plan 0006 — Step 5: thin vertical slice of Option E, end-to-end under
// `withYlmish`.
//
// Option E = derive minimal, identity-stable ops by reconciling each new
// immutable model against LIVE Yjs, keyed by item id. The collection lives at a
// **top-level Yjs root** (A1-safe get-or-create — a nested-in-root-map array would
// hit the A3 create-time clobber and still lose the cold concurrent add), so it
// rides the existing `connect` path as a consumer `CustomElement` (no library
// change beyond the Step 5 read-back hook). The model field stays a plain
// immutable `string list` — the promise the human chose to keep.
//
// This file proves the headline bug is fixed *end-to-end under withYlmish*: two
// peers concurrently add to the same collection and BOTH adds survive in BOTH
// Elmish models (P1 convergence + P2 no-loss), and a remote add reaches the other
// peer's model (P6 liveness via the new `afterTransaction` read-back). Minimality
// (P4) and the keyed mechanism itself are already covered in Harness.Options.
//
// Scope (thin slice): membership + order over `PropD : string list`, the exact
// field today's hybrid loses (Harness.fs "hybrid converges but violates
// no-loss"). Per-item nested collaborative text composes on top via id-named
// roots (proven independently in Harness.Identity / 0004's Scheme.byKey) and is
// the next increment, not this slice.
// =============================================================================

open Elmish
open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec
open Example

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// -----------------------------------------------------------------------------
// The Option E collection element — a consumer `CustomElement` over a top-level
// `Y.Array` of item ids. `local` is this peer's id list (mirrored up by keyed
// reconcile against live Yjs); `merged` is the converged id list it writes back
// and that `Decode.custom merged` reads. Mirrors the Counter binding's shape.
// -----------------------------------------------------------------------------

let collectionElement (local : aval<string list>) (merged : cval<string list>) : CustomElement =
    { new CustomElement with
        member _.Kind = Kind.Custom
        member _.Connect (ctx : BindContext) =
            let slot =
                match ctx.Slot with
                | Slot.Named n -> n
                | Slot.Index i -> string i
            let arr : Y.Array<obj> = ctx.Doc.getArray slot
            let active = ctx.Active
            let liveIds () = arr.toArray () |> Seq.map string |> Seq.toList
            let sync () = transact (fun () -> merged.Value <- liveIds ())
            // Decode (Y -> merged): the converged array IS the value.
            let observe (_ : Y.Array.Event<obj>) (_ : Y.Transaction) =
                if not active.Value then
                    active.Value <- true
                    try sync () finally active.Value <- false
            arr.observe observe
            // Encode (local -> Y): reconcile the live array to the model's ids by
            // key — Option E. An add is a single insert; concurrent adds compose.
            let cb =
                local.AddCallback (fun ids ->
                    if not active.Value then
                        active.Value <- true
                        try
                            ctx.Doc.transact (fun _ -> HarnessOptions.reconcileArray arr ids)
                            sync ()
                        finally active.Value <- false)
            { new System.IDisposable with
                member _.Dispose () =
                    arr.unobserve observe
                    cb.Dispose () } }

// -----------------------------------------------------------------------------
// A withYlmish program whose `PropD : string list` is an Option E collection.
// -----------------------------------------------------------------------------

let private emptyModel : Model =
    { PropA = ""; PropB = None; PropC = IndexList.empty
      PropD = IndexList.empty; PropE = { Prop0 = "" }; PropF = None }

let program (doc : Y.Doc) =
    // One merged cell per peer, threaded into both the encoder's binding and the
    // decoder (symmetric with Encode.custom / Decode.custom).
    let merged = cval ([] : string list)
    Program.mkSimple (fun () -> emptyModel) Model.update Model.view
    |> Program.withYlmish {
        Create = AdaptiveModel.Create
        Update = fun (a : AdaptiveModel) b -> a.Update b
        Encode =
            fun (m : AdaptiveModel) ->
                let local = m.PropD |> AList.toAVal |> AVal.map IndexList.toList
                Encode.object [ "items", Encode.custom (collectionElement local merged) ]
        Decode =
            Decode.object {
                let! items = Decode.object.required "items" (Decode.custom merged)
                return { emptyModel with PropD = IndexList.ofList items }
            }
        Doc = doc
    }
    |> Program.test

let private dispatch (d : Program.ElmishDispatcher<_, _>) (msg : Message) =
    d.Dispatch (Ylmish.Program.Message.User msg)

let private items (d : Program.ElmishDispatcher<_, _>) =
    d.Model.PropD |> IndexList.toList |> List.sort

let tests = testList "Slice (0006 Step 5)" [

    // THE HEADLINE FIX, END-TO-END: two withYlmish peers concurrently add to the
    // same collection; both adds survive in both Elmish models (vs the hybrid,
    // which drops one — Harness.fs "hybrid converges but violates no-loss").
    test "two withYlmish peers: concurrent collection adds BOTH survive (element-wise, not LWW)" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use disp1 = program d1
        use disp2 = program d2

        dispatch disp1 (AddPropD "a")
        dispatch disp2 (AddPropD "b")

        Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)
        Y.applyUpdate (d1, Y.encodeStateAsUpdate d2)

        Expect.equal (items disp1) (items disp2) "the Elmish models converge (P1)"
        Expect.equal (items disp1) [ "a"; "b" ]
            "BOTH concurrent adds survive in the model — element-wise merge under withYlmish (P2)"
    }

    // LIVENESS (P6): a remote add reaches the other peer's MODEL — the new
    // afterTransaction read-back fires for the top-level collection root that the
    // root-map observer never sees.
    test "a remote collection add reaches the other peer's model (read-back liveness)" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use disp1 = program d1
        use disp2 = program d2

        dispatch disp1 (AddPropD "x")
        Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)

        Expect.equal (items disp2) [ "x" ]
            "peer 2's model reflects peer 1's add without any local dispatch (P6 liveness)"
    }

    // SEQUENTIAL SANITY: adds + a remove round-trip through the model correctly.
    test "sequential add/remove on one peer round-trips through the model" {
        let d = Y.Doc.Create ()
        use disp = program d
        dispatch disp (AddPropD "a")
        dispatch disp (AddPropD "b")
        dispatch disp (RemPropD "a")
        Expect.equal (items disp) [ "b" ] "the collection reflects add a, add b, remove a"
    }
]
