module Ylmish.Y.Element

open FSharp.Data.Adaptive
open Yjs

open Ylmish

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let tests = testList "Y.Element" [
    testList "toAdaptive" [
        test "String converts to Value" {
            let result = Y.Element.toAdaptive (Y.Y.Element.String "hello")
            Expect.equal result (Y.A.Element.Value (Y.A.Value.String "hello")) "String should convert to Value"
        }

        test "Array converts to AList" {
            let ydoc = Y.Doc.Create ()
            let yarray : Y.Array<Y.Y.Element option> = ydoc.getArray "test"
            yarray.push [| Some (Y.Y.Element.String "item1"); Some (Y.Y.Element.String "item2") |] |> ignore
            let result = Y.Element.toAdaptive (Y.Y.Element.Array yarray)
            match result with
            | Y.A.Element.AList alist ->
                let items = AList.force alist |> IndexList.toList
                Expect.equal items.Length 2 "should have 2 items"
                Expect.equal items.[0] (Some (Y.A.Element.Value (Y.A.Value.String "item1"))) "first item"
                Expect.equal items.[1] (Some (Y.A.Element.Value (Y.A.Value.String "item2"))) "second item"
            | _ -> failwith "should be AList"
        }

        test "Map converts to AMap" {
            let ydoc = Y.Doc.Create ()
            let ymap : Y.Map<Y.Y.Element option> = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            ymap.set("b", Some (Y.Y.Element.String "value2")) |> ignore
            let result = Y.Element.toAdaptive (Y.Y.Element.Map ymap)
            match result with
            | Y.A.Element.AMap amap ->
                let items = AMap.force amap
                Expect.equal (HashMap.count items) 2 "should have 2 entries"
                Expect.equal (HashMap.tryFind "a" items) (Some (Some (Y.A.Element.Value (Y.A.Value.String "value1")))) "entry 'a'"
                Expect.equal (HashMap.tryFind "b" items) (Some (Some (Y.A.Element.Value (Y.A.Value.String "value2")))) "entry 'b'"
            | _ -> failwith "should be AMap"
        }
    ]

    testList "ofAdaptive" [
        test "Value String converts to String" {
            let result = Y.Element.ofAdaptive (Y.A.Element.Value (Y.A.Value.String "hello"))
            match result with
            | Y.Y.Element.String s -> Expect.equal s "hello" "should be 'hello'"
            | _ -> failwith "should be String"
        }

        test "Value Text converts to String" {
            let text = IndexList.ofList ['h'; 'i']
            let result = Y.Element.ofAdaptive (Y.A.Element.Value (Y.A.Value.Text text))
            match result with
            | Y.Y.Element.String s -> Expect.equal s "hi" "should be 'hi'"
            | _ -> failwith "should be String"
        }

        test "AList converts to Array" {
            let ydoc = Y.Doc.Create ()
            let l = clist [ Some (Y.A.Element.Value (Y.A.Value.String "item1")); Some (Y.A.Element.Value (Y.A.Value.String "item2")) ]
            let result = Y.Element.ofAdaptive (Y.A.Element.AList (l :> alist<_>))
            // Y.Element is erased, so unbox to the underlying Y.Array type
            let yarray : Y.Array<Y.Y.Element option> = unbox result
            // Attach to a document so values are accessible
            ydoc.getMap("container").set("test", yarray) |> ignore
            let items = yarray.toArray()
            Expect.equal items.Count 2 "should have 2 items"
            match yarray.get 0 with
            | Some (Y.Y.Element.String s) -> Expect.equal s "item1" "first item should be 'item1'"
            | _ -> failwith "first item should be String"
            match yarray.get 1 with
            | Some (Y.Y.Element.String s) -> Expect.equal s "item2" "second item should be 'item2'"
            | _ -> failwith "second item should be String"
        }

        test "AMap converts to Map" {
            let ydoc = Y.Doc.Create ()
            let m = cmap [ "a", Some (Y.A.Element.Value (Y.A.Value.String "value1")) ]
            let result = Y.Element.ofAdaptive (Y.A.Element.AMap (m :> amap<_, _>))
            // Y.Element is erased, so unbox to the underlying Y.Map type
            let ymap : Y.Map<Y.Y.Element option> = unbox result
            // Attach to a document so values are accessible
            ydoc.getMap("container").set("test", ymap) |> ignore
            match ymap.get "a" with
            | Some (Some (Y.Y.Element.String s)) -> Expect.equal s "value1" "entry 'a' should be 'value1'"
            | _ -> failwith "entry 'a' should be a string"
        }
    ]
]
