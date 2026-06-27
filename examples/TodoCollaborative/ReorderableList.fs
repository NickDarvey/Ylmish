module TodoCollaborative.ReorderableList

// A worked **reorderable collaborative list**: a list of items, each with a
// collaborative text `body`, where the text survives concurrent reorder/insert
// because each item's text root is named by a **stable, immutable id** (a guid)
// rather than its position — `Scheme.byKey "id"` (plan 0004).
//
// Why an id and not the index: under concurrent reorder two peers disagree about
// which item is "index 2", so positional root names (`items.2.body`) bind the
// same name to different logical items and their edits split. An immutable id is
// position-independent, so `items.<id>.body` always names the same logical item.
//
// Ordering is a *separate* concern from identity: the id names the root (and must
// never change), while display order is ordinary model state — typically a
// mutable **fractional index** (see the `fractional-indexing` library) you sort
// by. This sample focuses on the identity/naming half; it represents "the peers
// reordered" simply by having them hold the list in different orders.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

/// A runnable, in-process demonstration: two peers hold the same list in
/// different orders (and one inserted an extra item), then edit the same logical
/// item concurrently. With `Scheme.byKey` the edits merge onto the right item.
let run () =
    // Build a peer whose "items" list holds (id, body-text) in a given order.
    let mkPeer (items : (string * cval<string>) list) : Encoded<Element<string>> =
        let itemEnc (idStr, body) =
            Encode.object [ "id", Encode.value id (AVal.constant idStr); "body", Encode.text body ]
        Encode.object [ "items", Encode.list itemEnc (clist items :> alist<_>) ]

    let a1, b1 = cval "", cval ""
    let a2, b2, c2 = cval "", cval "", cval ""
    let enc1 = mkPeer [ "a", a1; "b", b1 ]            // peer A order: [a, b]
    let enc2 = mkPeer [ "b", b2; "a", a2; "c", c2 ]   // peer B: reordered + inserted c

    let d1 = Y.Doc.Create ()
    let d2 = Y.Doc.Create ()
    use _ = Y.Doc.connectWith (Scheme.byKey "id") d1 enc1
    use _ = Y.Doc.connectWith (Scheme.byKey "id") d2 enc2

    // Concurrent edits to the SAME logical item (a), plus text on the inserted one.
    transact (fun () -> a1.Value <- "hello ")
    transact (fun () -> a2.Value <- "world")
    transact (fun () -> c2.Value <- "inserted")

    Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)
    Y.applyUpdate (d1, Y.encodeStateAsUpdate d2)

    printfn "[reorder] peers hold the list in different orders (A:[a,b]  B:[b,a,c])"
    printfn "[reorder] item a, edited concurrently on both, named items.a.body:"
    printfn "[reorder]   A sees %A, B sees %A (converged, both edits merged)"
        ((d1.getText "items.a.body").toString ()) ((d2.getText "items.a.body").toString ())
    printfn "[reorder] inserted item c syncs by its own id: %A"
        ((d1.getText "items.c.body").toString ())
    printfn "[reorder] -> identity (not position) kept the text with the right item"
