//1d2172d4-88af-f894-80a9-769a9203d9dc
//5d310652-f0db-7558-89f4-a82087ca69bd
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
    let _Note_ = FSharp.Data.Adaptive.cval(value.Note)
    let _Todos_ = FSharp.Data.Adaptive.cmap(value.Todos)
    let _Theme_ = FSharp.Data.Adaptive.cval(value.Theme)
    let _Hits_ = FSharp.Data.Adaptive.cval(value.Hits)
    let _Draft_ = FSharp.Data.Adaptive.cval(value.Draft)
    let mutable __value = value
    let __adaptive = FSharp.Data.Adaptive.AVal.custom((fun (token : FSharp.Data.Adaptive.AdaptiveToken) -> __value))
    static member Create(value : TodoModel) = AdaptiveTodoModel(value)
    static member Unpersist = Adaptify.Unpersist.create (fun (value : TodoModel) -> AdaptiveTodoModel(value)) (fun (adaptive : AdaptiveTodoModel) (value : TodoModel) -> adaptive.Update(value))
    member __.Update(value : TodoModel) =
        if Microsoft.FSharp.Core.Operators.not((FSharp.Data.Adaptive.ShallowEqualityComparer<TodoModel>.ShallowEquals(value, __value))) then
            __value <- value
            __adaptive.MarkOutdated()
            _Note_.Value <- value.Note
            _Todos_.Value <- value.Todos
            _Theme_.Value <- value.Theme
            _Hits_.Value <- value.Hits
            _Draft_.Value <- value.Draft
    member __.Current = __adaptive
    member __.Note = _Note_ :> FSharp.Data.Adaptive.aval<Ylmish.Text>
    member __.Todos = _Todos_ :> FSharp.Data.Adaptive.amap<Microsoft.FSharp.Core.string, Todo>
    member __.Theme = _Theme_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>
    member __.Hits = _Hits_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.int>
    member __.Draft = _Draft_ :> FSharp.Data.Adaptive.aval<Microsoft.FSharp.Core.string>

