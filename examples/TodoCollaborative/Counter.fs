module TodoCollaborative.Counter

// A worked **custom element**: a grow-only counter, defined entirely in consumer
// code on Ylmish's public seam (`CustomElement` + `Encode.custom` / `Decode.custom`)
// — nothing is added to the library's `Element` union.
//
// The merge strategy is the whole point. The counter's value is the number of
// ticks in a `Y.Array` root, so two peers that each increment concurrently both
// have their tick survive (Y.Array merges concurrent inserts): the merged value
// is the SUM. A last-writer-wins register (`Encode.value`) would keep only one.
//
// Like the built-in text element, a custom element lives in its own top-level
// root (the A3-safe `Parent = Root`, relying on A1 root get-or-create) and wires
// bi-directional, delta-level sync through `connect` — so it composes with the
// rest of the codec instead of replacing it.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

/// Build the counter element. `local` is this peer's count, mirrored up into the
/// `Y.Array` as ticks; `merged` is the converged value the binding writes back
/// from the array and that `Decode.custom merged` reads. Increments only.
let element (local : aval<int>) (merged : cval<int>) : CustomElement =
    { new CustomElement with
        member _.Kind = Kind.Custom
        member _.Connect (ctx : BindContext) =
            // The Scheme already chose our root name; we only get-or-create it.
            let name =
                match ctx.Slot with
                | Slot.Named n -> n
                | Slot.Index i -> string i
            let arr : Y.Array<obj> = ctx.Doc.getArray name
            let active = ctx.Active
            let len () = arr.toArray().Count
            let sync () = transact (fun () -> merged.Value <- len ())
            // Decode (Y -> merged): the converged array length is the value.
            let observe (_e : Y.Array.Event<obj>) (_t : Y.Transaction) =
                if not active.Value then
                    active.Value <- true
                    try sync () finally active.Value <- false
            arr.observe observe
            // Encode (local -> Y): push the positive delta of new ticks, honouring
            // the shared reentrancy guard so a local push never echoes back.
            let cb =
                local.AddCallback (fun n ->
                    if not active.Value then
                        active.Value <- true
                        try
                            let toAdd = n - len ()
                            if toAdd > 0 then
                                ctx.Doc.transact (fun _ ->
                                    for _ in 1 .. toAdd do arr.push [| box 1 |])
                            sync ()
                        finally active.Value <- false)
            { new System.IDisposable with
                member _.Dispose () =
                    arr.unobserve observe
                    cb.Dispose () } }

/// A runnable, in-process demonstration: two peers each increment once,
/// concurrently, then exchange updates. The merged value is the SUM on both —
/// the merge a custom element buys you over last-writer-wins.
let run () =
    let l1, m1 = cval 0, cval 0
    let l2, m2 = cval 0, cval 0
    let enc1 : Encoded<Element<string>> = Encode.object [ "hits", Encode.custom (element l1 m1) ]
    let enc2 : Encoded<Element<string>> = Encode.object [ "hits", Encode.custom (element l2 m2) ]

    let d1 = Y.Doc.Create ()
    let d2 = Y.Doc.Create ()
    use _ = Y.Doc.connect d1 enc1
    use _ = Y.Doc.connect d2 enc2

    // Concurrent increments, no pre-sync.
    transact (fun () -> l1.Value <- 1)
    transact (fun () -> l2.Value <- 1)

    Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)
    Y.applyUpdate (d1, Y.encodeStateAsUpdate d2)

    // Read the merged value back through the public decoder.
    let decoder (cell : aval<int>) : Decoder<unit, Element<string>, int> =
        Decode.object {
            let! n = Decode.object.required "hits" (Decode.custom cell)
            return n
        }
    let read enc cell =
        match AVal.force (Decode.run () (decoder cell) enc) with
        | Ok n -> n
        | Error _ -> -1

    printfn "[counter] A and B each incremented once, concurrently"
    printfn "[counter] merged value: A=%d B=%d (the SUM — last-writer-wins would give 1)"
        (read enc1 m1) (read enc2 m2)
