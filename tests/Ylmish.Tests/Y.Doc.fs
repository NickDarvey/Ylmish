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

            let decode : Decoder<_, _, Submodel> = Decode.object {
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

        let decode : Decoder<_, _, Model> = Decode.object {
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

        // Plan 0003, Step 2 — a Custom leaf is connect-managed (its own root),
        // like Text, so the structural path must skip it: materialize emits the
        // non-custom fields and simply omits the custom one from the root map.
        test "materialize skips a Custom field and still materializes the rest" {
            let stubCustom : CustomElement =
                { new CustomElement with
                    member _.Kind = Kind.Custom
                    member _.Connect _ =
                        { new System.IDisposable with member _.Dispose () = () } }

            let enc : Encoded<Element<string>> =
                HashMap.ofList [
                    "value",  Some (Element.Value "v")
                    "custom", Some (Element.Custom stubCustom)
                ]
                |> AMap.ofHashMap
                |> Element.AMap
                |> Some
                |> AVal.constant

            let doc = Y.Doc.Create ()
            // Must not throw on the Custom field.
            Y.Doc.materialize doc enc

            let root = doc.getMap()
            Expect.equal (root.get "value") (Some "v") "non-custom field is materialized"
            Expect.isFalse (root.has "custom") "custom field is absent from the structural root map"
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
            let decoded = Example.Codec.decode () ([], element) |> AVal.force

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
            let decoded = Example.Codec.decode () ([], element) |> AVal.force

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

    testList "properties" [
        testCase "materialize is idempotent" <| fun _ -> Property.check <| property {
            let! propA = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propB = Gen.string (Range.linear 0 50) Gen.alphaNum |> Gen.option
            let! propD = Gen.string (Range.linear 0 20) Gen.alphaNum |> Gen.list (Range.linear 0 5)
            let! prop0 = Gen.string (Range.linear 0 50) Gen.alphaNum

            let model : Example.Model = {
                PropA = propA
                PropB = propB
                PropC = IndexList.empty
                PropD = IndexList.ofList propD
                PropE = { Prop0 = prop0 }
                PropF = None
            }

            let doc1 = Y.Doc.Create ()
            let doc2 = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create model
            let encoded = Example.Codec.encode amodel

            // Materialize once
            Y.Doc.materialize doc1 encoded
            let decoded1 = Example.Codec.decode () ([], Y.Doc.dematerialize doc1) |> AVal.force

            // Materialize twice
            Y.Doc.materialize doc2 encoded
            Y.Doc.materialize doc2 encoded
            let decoded2 = Example.Codec.decode () ([], Y.Doc.dematerialize doc2) |> AVal.force

            // Both should produce the same decoded result
            match decoded1, decoded2 with
            | Ok result1, Ok result2 ->
                Expect.equal result1.PropA result2.PropA "PropA should match"
                Expect.equal result1.PropB result2.PropB "PropB should match"
                Expect.equal (IndexList.toList result1.PropD) (IndexList.toList result2.PropD) "PropD should match"
            | Error e1, _ -> failwith $"First decode failed: %A{e1}"
            | _, Error e2 -> failwith $"Second decode failed: %A{e2}"
        }

        testCase "materialize then dematerialize preserves structure" <| fun _ -> Property.check <| property {
            let! propA = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propB = Gen.string (Range.linear 0 50) Gen.alphaNum |> Gen.option
            let! propD = Gen.string (Range.linear 0 20) Gen.alphaNum |> Gen.list (Range.linear 0 5)
            let! prop0 = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propCItems =
                Gen.string (Range.linear 0 50) Gen.alphaNum
                |> Gen.map (fun s -> { Example.Submodel.Prop0 = s })
                |> Gen.list (Range.linear 0 5)

            let model : Example.Model = {
                PropA = propA
                PropB = propB
                PropC = IndexList.ofList propCItems
                PropD = IndexList.ofList propD
                PropE = { Prop0 = prop0 }
                PropF = None
            }

            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create model
            let encoded = Example.Codec.encode amodel

            // Materialize and dematerialize
            Y.Doc.materialize doc encoded
            let element = Y.Doc.dematerialize doc

            // Decode and verify all fields match the original model
            let decoded = Example.Codec.decode () ([], element) |> AVal.force

            match decoded with
            | Ok result ->
                Expect.equal result.PropA model.PropA "PropA should be preserved"
                Expect.equal result.PropB model.PropB "PropB should be preserved"
                Expect.equal (IndexList.toList result.PropC) (IndexList.toList model.PropC) "PropC should be preserved"
                Expect.equal (IndexList.toList result.PropD) (IndexList.toList model.PropD) "PropD should be preserved"
                Expect.equal result.PropE.Prop0 model.PropE.Prop0 "PropE should be preserved"
            | Error errors ->
                failwith $"Decoding failed: %A{errors}"
        }

        testCase "materialize handles empty collections" <| fun _ -> Property.check <| property {
            let! propA = Gen.string (Range.linear 0 50) Gen.alphaNum

            let model : Example.Model = {
                PropA = propA
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "default" }
                PropF = None
            }

            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create model
            let encoded = Example.Codec.encode amodel

            Y.Doc.materialize doc encoded
            let element = Y.Doc.dematerialize doc

            // Decode and verify empty collections are preserved
            let decoded = Example.Codec.decode () ([], element) |> AVal.force

            match decoded with
            | Ok result ->
                Expect.isEmpty (IndexList.toList result.PropC) "PropC should be empty"
                Expect.isEmpty (IndexList.toList result.PropD) "PropD should be empty"
            | Error errors ->
                failwith $"Decoding failed: %A{errors}"
        }

        testCase "materialize updates overwrite previous data" <| fun _ -> Property.check <| property {
            let! propA1 = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propA2 = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propD1 = Gen.string (Range.linear 0 20) Gen.alphaNum |> Gen.list (Range.linear 1 5)
            let! propD2 = Gen.string (Range.linear 0 20) Gen.alphaNum |> Gen.list (Range.linear 1 5)

            let model1 : Example.Model = {
                PropA = propA1
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.ofList propD1
                PropE = { Prop0 = "first" }
                PropF = None
            }

            let model2 : Example.Model = {
                PropA = propA2
                PropB = Some "updated"
                PropC = IndexList.empty
                PropD = IndexList.ofList propD2
                PropE = { Prop0 = "second" }
                PropF = None
            }

            let doc = Y.Doc.Create ()

            // Materialize first model
            let amodel1 = Example.AdaptiveModel.Create model1
            let encoded1 = Example.Codec.encode amodel1
            Y.Doc.materialize doc encoded1

            // Materialize second model (should overwrite)
            let amodel2 = Example.AdaptiveModel.Create model2
            let encoded2 = Example.Codec.encode amodel2
            Y.Doc.materialize doc encoded2

            // Dematerialize and decode
            let element = Y.Doc.dematerialize doc
            let decoded = Example.Codec.decode () ([], element) |> AVal.force

            match decoded with
            | Ok result ->
                // Should have second model's data, not first
                Expect.equal result.PropA propA2 "PropA should be from second model"
                Expect.equal result.PropB (Some "updated") "PropB should be updated"
                Expect.equal (IndexList.toList result.PropD) propD2 "PropD should be from second model"
                Expect.equal result.PropE.Prop0 "second" "PropE should be from second model"
            | Error errors ->
                failwith $"Decoding failed: %A{errors}"
        }

        testCase "dematerialize preserves element structural kinds" <| fun _ -> Property.check <| property {
            // This tests the structural type mapping invariant directly:
            // Value → Element.Value, list → Element.AList, object → Element.AMap
            let! propA = Gen.string (Range.linear 0 50) Gen.alphaNum
            let! propB = Gen.string (Range.linear 0 50) Gen.alphaNum |> Gen.option
            let! propCItems =
                Gen.string (Range.linear 0 20) Gen.alphaNum
                |> Gen.map (fun s -> { Example.Submodel.Prop0 = s })
                |> Gen.list (Range.linear 0 5)
            let! propDItems = Gen.string (Range.linear 0 20) Gen.alphaNum |> Gen.list (Range.linear 0 5)

            let model : Example.Model = {
                PropA = propA
                PropB = propB
                PropC = IndexList.ofList propCItems
                PropD = IndexList.ofList propDItems
                PropE = { Prop0 = "nested" }
                PropF = None
            }

            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create model
            let encoded = Example.Codec.encode amodel

            Y.Doc.materialize doc encoded
            let dematerialized = Y.Doc.dematerialize doc

            // The dematerialized root must be an AMap, and each field must have
            // the correct element kind regardless of the specific values
            match dematerialized with
            | Element.AMap root ->
                let items = AMap.force root
                match HashMap.tryFind "propA" items with
                | Some (Some (Element.Value _)) -> ()
                | v -> failwith $"propA should be Element.Value, got %A{v}"
                match HashMap.tryFind "propB" items with
                | None | Some None | Some (Some (Element.Value _)) -> ()
                | v -> failwith $"propB should be Element.Value or absent, got %A{v}"
                match HashMap.tryFind "propC" items with
                | Some (Some (Element.AList _)) -> ()
                | v -> failwith $"propC should be Element.AList, got %A{v}"
                match HashMap.tryFind "propD" items with
                | Some (Some (Element.AList _)) -> ()
                | v -> failwith $"propD should be Element.AList, got %A{v}"
                match HashMap.tryFind "propE" items with
                | Some (Some (Element.AMap _)) -> ()
                | v -> failwith $"propE should be Element.AMap, got %A{v}"
            | _ -> failwith "dematerialized root should be Element.AMap"
        }

        testCase "materialize preserves list item order" <| fun _ -> Property.check <| property {
            // List ordering is a fundamental invariant: Y.Array is ordered so
            // items must survive materialize/dematerialize in the original order
            let! items = Gen.string (Range.linear 0 20) Gen.alphaNum |> Gen.list (Range.linear 0 10)

            let model : Example.Model = {
                PropA = ""
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.ofList items
                PropE = { Prop0 = "" }
                PropF = None
            }

            let doc = Y.Doc.Create ()
            let amodel = Example.AdaptiveModel.Create model
            let encoded = Example.Codec.encode amodel

            Y.Doc.materialize doc encoded
            let element = Y.Doc.dematerialize doc
            let decoded = Example.Codec.decode () ([], element) |> AVal.force

            match decoded with
            | Ok result ->
                Expect.equal (IndexList.toList result.PropD) items "List item order must be preserved"
            | Error errors ->
                failwith $"Decoding failed: %A{errors}"
        }
    ]

    // Plan 0002, Step 4 — Y.Doc.connect for a single top-level text root. This is
    // the #83 headline scenario proven at the connect layer, before any
    // withYlmish rewire: connect get-or-creates the shared text root (A1) and
    // wires bi-directional delta sync, so concurrent edits CRDT-merge.
    testList "connect" [
        test "two docs connected to the same text root interleave concurrent edits (A1)" {
            // Each peer encodes an object { body: <text> } via Encode.text.
            let s1 = cval ""
            let s2 = cval ""
            let enc1 : Encoded<Element<string>> = Encode.object [ "body", Encode.text s1 ]
            let enc2 : Encoded<Element<string>> = Encode.object [ "body", Encode.text s2 ]

            let d1 = Y.Doc.Create ()
            let d2 = Y.Doc.Create ()
            use _ = Y.Doc.connect d1 enc1
            use _ = Y.Doc.connect d2 enc2

            // Concurrent local edits, expressed the MVU way (whole-string sets),
            // with no pre-sync between the peers.
            transact (fun () -> s1.Value <- "AAA")
            transact (fun () -> s2.Value <- "BBB")

            // Exchange updates both ways.
            Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)
            Y.applyUpdate (d1, Y.encodeStateAsUpdate d2)

            let r1 = (d1.getText "body").toString ()
            let r2 = (d2.getText "body").toString ()
            Expect.equal r1 r2 "docs must converge"
            Expect.isTrue (r1.Contains "AAA" && r1.Contains "BBB")
                "both peers' edits survive — connect wires CRDT merge, not last-writer-wins"
        }

        test "connect handles multiple text fields, each its own root" {
            let title1 = cval ""
            let body1 = cval ""
            let title2 = cval ""
            let body2 = cval ""
            let enc1 : Encoded<Element<string>> =
                Encode.object [ "title", Encode.text title1; "body", Encode.text body1 ]
            let enc2 : Encoded<Element<string>> =
                Encode.object [ "title", Encode.text title2; "body", Encode.text body2 ]

            let d1 = Y.Doc.Create ()
            let d2 = Y.Doc.Create ()
            use _ = Y.Doc.connect d1 enc1
            use _ = Y.Doc.connect d2 enc2

            transact (fun () -> title1.Value <- "AAA")
            transact (fun () -> body2.Value <- "BBB")

            Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)
            Y.applyUpdate (d1, Y.encodeStateAsUpdate d2)

            // Each field is an independent root that converges on its own.
            Expect.equal ((d1.getText "title").toString ()) "AAA" "title root converges on d1"
            Expect.equal ((d2.getText "title").toString ()) "AAA" "title root converges on d2"
            Expect.equal ((d1.getText "body").toString ()) "BBB" "body root converges on d1"
            Expect.equal ((d2.getText "body").toString ()) "BBB" "body root converges on d2"
        }

        test "disposing the connection tears down sync (A4 lifecycle)" {
            let s = cval ""
            let enc : Encoded<Element<string>> = Encode.object [ "body", Encode.text s ]
            // Reach into the encoded tree for the backing clist so we can observe
            // whether the decode direction is still live after disposal.
            let bodyChars =
                match AVal.force enc with
                | Some (Element.AMap m) ->
                    match AMap.force m |> HashMap.tryFind "body" with
                    | Some (Some (Element.Text c)) -> c
                    | _ -> failwith "expected a body text field"
                | _ -> failwith "expected a top-level object"

            let d = Y.Doc.Create ()
            let disposable = Y.Doc.connect d enc
            let yt = d.getText "body"

            // Before disposal: a remote-style Y.Text edit reaches the backing clist.
            yt.insert (0, "X")
            Expect.equal (System.String.Concat bodyChars) "X" "decode direction is live before disposal"

            disposable.Dispose ()

            // After disposal: further Y.Text edits must NOT reach the clist.
            yt.insert (1, "Y")
            Expect.equal (System.String.Concat bodyChars) "X" "decode direction is torn down after disposal"
            Expect.equal (yt.toString ()) "XY" "the Y.Text itself still updates (only our observer is gone)"
        }

        test "connect flattens text nested in an object to a dotted root name" {
            let s = cval ""
            let enc : Encoded<Element<string>> =
                Encode.object [ "doc", Encode.object [ "body", Encode.text s ] ]
            let d = Y.Doc.Create ()
            use _ = Y.Doc.connect d enc
            transact (fun () -> s.Value <- "nested")
            // The default flat scheme names the nested-text root by its path.
            Expect.equal ((d.getText "doc.body").toString ()) "nested"
                "nested text is flattened to the root 'doc.body'"
        }

        test "a custom Scheme controls root names (consumer seam)" {
            // A consumer-defined scheme that namespaces every root under "app/".
            let scheme : Scheme = { RootName = fun path -> "app/" + Scheme.flat.RootName path }
            let s = cval ""
            let enc : Encoded<Element<string>> = Encode.object [ "body", Encode.text s ]
            let d = Y.Doc.Create ()
            use _ = Y.Doc.connectWith scheme d enc
            transact (fun () -> s.Value <- "hi")
            Expect.equal ((d.getText "app/body").toString ()) "hi"
                "text is stored under the custom scheme's root name"
            Expect.equal ((d.getText "body").toString ()) ""
                "the default-named root is unused under the custom scheme"
        }
    ]
]
