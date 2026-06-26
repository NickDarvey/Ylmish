//99c6f229-9be9-ce29-8632-476a85dd0840
//5aa86b30-090d-49ec-50af-14d7e6035676
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
type AdaptiveTodoModel(value : TodoModel) =
    let _Items_ = FSharp.Data.Adaptive.clist(value.Items)
    let _NewItem_ = FSharp.Data.Adaptive.cval(value.NewItem)
    let _Note_ = FSharp.Data.Adaptive.cval(value.Note)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : TodoModel) = AdaptiveTodoModel(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : TodoModel) -> AdaptiveTodoModel(value)) (fun (adaptive : AdaptiveTodoModel) (value : TodoModel) -> adaptive.Update(value))
    member __.Update(value : TodoModel) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<TodoModel>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _Items_.Value <- value.Items
            _NewItem_.Value <- value.NewItem
            _Note_.Value <- value.Note
    member __.Current = __adaptive
    member __.Items = _Items_ :> FSharp.Data.Adaptive.alist<Microsoft.FSharp.Core.string>
    member __.NewItem = _NewItem_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
    member __.Note = _Note_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>

