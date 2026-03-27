module Ylmish.Y.Map

open FSharp.Data.Adaptive
open Yjs

open Ylmish

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let tests = testList "Y.Map" [
    testList "ofAdaptive" [
        test "ofAdaptive (initialisation)" {
            let ydoc = Y.Doc.Create ()
            let amap = cmap [ "a", Some (Y.A.Element.Value (Y.A.Value.String "value1"))
                              "b", Some (Y.A.Element.Value (Y.A.Value.String "value2")) ]
            let ymap = Y.Map.ofAdaptive (amap :> amap<_, _>)
            // Attach to document so it can be accessed
            let _ = ydoc.getMap("container").set("test", Some ymap)

            Expect.equal (amap.["a"]) (Some (Y.A.Element.Value (Y.A.Value.String "value1"))) "amap 'a' doesn't equal expected value"
            Expect.equal (amap.["b"]) (Some (Y.A.Element.Value (Y.A.Value.String "value2"))) "amap 'b' doesn't equal expected value"

            match ymap.get "a" with
            | Some (Some (Y.Y.Element.String s)) -> Expect.equal s "value1" "ymap 'a' doesn't equal expected value"
            | _ -> failwith "ymap 'a' should be a string"

            match ymap.get "b" with
            | Some (Some (Y.Y.Element.String s)) -> Expect.equal s "value2" "ymap 'b' doesn't equal expected value"
            | _ -> failwith "ymap 'b' should be a string"
        }
    ]

    testList "toAdaptive" [
        test "toAdaptive (initialisation)" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            ymap.set("b", Some (Y.Y.Element.String "value2")) |> ignore
            let amap = Y.Map.toAdaptive ymap

            Expect.equal (amap |> AMap.force |> HashMap.count) 2 "amap should have 2 entries"
            Expect.equal (amap |> AMap.force |> HashMap.tryFind "a") (Some (Some (Y.A.Element.Value (Y.A.Value.String "value1")))) "amap 'a' doesn't equal expected value"
            Expect.equal (amap |> AMap.force |> HashMap.tryFind "b") (Some (Some (Y.A.Element.Value (Y.A.Value.String "value2")))) "amap 'b' doesn't equal expected value"
        }

        test "toAdaptive, amap insert: insert new key 'c'" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            let amap = Y.Map.toAdaptive ymap

            transact (fun () -> amap.["c"] <- Some (Y.A.Element.Value (Y.A.Value.String "value3")))

            Expect.equal (amap.["c"]) (Some (Y.A.Element.Value (Y.A.Value.String "value3"))) "amap 'c' doesn't equal expected value"

            match ymap.get "c" with
            | Some (Some (Y.Y.Element.String s)) -> Expect.equal s "value3" "ymap 'c' doesn't equal expected value"
            | _ -> failwith "ymap 'c' should be a string"
        }

        test "toAdaptive, amap update: update existing key 'a'" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            let amap = Y.Map.toAdaptive ymap

            transact (fun () -> amap.["a"] <- Some (Y.A.Element.Value (Y.A.Value.String "updated")))

            Expect.equal (amap.["a"]) (Some (Y.A.Element.Value (Y.A.Value.String "updated"))) "amap 'a' doesn't equal expected value"

            match ymap.get "a" with
            | Some (Some (Y.Y.Element.String s)) -> Expect.equal s "updated" "ymap 'a' doesn't equal expected value"
            | _ -> failwith "ymap 'a' should be a string"
        }

        test "toAdaptive, amap delete: remove key 'a'" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            ymap.set("b", Some (Y.Y.Element.String "value2")) |> ignore
            let amap = Y.Map.toAdaptive ymap

            transact (fun () -> amap.Remove "a" |> ignore)

            Expect.isFalse (amap.ContainsKey "a") "amap should not contain 'a'"
            Expect.isFalse (ymap.has "a") "ymap should not contain 'a'"
        }

        test "toAdaptive, ymap set: insert from Y side" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            let amap = Y.Map.toAdaptive ymap

            ymap.set("b", Some (Y.Y.Element.String "value2")) |> ignore

            Expect.equal (amap.["b"]) (Some (Y.A.Element.Value (Y.A.Value.String "value2"))) "amap 'b' doesn't equal expected value"

            match ymap.get "b" with
            | Some (Some (Y.Y.Element.String s)) -> Expect.equal s "value2" "ymap 'b' doesn't equal expected value"
            | _ -> failwith "ymap 'b' should be a string"
        }

        test "toAdaptive, ymap delete: delete from Y side" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            ymap.set("b", Some (Y.Y.Element.String "value2")) |> ignore
            let amap = Y.Map.toAdaptive ymap

            ymap.delete "a"

            Expect.isFalse (amap.ContainsKey "a") "amap should not contain 'a'"
            Expect.isFalse (ymap.has "a") "ymap should not contain 'a'"
        }

        test "toAdaptive, ymap set: insert from Y side" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            let amap = Y.Map.toAdaptive ymap

            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore

            Expect.equal (amap |> AMap.force |> HashMap.tryFind "a") (Some (Some (Y.A.Element.Value (Y.A.Value.String "value1")))) "amap 'a' doesn't equal expected value"
        }

        test "toAdaptive, ymap delete: delete from Y side" {
            let ydoc = Y.Doc.Create ()
            let ymap = ydoc.getMap "test"
            ymap.set("a", Some (Y.Y.Element.String "value1")) |> ignore
            ymap.set("b", Some (Y.Y.Element.String "value2")) |> ignore
            let amap = Y.Map.toAdaptive ymap

            ymap.delete "a"

            Expect.equal (amap |> AMap.force |> HashMap.count) 1 "amap should have 1 entry"
            Expect.isFalse (amap |> AMap.force |> HashMap.containsKey "a") "amap should not contain 'a'"
        }
    ]
]
