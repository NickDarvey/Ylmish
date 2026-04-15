module Ylmish.Y.Array

open FSharp.Data.Adaptive
open Yjs

open Ylmish

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let tests = testList "Y.Array" [
    testList "attachDecode (decode-only)" [
        test "attachDecode observes Y.Array changes and updates adaptive clist" {
            let ydoc = Y.Doc.Create ()
            let yarray : Y.Array<Y.Y.Element option> = ydoc.getArray "test"
            let _ = yarray.push [| Some (Y.Y.Element.String "a"); Some (Y.Y.Element.String "b") |]

            let alist : clist<Y.A.Element option> =
                yarray
                |> Seq.map (Option.map Y.Element.toAdaptive)
                |> clist
            let active = ref false
            let _ = Y.Array.attachDecode active alist yarray

            // Insert from Yjs side should be reflected in adaptive
            let _ = yarray.push [| Some (Y.Y.Element.String "c") |]

            let items = AList.force alist |> IndexList.toList
            Expect.equal items.Length 3 "alist should have 3 items after Yjs push"
            match items.[2] with
            | Some (Y.A.Element.Value (Y.A.Value.String s)) -> Expect.equal s "c" "third item should be 'c'"
            | _ -> failwith "third item should be String 'c'"
        }

        test "attachDecode does not sync Adaptive→Yjs (encode direction is not attached)" {
            let ydoc = Y.Doc.Create ()
            let yarray : Y.Array<Y.Y.Element option> = ydoc.getArray "test"
            let _ = yarray.push [| Some (Y.Y.Element.String "a") |]

            let alist : clist<Y.A.Element option> =
                yarray
                |> Seq.map (Option.map Y.Element.toAdaptive)
                |> clist
            let active = ref false
            let _ = Y.Array.attachDecode active alist yarray

            // Insert from adaptive side should NOT be reflected in yarray
            let _ = transact (fun () -> alist.Add(Some (Y.A.Element.Value (Y.A.Value.String "b"))))

            let adaptiveItems = AList.force alist |> IndexList.toList
            Expect.equal adaptiveItems.Length 2 "alist should have 2 items"

            let yarrayItems = yarray.toArray()
            Expect.equal yarrayItems.Count 1 "yarray should still have 1 item (encode not attached)"
        }
    ]

    testList "attachEncode (encode-only)" [
        test "attachEncode observes adaptive changes and updates Y.Array" {
            let ydoc = Y.Doc.Create ()
            let alist = clist [ Some (Y.A.Element.Value (Y.A.Value.String "a")) ]
            let yarray = Y.Array.Create ()
            do AList.force alist
                |> IndexList.map (Option.map Y.Element.ofAdaptive)
                |> IndexList.toArray
                |> yarray.push
            let _ = ydoc.getMap("container").set("test", yarray)

            let active = ref false
            let _ = Y.Array.attachEncode active alist yarray

            // Insert from adaptive side should be reflected in Yjs
            let _ = transact (fun () -> alist.Add(Some (Y.A.Element.Value (Y.A.Value.String "b"))))

            let yarrayItems = yarray.toArray()
            Expect.equal yarrayItems.Count 2 "yarray should have 2 items after adaptive add"
            match yarray.get 1 with
            | Some (Y.Y.Element.String s) -> Expect.equal s "b" "second item should be 'b'"
            | _ -> failwith "second item should be String 'b'"
        }

        test "attachEncode does not sync Yjs→Adaptive (decode direction is not attached)" {
            let ydoc = Y.Doc.Create ()
            let alist = clist [ Some (Y.A.Element.Value (Y.A.Value.String "a")) ]
            let yarray = Y.Array.Create ()
            do AList.force alist
                |> IndexList.map (Option.map Y.Element.ofAdaptive)
                |> IndexList.toArray
                |> yarray.push
            let _ = ydoc.getMap("container").set("test", yarray)

            let active = ref false
            let _ = Y.Array.attachEncode active alist yarray

            // Insert from Yjs side should NOT be reflected in alist
            let _ = yarray.push [| Some (Y.Y.Element.String "b") |]

            let adaptiveItems = AList.force alist |> IndexList.toList
            Expect.equal adaptiveItems.Length 1 "alist should still have 1 item (decode not attached)"

            let yarrayItems = yarray.toArray()
            Expect.equal yarrayItems.Count 2 "yarray should have 2 items"
        }
    ]

    testList "attach (bi-directional helper)" [
        test "attach synchronizes both directions with shared reentrancy guard" {
            let ydoc = Y.Doc.Create ()
            let yarray : Y.Array<Y.Y.Element option> = ydoc.getArray "test"
            let _ = yarray.push [| Some (Y.Y.Element.String "a") |]

            let alist : clist<Y.A.Element option> =
                yarray
                |> Seq.map (Option.map Y.Element.toAdaptive)
                |> clist
            let active = ref false
            use disposable = Y.Array.attach active alist yarray

            // Insert from Yjs side
            let _ = yarray.push [| Some (Y.Y.Element.String "b") |]
            let adaptiveItems = AList.force alist |> IndexList.toList
            Expect.equal adaptiveItems.Length 2 "alist should reflect Y.Array change"

            // Insert from adaptive side
            let _ = transact (fun () -> alist.Add(Some (Y.A.Element.Value (Y.A.Value.String "c"))))
            let yarrayItems = yarray.toArray()
            Expect.equal yarrayItems.Count 3 "yarray should reflect adaptive change"

            // Dispose should work correctly
            disposable.Dispose()
        }

        test "attach prevents feedback loops with shared reentrancy guard" {
            let ydoc = Y.Doc.Create ()
            let yarray : Y.Array<Y.Y.Element option> = ydoc.getArray "test"
            let _ = yarray.push [| Some (Y.Y.Element.String "a") |]

            let alist : clist<Y.A.Element option> =
                yarray
                |> Seq.map (Option.map Y.Element.toAdaptive)
                |> clist
            let active = ref false
            let _ = Y.Array.attach active alist yarray

            // This should not cause infinite loop
            let _ = transact (fun () -> alist.Add(Some (Y.A.Element.Value (Y.A.Value.String "b"))))

            let adaptiveItems = AList.force alist |> IndexList.toList
            Expect.equal adaptiveItems.Length 2 "alist should have expected value"

            let yarrayItems = yarray.toArray()
            Expect.equal yarrayItems.Count 2 "yarray should have expected value"
        }
    ]
]
