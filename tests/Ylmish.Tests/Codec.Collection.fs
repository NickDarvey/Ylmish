module Ylmish.Codec.Collection

// Plan 0007, Step 1 — the library `Encode.collection` / `Decode.collection`: an
// element-wise, id-keyed collection where concurrent add/remove merge (no lost
// items) and per-item scalar fields are per-id last-writer-wins. Proven directly
// at the connect layer with two docs.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let private item id fields : CollectionItem = { Id = id; Fields = fields }
let private ids (xs : CollectionItem list) = xs |> List.map (fun i -> i.Id) |> List.sort

/// Normalised converged shape for stable comparison (ids + sorted fields).
let private norm (m : cval<CollectionItem list>) =
    AVal.force m
    |> List.map (fun i -> i.Id, List.sort i.Fields)
    |> List.sortBy fst

let private fieldOf (m : cval<CollectionItem list>) (id : string) (field : string) =
    AVal.force m
    |> List.tryFind (fun i -> i.Id = id)
    |> Option.bind (fun i -> i.Fields |> List.tryFind (fst >> (=) field) |> Option.map snd)

type private Peer =
    { Doc : Y.Doc
      Items : clist<CollectionItem>
      Merged : cval<CollectionItem list>
      Dispose : System.IDisposable }

let private mkPeer (initial : CollectionItem list) : Peer =
    let cl = clist initial
    let merged = cval ([] : CollectionItem list)
    let enc =
        Encode.object [ "todos", Encode.collection (fun it -> AVal.constant it) merged (cl :> alist<_>) ]
    let doc = Y.Doc.Create ()
    { Doc = doc; Items = cl; Merged = merged; Dispose = Y.Doc.connect doc enc }

/// Push a whole new item list (the MVU way) into a peer.
let private setItems (p : Peer) (items : CollectionItem list) =
    transact (fun () -> p.Items.Value <- IndexList.ofList items)

let private sync (a : Peer) (b : Peer) =
    Y.applyUpdate (b.Doc, Y.encodeStateAsUpdate a.Doc, box "remote")

let private exchange (a : Peer) (b : Peer) =
    sync a b
    sync b a

let tests = testList "Codec.Collection" [

    test "concurrent adds of different items both survive (element-wise, not LWW)" {
        let p1 = mkPeer []
        let p2 = mkPeer []
        setItems p1 [ item "a" [] ]
        setItems p2 [ item "b" [] ]
        exchange p1 p2
        Expect.equal (ids (AVal.force p1.Merged)) [ "a"; "b" ] "peer 1 has both items"
        Expect.equal (ids (AVal.force p2.Merged)) [ "a"; "b" ] "peer 2 has both items"
    }

    test "concurrent edits to different items' fields merge" {
        let start = [ item "a" [ "done", "false" ]; item "b" [ "done", "false" ] ]
        let p1 = mkPeer start
        let p2 = mkPeer start
        exchange p1 p2
        // p1 completes a; p2 completes b — concurrently.
        setItems p1 [ item "a" [ "done", "true" ]; item "b" [ "done", "false" ] ]
        setItems p2 [ item "a" [ "done", "false" ]; item "b" [ "done", "true" ] ]
        exchange p1 p2
        Expect.equal (fieldOf p1.Merged "a" "done") (Some "true") "a's completion (peer 1) survives"
        Expect.equal (fieldOf p1.Merged "b" "done") (Some "true") "b's completion (peer 2) survives"
        Expect.equal (norm p1.Merged) (norm p2.Merged) "peers converge"
    }

    test "concurrent edits to the SAME field converge (per-id LWW)" {
        let start = [ item "a" [ "done", "false" ] ]
        let p1 = mkPeer start
        let p2 = mkPeer start
        exchange p1 p2
        setItems p1 [ item "a" [ "done", "true" ] ]
        setItems p2 [ item "a" [ "done", "false" ] ]
        exchange p1 p2
        Expect.equal (fieldOf p1.Merged "a" "done") (fieldOf p2.Merged "a" "done")
            "same-field concurrent edits converge to one value (LWW)"
    }

    test "remove on one peer removes the item everywhere" {
        let start = [ item "a" []; item "b" [] ]
        let p1 = mkPeer start
        let p2 = mkPeer start
        exchange p1 p2
        setItems p1 [ item "b" [] ]   // remove a
        exchange p1 p2
        Expect.equal (ids (AVal.force p1.Merged)) [ "b" ] "a removed on peer 1"
        Expect.equal (ids (AVal.force p2.Merged)) [ "b" ] "removal propagated to peer 2"
    }
]
