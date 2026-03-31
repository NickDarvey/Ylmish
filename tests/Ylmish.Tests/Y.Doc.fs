module Ylmish.Y.Doc

open FSharp.Data.Adaptive
open Hedgehog
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// Use the example model from the common test helpers
module Example =
    open Example

    module Codec =
        module Submodel =
            let encode (asubmodel : AdaptiveSubmodel) = Encode.object [
                "prop0", asubmodel.Prop0 |> Encode.value id
            ]

            let decode : Decoder<_, Submodel> = Decode.object {
                let! prop0 = Decode.object.required "prop0" Decode.value
                return { Prop0 = prop0 }
            }

        let encode (amodel : AdaptiveModel) = Encode.object [
            "propA", amodel.PropA |> Encode.value id
            "propB", amodel.PropB |> Encode.option
            "propC", Encode.list Submodel.encode amodel.PropC
            "propD", Encode.list (fun s -> Encode.value id (AVal.constant s)) amodel.PropD
            "propE", Submodel.encode amodel.PropE
        ]

        let decode : Decoder<_, Model> = Decode.object {
            let! propA = Decode.object.required "propA" Decode.value
            let! propB = Decode.object.optional "propB" Decode.value
            let! propC = Decode.object.required "propC" (Decode.list.required Submodel.decode)
            let! propD = Decode.object.required "propD" (Decode.list.required Decode.value)
            let! propE = Decode.object.required "propE" Submodel.decode
            return {
                PropA = propA
                PropB = propB
                PropC = propC
                PropD = propD
                PropE = propE
                PropF = None
            }
        }

let tests = testList "Y.Doc" [
    testList "materialize" [
        test "materializes simple value" {
            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create {
                PropA = "test-value"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "sub-value" }
                PropF = None
            }

            let encoded = Example.Codec.encode amodel
            Y.Doc.materialize doc encoded

            let root = doc.getMap()
            Expect.equal (root.get "propA") (Some "test-value") "propA should be materialized"
        }

        test "materializes optional value (Some)" {
            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create {
                PropA = "unused"
                PropB = Some "optional-value"
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "sub-value" }
                PropF = None
            }

            let encoded = Example.Codec.encode amodel
            Y.Doc.materialize doc encoded

            let root = doc.getMap()
            Expect.equal (root.get "propB") (Some "optional-value") "propB should be materialized"
        }

        test "materializes optional value (None)" {
            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create {
                PropA = "unused"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "sub-value" }
                PropF = None
            }

            let encoded = Example.Codec.encode amodel
            Y.Doc.materialize doc encoded

            let root = doc.getMap()
            Expect.equal (root.get "propB") None "propB should be None"
        }

        test "materializes list of objects" {
            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create {
                PropA = "unused"
                PropB = None
                PropC = IndexList.ofList [
                    { Prop0 = "item1" }
                    { Prop0 = "item2" }
                ]
                PropD = IndexList.empty
                PropE = { Prop0 = "sub-value" }
                PropF = None
            }

            let encoded = Example.Codec.encode amodel
            Y.Doc.materialize doc encoded

            let root : Y.Map<Y.Array<Y.Map<string>>> = doc.getMap()
            let propC = root.get "propC"
            Expect.isSome propC "propC should exist"
            let arr = propC.Value
            let arrData = arr.toArray() |> Seq.toList
            Expect.equal arrData.Length 2 "propC should have 2 items"
            Expect.equal (arrData.[0].get("prop0")) (Some "item1") "first item prop0"
            Expect.equal (arrData.[1].get("prop0")) (Some "item2") "second item prop0"
        }

        test "materializes list of values" {
            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create {
                PropA = "unused"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.ofList ["value1"; "value2"; "value3"]
                PropE = { Prop0 = "sub-value" }
                PropF = None
            }

            let encoded = Example.Codec.encode amodel
            Y.Doc.materialize doc encoded

            let root : Y.Map<Y.Array<string>> = doc.getMap()
            let propD = root.get "propD"
            Expect.isSome propD "propD should exist"
            let arr = propD.Value
            let arrData = arr.toArray() |> Seq.toList
            Expect.equal arrData.Length 3 "propD should have 3 items"
            Expect.equal arrData.[0] "value1" "first item"
            Expect.equal arrData.[1] "value2" "second item"
            Expect.equal arrData.[2] "value3" "third item"
        }

        test "materializes nested object" {
            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create {
                PropA = "unused"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "nested-value" }
                PropF = None
            }

            let encoded = Example.Codec.encode amodel
            Y.Doc.materialize doc encoded

            let root : Y.Map<Y.Map<string>> = doc.getMap()
            let propE = root.get "propE"
            Expect.isSome propE "propE should exist"
            let nestedMap = propE.Value
            Expect.equal (nestedMap.get "prop0") (Some "nested-value") "nested prop0"
        }
    ]

    testList "dematerialize" [
        test "dematerializes simple value" {
            let doc = Y.Doc.Create ()
            let root = doc.getMap()
            root.set("propA", "test-value") |> ignore

            let element = Y.Doc.dematerialize doc

            match element with
            | Element.AMap amap ->
                let items = AMap.force amap
                match HashMap.tryFind "propA" items with
                | Some (Some (Element.Value str)) -> Expect.equal str "test-value" "propA value"
                | _ -> failwith "propA should be a Value"
            | _ -> failwith "root should be AMap"
        }

        test "dematerializes nested object" {
            let doc = Y.Doc.Create ()
            let root = doc.getMap()
            let nestedMap = Y.Map.Create<string>()
            nestedMap.set("prop0", "nested-value") |> ignore
            root.set("propE", nestedMap) |> ignore

            let element = Y.Doc.dematerialize doc

            match element with
            | Element.AMap amap ->
                let items = AMap.force amap
                match HashMap.tryFind "propE" items with
                | Some (Some (Element.AMap nestedAmap)) ->
                    let nestedItems = AMap.force nestedAmap
                    match HashMap.tryFind "prop0" nestedItems with
                    | Some (Some (Element.Value str)) -> Expect.equal str "nested-value" "nested prop0"
                    | _ -> failwith "prop0 should be a Value"
                | _ -> failwith "propE should be AMap"
            | _ -> failwith "root should be AMap"
        }

        test "dematerializes array" {
            let doc = Y.Doc.Create ()
            let root = doc.getMap()
            let arr = Y.Array.Create<string>()
            arr.push [| "item1"; "item2" |] |> ignore
            root.set("propD", arr) |> ignore

            let element = Y.Doc.dematerialize doc

            match element with
            | Element.AMap amap ->
                let items = AMap.force amap
                match HashMap.tryFind "propD" items with
                | Some (Some (Element.AList alist)) ->
                    let listItems = AList.force alist |> IndexList.toList
                    Expect.equal listItems.Length 2 "should have 2 items"
                    match listItems.[0], listItems.[1] with
                    | Some (Element.Value s1), Some (Element.Value s2) ->
                        Expect.equal s1 "item1" "first item"
                        Expect.equal s2 "item2" "second item"
                    | _ -> failwith "items should be Values"
                | _ -> failwith "propD should be AList"
            | _ -> failwith "root should be AMap"
        }
    ]

    testList "round-trip" [
        test "round-trip with simple values" {
            let doc = Y.Doc.Create ()
            let original : Example.Model = {
                PropA = "test-value"
                PropB = Some "optional"
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "sub-value" }
                PropF = None
            }

            let amodel = Example.AdaptiveModel.Create original
            let encoded = Example.Codec.encode amodel

            // Materialize
            Y.Doc.materialize doc encoded

            // Dematerialize
            let element = Y.Doc.dematerialize doc

            // Decode
            let decoded = Example.Codec.decode ([], element) |> AVal.force

            match decoded with
            | Ok result ->
                Expect.equal result.PropA original.PropA "PropA should match"
                Expect.equal result.PropB original.PropB "PropB should match"
                Expect.equal result.PropE.Prop0 original.PropE.Prop0 "PropE.Prop0 should match"
            | Error errors ->
                failwith $"Decoding failed: %A{errors}"
        }

        testCase "round-trip with complex model" <| fun _ -> Property.check <| property {
            let! propA = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propB = Gen.string (Range.linear 0 50) Gen.alphaNum |> Gen.option
            let! propD = Gen.string (Range.linear 0 20) Gen.alphaNum |> Gen.list (Range.linear 0 5)
            let! prop0 = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propCItems =
                Gen.string (Range.linear 0 50) Gen.alphaNum
                |> Gen.map (fun s -> { Example.Submodel.Prop0 = s })
                |> Gen.list (Range.linear 0 5)

            let original : Example.Model = {
                PropA = propA
                PropB = propB
                PropC = IndexList.ofList propCItems
                PropD = IndexList.ofList propD
                PropE = { Prop0 = prop0 }
                PropF = None
            }

            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create original
            let encoded = Example.Codec.encode amodel

            // Materialize
            Y.Doc.materialize doc encoded

            // Dematerialize
            let element = Y.Doc.dematerialize doc

            // Decode
            let decoded = Example.Codec.decode ([], element) |> AVal.force

            match decoded with
            | Ok result ->
                Expect.equal result.PropA original.PropA "PropA should match"
                Expect.equal result.PropB original.PropB "PropB should match"
                Expect.equal (IndexList.toList result.PropC) (IndexList.toList original.PropC) "PropC should match"
                Expect.equal (IndexList.toList result.PropD) (IndexList.toList original.PropD) "PropD should match"
                Expect.equal result.PropE.Prop0 original.PropE.Prop0 "PropE.Prop0 should match"
            | Error errors ->
                failwith $"Decoding failed: %A{errors}"
        }
    ]
]
