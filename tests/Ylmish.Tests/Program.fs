module Ylmish.Program

//open Expect.Elmish
open Elmish
open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

module Example =
    open Example


    let program (opts : {| Init : _; Doc: _; Encode : _; Decode : _ |} )=
        Program.mkSimple (fun () -> opts.Init) Model.update Model.view
        |> Program.withYlmish {
            Create = AdaptiveModel.Create
            Update = fun a b -> a.Update b
            Encode = opts.Encode
            Decode = opts.Decode
            Doc = opts.Doc
        }
        |> Program.test

    let dispatch (dispatcher : Program.ElmishDispatcher<_,_>) msg =
        dispatcher.Dispatch <| Ylmish.Program.Message.User msg


let tests = testList "Program" [
    test "withYlmish persists initial value" {
        let doc = Y.Doc.Create ()
        let value = "initial"
        use dispatcher = Example.program {|
            Init = {
                PropA = value
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            // or encoded = Element<'a where 'a = _ option aval>
            Encode = fun m -> Encode.object [
                "propA", m.PropA |> Encode.value id
            ]
            Decode = Decode.object {
                let! propA = Decode.object.required "propA" Decode.value
                return {
                    PropA = propA
                    PropB = None
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        //Promise.awaitAnimationFrame ()

        Expect.equal (value) (dispatcher.Model.PropA) "Model value"
        Expect.equal (Some value) (doc.getMap().get("propA")) "Y.Doc value"
        
    }

    test "withYlmish restores value" {
        let doc = Y.Doc.Create ()
        let value = doc.getMap().set("propA", "persisted")
        use dispatcher = Example.program {|
            Init = {
                PropA = "initial"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propA", m.PropA |> Encode.value id
            ]
            Decode = Decode.object {
                let! propA = Decode.object.required "propA" Decode.value
                return {
                    PropA = propA
                    PropB = None
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        //Promise.awaitAnimationFrame ()

        Expect.equal (value) (dispatcher.Model.PropA) "Model value"
        Expect.equal (Some value) (doc.getMap().get("propA")) "Y.Doc value"       
    }

    test "withYlmish restores optional value" {
        let doc = Y.Doc.Create ()
        let value = doc.getMap().set("propB", "persisted")
        use dispatcher = Example.program {|
            Init = {
                PropA = "unused"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propB", m.PropB |> Encode.option
            ]
            Decode = Decode.object {
                let! propB = Decode.object.optional "propB" Decode.value
                return {
                    PropA = "unused"
                    PropB = propB
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        //Promise.awaitAnimationFrame ()

        Expect.equal (Some value) (dispatcher.Model.PropB) "Model value"
        Expect.equal (Some value) (doc.getMap().get("propB")) "Y.Doc value"       
    }

    test "withYlmish persists updated value" {
        let doc = Y.Doc.Create ()
        let value = "initial"
        let value' = "updated"
        use dispatcher = Example.program {|
            Init = {
                PropA = value
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propA", m.PropA |> Encode.value id
            ]
            Decode = Decode.object {
                let! propA = Decode.object.required "propA" Decode.value
                return {
                    PropA = propA
                    PropB = None
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        Example.dispatch dispatcher <| Example.SetPropA value'

        //Promise.awaitAnimationFrame ()

        Expect.equal (value') (dispatcher.Model.PropA) "Model value"
        Expect.equal (Some value') (doc.getMap().get("propA")) "Y.Doc value"       
    }

    test "withYlmish persists initial optional value" {
        let doc = Y.Doc.Create ()
        let value = "initial"
        use dispatcher = Example.program {|
            Init = {
                PropA = "unused"
                PropB = Some value
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propB", m.PropB |> Encode.option
            ]
            Decode = Decode.object {
                let! propB = Decode.object.optional "propB" Decode.value
                return {
                    PropA = "unused"
                    PropB = propB
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        //Promise.awaitAnimationFrame ()

        Expect.equal (Some value) (dispatcher.Model.PropB) "Model value"
        Expect.equal (Some value) (doc.getMap().get("propB")) "Y.Doc value"       
    }

    test "withYlmish persists updated none value" {
        let doc = Y.Doc.Create ()
        use dispatcher = Example.program {|
            Init = {
                PropA = "unused"
                PropB = Some "initial"
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propB", m.PropB |> Encode.option
            ]
            Decode = Decode.object {
                let! propB = Decode.object.optional "propB" Decode.value
                return {
                    PropA = "unused"
                    PropB = propB
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        Example.dispatch dispatcher <| Example.SetPropB None

        Expect.equal None (dispatcher.Model.PropB) "Model value"
        Expect.equal None (doc.getMap().get("propB")) "Y.Doc value"       
    }

    test "withYlmish persists initial list of objects" {
        let doc = Y.Doc.Create ()
        let value = "test"
        //let propCitem0 = doc.getMap()
        //let _ = propCitem0.set("prop0", value)
        //let propC = doc.getArray()
        //let _ = propC.push(Array.singleton propCitem0)
        //let _ = doc.getMap().set("propC", propC)
        use dispatcher = Example.program {|
            Init = {
                PropA = "unused"
                PropB = None
                PropC = IndexList.single { Prop0 = value }
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propC", m.PropC |> Encode.listWith <| fun m -> Encode.object [
                    "prop0", m.Prop0 |> Encode.value id
                ]
            ]
            Decode = Decode.object {
                let! propC = Decode.object.required "propC" (Decode.list.required <| Decode.object {
                    let! prop0 = Decode.object.required "prop0" Decode.value
                    return {
                        Example.Submodel.Prop0 = prop0
                    }
                })
                return {
                    PropA = "unused"
                    PropB = None
                    PropC = propC
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        //Promise.awaitAnimationFrame ()

        let root : Y.Map<Y.Array<Y.Map<string>>> = doc.getMap ()
        Expect.equal value (dispatcher.Model.PropC[0].Prop0) "Model value"
        Expect.equal (Some value) (root.get("propC").Value.get(0).get("prop0")) "Y.Doc value"       
    }

    test "withYlmish persists updated list of objects" {
        let doc = Y.Doc.Create ()
        let item1 : Example.Submodel = { Prop0 = "item-1" }
        let item2 : Example.Submodel = { Prop0 = "item-2" }

        use dispatcher = Example.program {|
            Init = {
                PropA = "unused"
                PropB = None
                PropC = IndexList.single item1
                PropD = IndexList.empty
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propC", m.PropC |> Encode.listWith <| fun m -> Encode.object [
                    "prop0", m.Prop0 |> Encode.value id
                ]
            ]
            Decode = Decode.object {
                let! propC = Decode.object.required "propC" (Decode.list.required <| Decode.object {
                    let! prop0 = Decode.object.required "prop0" Decode.value
                    return {
                        Example.Submodel.Prop0 = prop0
                    }
                })
                return {
                    PropA = "unused"
                    PropB = None
                    PropC = propC
                    PropD = IndexList.empty
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        Example.dispatch dispatcher <| Example.AddPropC item2
        Example.dispatch dispatcher <| Example.RemPropC item1

        let root : Y.Map<Y.Array<Y.Map<string>>> = doc.getMap ()
        Expect.equal item2.Prop0 (dispatcher.Model.PropC[0].Prop0) "Model value"
        Expect.equal (Some item2.Prop0) (root.get("propC").Value.get(0).get("prop0")) "Y.Doc value"
    }

    test "withYlmish persists initial list of values" {
        let doc = Y.Doc.Create ()
        let value = "test"
        //let propCitem0 = doc.getMap()
        //let _ = propCitem0.set("prop0", value)
        //let propC = doc.getArray()
        //let _ = propC.push(Array.singleton propCitem0)
        //let _ = doc.getMap().set("propC", propC)
        use dispatcher = Example.program {|
            Init = {
                PropA = "unused"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.single value
                PropE = { Prop0 = "not-used" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propD", m.PropD |> Encode.list (fun s -> Encode.value id (AVal.constant s))
            ]
            Decode = Decode.object {
                let! propD = Decode.object.required "propD" (Decode.list.required Decode.value)
                return {
                    PropA = "unused"
                    PropB = None
                    PropC = IndexList.empty
                    PropD = propD
                    PropE = { Prop0 = "not-used" }
                    PropF = None
                }
            }
        |}

        //Promise.awaitAnimationFrame ()

        let root : Y.Map<Y.Array<string>> = doc.getMap ()
        Expect.equal value (dispatcher.Model.PropD[0]) "Model value"
        Expect.equal value (root.get("propD").Value.get(0)) "Y.Doc value"       
    }

    test "withYlmish persists initial object" {
        let doc = Y.Doc.Create ()
        let value : Example.Submodel = { Prop0 = "initial" }
        use dispatcher = Example.program {|
            Init = {
                PropA = "not-used"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = value
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propE", Encode.object [
                    "prop0", m.PropE.Prop0 |> Encode.value id
                ]
            ]
            Decode = Decode.object {
                let! propE = Decode.object.required "propE" <| Decode.object {
                    let! prop0 = Decode.object.required "prop0" Decode.value
                    return {
                        Example.Prop0 = prop0
                    }
                }
                return {
                    PropA = "not-used"
                    PropB = None
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = propE
                    PropF = None
                }
            }
        |}

        //Promise.awaitAnimationFrame ()
        
        let root : Y.Map<Y.Map<string>> = doc.getMap ()
        Expect.equal (value.Prop0) (dispatcher.Model.PropE.Prop0) "Model value"
        Expect.equal (Some value.Prop0) (root.get("propE").Value.get("prop0")) "Y.Doc value"
        
    }

    test "withYlmish persists updated object" {
        let doc = Y.Doc.Create ()
        let value : Example.Submodel = { Prop0 = "updated" }
        use dispatcher = Example.program {|
            Init = {
                PropA = "not-used"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "initial" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propE", Encode.object [
                    "prop0", m.PropE.Prop0 |> Encode.value id
                ]
            ]
            Decode = Decode.object {
                let! propE = Decode.object.required "propE" <| Decode.object {
                    let! prop0 = Decode.object.required "prop0" Decode.value
                    return {
                        Example.Prop0 = prop0
                    }
                }
                return {
                    PropA = "not-used"
                    PropB = None
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = propE
                    PropF = None
                }
            }
        |}

        Example.dispatch dispatcher <| Example.SetPropE value
        
        let root : Y.Map<Y.Map<string>> = doc.getMap ()
        Expect.equal (value.Prop0) (dispatcher.Model.PropE.Prop0) "Model value"
        Expect.equal (Some value.Prop0) (root.get("propE").Value.get("prop0")) "Y.Doc value"
        
    }

    test "withYlmish falls back to init model when Y.Doc has incompatible state" {
        let doc = Y.Doc.Create ()
        // Pre-populate Y.Doc with an incompatible shape: a string value where
        // the decoder expects a nested object (propE -> { prop0: string }).
        doc.getMap().set("propE", "not-an-object") |> ignore

        use dispatcher = Example.program {|
            Init = {
                PropA = "init-value"
                PropB = None
                PropC = IndexList.empty
                PropD = IndexList.empty
                PropE = { Prop0 = "init-prop0" }
                PropF = None
            }
            Doc = doc
            Encode = fun m -> Encode.object [
                "propE", Encode.object [
                    "prop0", m.PropE.Prop0 |> Encode.value id
                ]
            ]
            Decode = Decode.object {
                let! propE = Decode.object.required "propE" <| Decode.object {
                    let! prop0 = Decode.object.required "prop0" Decode.value
                    return {
                        Example.Prop0 = prop0
                    }
                }
                return {
                    PropA = "init-value"
                    PropB = None
                    PropC = IndexList.empty
                    PropD = IndexList.empty
                    PropE = propE
                    PropF = None
                }
            }
        |}

        // Should fall back to init model since decode fails
        Expect.equal "init-value" (dispatcher.Model.PropA) "Model PropA should be from init"
        Expect.equal "init-prop0" (dispatcher.Model.PropE.Prop0) "Model PropE.Prop0 should be from init"
        // Y.Doc should be re-materialized with the init model's encoded data
        let root : Y.Map<Y.Map<string>> = doc.getMap ()
        Expect.equal (Some "init-prop0") (root.get("propE").Value.get("prop0")) "Y.Doc should be re-materialized"
    }

]