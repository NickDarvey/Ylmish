//f2e8c9d8-5c67-9754-1cad-24fea827b021
//012b150a-838c-735a-69df-47281a08c060
#nowarn "49" // upper case patterns
#nowarn "66" // upcast is unncecessary
#nowarn "1337" // internal types
#nowarn "1182" // value is unused
namespace rec TodoCollaborative

open System
open FSharp.Data.Adaptive
open Adaptify
open TodoCollaborative
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
type AdaptiveTodo(value : Todo) =
    let _Id_ = FSharp.Data.Adaptive.cval(value.Id)
    let _Text_ = FSharp.Data.Adaptive.cval(value.Text)
    let _Done_ = FSharp.Data.Adaptive.cval(value.Done)
    let _Order_ = FSharp.Data.Adaptive.cval(value.Order)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : Todo) = AdaptiveTodo(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : Todo) -> AdaptiveTodo(value)) (fun (adaptive : AdaptiveTodo) (value : Todo) -> adaptive.Update(value))
    member __.Update(value : Todo) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<Todo>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _Id_.Value <- value.Id
            _Text_.Value <- value.Text
            _Done_.Value <- value.Done
            _Order_.Value <- value.Order
    member __.Current = __adaptive
    member __.Id = _Id_ :> FSharp.Data.Adaptive.aval<TodoId>
    member __.Text = _Text_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
    member __.Done = _Done_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.bool>
    member __.Order = _Order_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
[<System.Diagnostics.CodeAnalysis.SuppressMessage("NameConventions", "*")>]
type AdaptiveTodoModel(value : TodoModel) =
    let _Todos_ =
        let inline __arg2 (m : AdaptiveTodo) (v : Todo) =
            m.Update(v)
            m
        FSharp.Data.Traceable.ChangeableModelList(value.Todos, (fun (v : Todo) -> AdaptiveTodo(v)), __arg2, (fun (m : AdaptiveTodo) -> m))
    let _NewItem_ = FSharp.Data.Adaptive.cval(value.NewItem)
    let _Filter_ = FSharp.Data.Adaptive.cval(value.Filter)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : TodoModel) = AdaptiveTodoModel(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : TodoModel) -> AdaptiveTodoModel(value)) (fun (adaptive : AdaptiveTodoModel) (value : TodoModel) -> adaptive.Update(value))
    member __.Update(value : TodoModel) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<TodoModel>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _Todos_.Update(value.Todos)
            _NewItem_.Value <- value.NewItem
            _Filter_.Value <- value.Filter
    member __.Current = __adaptive
    member __.Todos = _Todos_ :> FSharp.Data.Adaptive.alist<AdaptiveTodo>
    member __.NewItem = _NewItem_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
    member __.Filter = _Filter_ :> FSharp.Data.Adaptive.aval<Filter>

