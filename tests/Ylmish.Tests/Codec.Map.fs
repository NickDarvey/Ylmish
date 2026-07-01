module Ylmish.Codec.Map

// Plan 0008, Step 1 — the library `Encode.map` / `Decode.map`: an element-wise,
// id-keyed map whose items are ordinary OBJECTS (the `Encode.object`/`Decode.object`
// DSL), keyed by the model's map key, with the converged value read off the element
// (no consumer `merged` cell). Items carry a scalar (`done`, per-id LWW) and a
// per-item CRDT text field (`body`).

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// An item source: a bool cell (scalar) + a string cell (collaborative text).
type private Item = {| Done : cval<bool>; Body : cval<string> |}

let private item (d : bool) (b : string) : Item = {| Done = cval d; Body = cval b |}

let private encodeItem (it : Item) = Encode.object [
    "done", Encode.bool it.Done
    "body", Encode.text it.Body
]
let private decodeItem : Decoder<unit, Element<string>, bool * string> = Decode.object {
    let! d = Decode.object.required "done" Decode.bool
    let! b = Decode.object.required "body" Decode.text
    return (d, b)
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

let private keys (p : Peer) = decoded p |> HashMap.keys |> Seq.sort |> Seq.toList
let private get (p : Peer) k = decoded p |> HashMap.tryFind k
let private edit (p : Peer) k (f : Item -> unit) = transact (fun () -> f (HashMap.find k p.Items.Value))

let private sync (a : Peer) (b : Peer) = Y.applyUpdate (b.Doc, Y.encodeStateAsUpdate a.Doc, box "remote")
let private exchange a b = sync a b; sync b a

let tests = testList "Codec.Map" [

    test "items are decoded as objects (round-trip through the map)" {
        let p = mkPeer ()
        transact (fun () -> p.Items.["a"] <- item true "milk")
        Expect.equal (get p "a") (Some (true, "milk")) "the item decodes via Decode.object — typed bool + text"
    }

    test "concurrent adds of different items both survive (element-wise, not LWW)" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        transact (fun () -> p1.Items.["a"] <- item false "")
        transact (fun () -> p2.Items.["b"] <- item false "")
        exchange p1 p2
        Expect.equal (keys p1) [ "a"; "b" ] "peer 1 has both"
        Expect.equal (keys p2) [ "a"; "b" ] "peer 2 has both"
    }

    test "a scalar field edit round-trips to the other peer" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        transact (fun () -> p1.Items.["a"] <- item false "milk")
        exchange p1 p2
        edit p1 "a" (fun it -> it.Done.Value <- true)
        Expect.equal (get p1 "a") (Some (true, "milk")) "peer 1's own edit is reflected locally"
        exchange p1 p2
        Expect.equal (get p2 "a") (Some (true, "milk")) "peer 2 sees peer 1's field edit"
    }

    test "concurrent text edits to the same item's body merge character-wise" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        // Both start with an empty body (no double-insert), synced.
        transact (fun () -> p1.Items.["a"] <- item false "")
        exchange p1 p2
        transact (fun () -> p2.Items.["a"] <- item false "")   // p2 adopts a; empty mirror is a no-op
        exchange p1 p2
        // Concurrent edits to the SAME item's body.
        edit p1 "a" (fun it -> it.Body.Value <- "AAA")
        edit p2 "a" (fun it -> it.Body.Value <- "BBB")
        exchange p1 p2
        let t1 = get p1 "a" |> Option.map snd |> Option.defaultValue ""
        Expect.equal (get p1 "a" |> Option.map snd) (get p2 "a" |> Option.map snd) "body converges"
        Expect.isTrue (t1.Contains "AAA" && t1.Contains "BBB")
            "both peers' edits merged character-wise (CRDT), not last-writer-wins"
    }

    test "remove on one peer removes the item everywhere" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        transact (fun () ->
            p1.Items.["a"] <- item false ""
            p1.Items.["b"] <- item false "")
        exchange p1 p2
        transact (fun () -> p1.Items.Remove "a" |> ignore)
        exchange p1 p2
        Expect.equal (keys p1) [ "b" ] "a removed on peer 1"
        Expect.equal (keys p2) [ "b" ] "removal propagated to peer 2"
    }
]
