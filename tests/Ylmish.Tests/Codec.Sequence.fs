module Ylmish.Codec.Sequence

// Plan 0008, Step 3 — the library `Encode.sequence` / `Decode.sequence`: a keyless
// CRDT sequence of values over a `Y.Array`. Concurrent inserts/removes merge (both
// adds survive), but there is no per-item identity — values are added/removed/
// reordered as a whole, not edited in place.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

type private Peer = { Tags : clist<string>; Doc : Y.Doc; Enc : Encoded<Element<string>>; Dispose : System.IDisposable }

let private mkPeer () : Peer =
    let cl = clist<string> ()
    let enc = Encode.object [ "tags", Encode.sequence (cl :> alist<_>) ]
    let doc = Y.Doc.Create ()
    { Tags = cl; Doc = doc; Enc = enc; Dispose = Y.Doc.connect doc enc }

let private decoded (p : Peer) : string list =
    let decoder = Decode.object {
        let! tags = Decode.object.required "tags" Decode.sequence
        return tags
    }
    match Decode.run () decoder p.Enc |> AVal.force with
    | Ok xs -> xs
    | Error e -> failwithf "decode failed: %s" (Error.printAll e)

let private set (p : Peer) (xs : string list) = transact (fun () -> p.Tags.Value <- IndexList.ofList xs)
let private sync (a : Peer) (b : Peer) = Y.applyUpdate (b.Doc, Y.encodeStateAsUpdate a.Doc, box "remote")
let private exchange a b = sync a b; sync b a

let tests = testList "Codec.Sequence" [

    test "a value sequence round-trips in order" {
        let p = mkPeer ()
        set p [ "a"; "b"; "c" ]
        Expect.equal (decoded p) [ "a"; "b"; "c" ] "the sequence keeps its order"
    }

    test "concurrent appends both survive (element-wise, not LWW)" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        set p1 [ "x" ]
        exchange p1 p2
        set p2 [ "x" ]        // p2 adopts the shared base
        exchange p1 p2
        // Both append a different value to the shared base, concurrently.
        set p1 [ "x"; "a" ]
        set p2 [ "x"; "b" ]
        exchange p1 p2
        let r1 = decoded p1
        Expect.equal r1 (decoded p2) "sequences converge"
        Expect.isTrue (List.contains "a" r1 && List.contains "b" r1) "both concurrent appends survive"
        Expect.equal (List.head r1) "x" "the shared prefix is kept"
    }

    test "remove of a value propagates" {
        let p1 = mkPeer ()
        let p2 = mkPeer ()
        set p1 [ "a"; "b"; "c" ]
        exchange p1 p2
        set p2 [ "a"; "b"; "c" ]
        exchange p1 p2
        set p1 [ "a"; "c" ]   // remove "b"
        exchange p1 p2
        Expect.equal (decoded p1) [ "a"; "c" ] "b removed on peer 1"
        Expect.equal (decoded p2) [ "a"; "c" ] "removal propagated to peer 2"
    }
]
