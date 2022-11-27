module FSharp.Data.Adaptive.Codec

open Fable.Mocha
open FSharp.Data.Adaptive
open FSharp.Data.Adaptive.Codec
open Hedgehog

module private Example =

    // [<ModelType>]
    type Thing =
        {
            name  : string
            value : int
        }

    module Thing =
        let gen = gen {
            let! name = Gen.string (Range.linear 0 255) Gen.alphaNum
            let! value = Gen.int32 (Range.linearBounded ())
            return {
                name = name
                value = value
            }
        }

    // [<ModelType>]
    type Model =
        {
            foo : int
            bar : string
            things : IndexList<Thing>
        }

    [<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
    type AdaptiveThing(value : Thing) =
        let _name_ = FSharp.Data.Adaptive.cval(value.name)
        let _value_ = FSharp.Data.Adaptive.cval(value.value)
        let mutable __value = value
        let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (_ : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
        static member Create(value : Thing) = AdaptiveThing(value)
        // static member Unpersist = Adaptify.Unpersist.create (fun (value : Thing) -> AdaptiveThing(value)) (fun (adaptive : AdaptiveThing) (value : Thing) -> adaptive.Update(value))
        member __.Update(value : Thing) =
            if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<Thing>.ShallowEquals(value, __value))) then
                __value <- value
                __adaptive.MarkOutdated()
                _name_.Value <- value.name
                _value_.Value <- value.value
        member __.Current = __adaptive
        member __.name = _name_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
        member __.value = _value_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.int>

    [<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
    type AdaptiveModel(value : Model) =
        let _foo_ = FSharp.Data.Adaptive.cval(value.foo)
        let _bar_ = FSharp.Data.Adaptive.cval(value.bar)
        let _things_ =
            let inline __arg2 (m : AdaptiveThing) (v : Thing) =
                m.Update(v)
                m
            FSharp.Data.Traceable.ChangeableModelList(value.things, (fun (v : Thing) -> AdaptiveThing(v)), __arg2, (fun (m : AdaptiveThing) -> m))
        let mutable __value = value
        let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (_ : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
        static member Create(value : Model) = AdaptiveModel(value)
        // static member Unpersist = Adaptify.Unpersist.create (fun (value : Model) -> AdaptiveModel(value)) (fun (adaptive : AdaptiveModel) (value : Model) -> adaptive.Update(value))
        member __.Update(value : Model) =
            if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<Model>.ShallowEquals(value, __value))) then
                __value <- value
                __adaptive.MarkOutdated()
                _foo_.Value <- value.foo
                _bar_.Value <- value.bar
                _things_.Update(value.things)
        member __.Current = __adaptive
        member __.foo = _foo_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.int>
        member __.bar = _bar_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
        member __.things = _things_ :> FSharp.Data.Adaptive.alist<AdaptiveThing>

    module Codec =
        module Things =
            let encode (athing : AdaptiveThing) = Encode.object [
                "name", Encode.value athing.name
                "value", Encode.value athing.value
            ]

            let decode : Decoder<Thing> = Decode.object {
                let! name = Decode.object.key ("name", Decode.value'<string>)
                let! value = Decode.object.key ("value", Decode.value'<int>)
                return {
                    name = name
                    value = value
                }
            }

        let encode (amodel : AdaptiveModel) = Encode.object [
            "foo", Encode.value amodel.foo
            "bar", Encode.value amodel.bar
            "things", Encode.list Things.encode amodel.things
        ]

        let decode : Decoder<Model> = Decode.object {
            let! things = Decode.object.key ("things", Decode.list Things.decode)
            let! foo = Decode.object.key ("foo", Decode.value'<int>)
            let! bar = Decode.object.key ("bar", Decode.value'<string>)
            return {
                things = things
                foo = foo
                bar = bar
            }
        }


module private Decode =
    let inline force decoder encoded =
        Decode.run decoder encoded
        |> Decoded.mapError Error.printAll
        |> AVal.force
        |> function
        | Ok value -> value
        | Error e -> invalidOp e

let tests = testList "FSharp.Data.Adaptive.Codec" [
    test "roundtrips" {
        let example : Example.Thing = {
            name = "Example Thing"
            value = 42
        }
        let actual =
            example
            |> Example.AdaptiveThing
            |> Example.Codec.Things.encode
            |> Decode.force Example.Codec.Things.decode

        Expect.equal actual example ""
    }
    testCase "idk" <| fun _ -> Property.check <| property {
        let! model = Example.Thing.gen |> Gen.map Example.AdaptiveThing
        let model' =
            model
            |> Example.Codec.Things.encode
            |> Decode.run Example.Codec.Things.decode
            |> Decoded.mapError Error.printAll
            |> AVal.map (function
            | Ok r -> r
            | Error e -> invalidOp e)
        
        let value1 = AVal.force model'
        let value2 = AVal.force model.Current

        Expect.equal value1 value2 ""
        // do this https://github.com/hedgehogqa/fsharp-hedgehog/pull/196/files#diff-6203087c4a62052414dbe7e20c77af63ede65fc3ec4815c473c3c5ea8781cccc
    }
]