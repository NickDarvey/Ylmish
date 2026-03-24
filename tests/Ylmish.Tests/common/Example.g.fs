//faeb7074-59b6-18d2-bb45-ac1b0ebca0de
//f6dbec3f-031b-62ea-3685-40fa122835b6
#nowarn "49" // upper case patterns
#nowarn "66" // upcast is unncecessary
#nowarn "1337" // internal types
#nowarn "1182" // value is unused
namespace rec Example

open System
open FSharp.Data.Adaptive
open Adaptify
open Example
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
type AdaptiveModel(value : Model) =
    let _PropA_ = FSharp.Data.Adaptive.cval(value.PropA)
    let _PropB_ = FSharp.Data.Adaptive.cval(value.PropB)
    let _PropC_ =
        let inline __arg2 (m : AdaptiveSubmodel) (v : Submodel) =
            m.Update(v)
            m
        FSharp.Data.Traceable.ChangeableModelList(value.PropC, (fun (v : Submodel) -> AdaptiveSubmodel(v)), __arg2, (fun (m : AdaptiveSubmodel) -> m))
    let _PropD_ = FSharp.Data.Adaptive.clist(value.PropD)
    let _PropE_ = AdaptiveSubmodel(value.PropE)
    let _PropF_ =
        let inline __arg2 (o : System.Object) (v : Submodel) =
            (unbox<AdaptiveSubmodel> o).Update(v)
            o
        let inline __arg5 (o : System.Object) (v : Submodel) =
            (unbox<AdaptiveSubmodel> o).Update(v)
            o
        Adaptify.FSharp.Core.AdaptiveOption<Example.Submodel, Example.AdaptiveSubmodel, Example.AdaptiveSubmodel>(value.PropF, (fun (v : Submodel) -> AdaptiveSubmodel(v) :> System.Object), __arg2, (fun (o : System.Object) -> unbox<AdaptiveSubmodel> o), (fun (v : Submodel) -> AdaptiveSubmodel(v) :> System.Object), __arg5, (fun (o : System.Object) -> unbox<AdaptiveSubmodel> o))
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : Model) = AdaptiveModel(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : Model) -> AdaptiveModel(value)) (fun (adaptive : AdaptiveModel) (value : Model) -> adaptive.Update(value))
    member __.Update(value : Model) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<Model>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _PropA_.Value <- value.PropA
            _PropB_.Value <- value.PropB
            _PropC_.Update(value.PropC)
            _PropD_.Value <- value.PropD
            _PropE_.Update(value.PropE)
            _PropF_.Update(value.PropF)
    member __.Current = __adaptive
    member __.PropA = _PropA_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
    member __.PropB = _PropB_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.option<Microsoft.FSharp.Core.string>>
    member __.PropC = _PropC_ :> FSharp.Data.Adaptive.alist<AdaptiveSubmodel>
    member __.PropD = _PropD_ :> FSharp.Data.Adaptive.alist<Microsoft.FSharp.Core.string>
    member __.PropE = _PropE_
    member __.PropF = _PropF_ :> FSharp.Data.Adaptive.aval<Adaptify.FSharp.Core.AdaptiveOptionCase<Submodel, AdaptiveSubmodel, AdaptiveSubmodel>>
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
type AdaptiveSubmodel(value : Submodel) =
    let _Prop0_ = FSharp.Data.Adaptive.cval(value.Prop0)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : Submodel) = AdaptiveSubmodel(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : Submodel) -> AdaptiveSubmodel(value)) (fun (adaptive : AdaptiveSubmodel) (value : Submodel) -> adaptive.Update(value))
    member __.Update(value : Submodel) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<Submodel>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _Prop0_.Value <- value.Prop0
    member __.Current = __adaptive
    member __.Prop0 = _Prop0_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>

