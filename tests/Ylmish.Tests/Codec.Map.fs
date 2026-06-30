module Ylmish.Codec.Map

// Plan 0008, Step 1a — the library `Encode.map` / `Decode.map`: an element-wise,
// id-keyed map whose items are ordinary OBJECTS (the `Encode.object`/`Decode.object`
// DSL), keyed by the model's map key, with the converged value read off the element
// (no consumer `merged` cell). Scalars only here; per-item text is next.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// An item source: a couple of mutable cells, encoded as an object.
type private Item = {| Done : cval<bool>; Note : cval<string> |}

let private item (d : bool) (n : string) : Item = {| Done = cval d; Note = cval n |}

let private encodeItem (it : Item) = Encode.object [
    "done", Encode.bool it.Done
    "note", it.Note |> Encode.value id
]
let private decodeItem : Decoder<unit, Element<string>, bool * string> = Decode.object {
    let! d = Decode.object.required "done" Decode.bool
    let! n = Decode.object.required "note" Decode.value
    return (d, n)
}

type private Peer = { Items : cmap<string, Item>; Doc : Y.Doc; Enc : Encoded<Element<string>>; Dispose : System.IDisposable }

let private mkPeer () : Peer =
    let cm = cmap<string, Item> ()
    let enc = Encode.object [ "items", Encode.map encodeItem (cm :> amap<_, _>) ]
    let doc = Y.Doc.Create ()
    { Items = cm; Doc = doc; Enc = enc; Dispose = Y.Doc.connect doc enc }

let private decoded (p : Peer) : HashMap<string, bool * string> =
    let decoder = Decode.object {
        let! items = Decode.object.required "items" (Decode.map decodeItem)
        return items
    }
    match Decode.run () decoder p.Enc |> AVal.force with
    | Ok m -> m
    | Error e -> failwithf "decode failed: %s" (Error.printAll e)

let private keys (p : Peer) = decoded p |> HashMap.toSeqV |> ignore; decoded p |> HashMap.keys |> Seq.sort |> Seq.toList
let private get (p : Peer) k = decoded p |> HashMap.tryFind k

let private sync (a : Peer) (b : Peer) = Y.applyUpdate (b.Doc, Y.encodeStateAsUpdate a.Doc, box "remote")
let private exchange a b = sync a b; sync b a

let tests = testList "Codec.Map" [

    test "items are decoded as objects (round-trip through the map)" {
        let p = mkPeer ()
        transact (fun () -> p.Items.["a"] <- item true "milk")
        Expect.equal (get p "a") (Some (true, "milk")) "the item decodes via Decode.object — typed bool + string"
    }

    test "concurrent adds of different items both survive (element-wise, not LWW)" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        transact (fun () -> p1.Items.["a"] <- item false "milk")
        transact (fun () -> p2.Items.["b"] <- item false "eggs")
        exchange p1 p2
        Expect.equal (keys p1) [ "a"; "b" ] "peer 1 has both"
        Expect.equal (keys p2) [ "a"; "b" ] "peer 2 has both"
    }

    test "an item's field edit round-trips to the other peer" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        transact (fun () -> p1.Items.["a"] <- item false "milk")
        exchange p1 p2
        transact (fun () -> (HashMap.find "a" p1.Items.Value).Done.Value <- true)
        Expect.equal (get p1 "a") (Some (true, "milk")) "peer 1's own edit is reflected locally"
        exchange p1 p2
        Expect.equal (get p2 "a") (Some (true, "milk")) "peer 2 sees peer 1's field edit"
    }

    test "concurrent edits to the same field converge (per-id LWW)" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        transact (fun () -> p1.Items.["a"] <- item false "x")
        transact (fun () -> p2.Items.["a"] <- item false "x")
        exchange p1 p2
        transact (fun () -> (HashMap.find "a" p1.Items.Value).Note.Value <- "p1")
        transact (fun () -> (HashMap.find "a" p2.Items.Value).Note.Value <- "p2")
        exchange p1 p2
        Expect.equal (get p1 "a" |> Option.map snd) (get p2 "a" |> Option.map snd)
            "same-field concurrent edits converge to one value"
    }

    test "remove on one peer removes the item everywhere" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        transact (fun () ->
            p1.Items.["a"] <- item false "a"
            p1.Items.["b"] <- item false "b")
        exchange p1 p2
        transact (fun () -> p1.Items.Remove "a" |> ignore)
        exchange p1 p2
        Expect.equal (keys p1) [ "b" ] "a removed on peer 1"
        Expect.equal (keys p2) [ "b" ] "removal propagated to peer 2"
    }
]
