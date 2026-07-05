module TodoCollaborative.Codec

open FSharp.Data.Adaptive
open Ylmish.Codec

let encode (amodel : AdaptiveTodoModel) : Encoded =
    Encode.object [
        "items", Encode.list Value.Encode.string amodel.Items
        "newItem", Encode.string amodel.NewItem
    ]

let decode : Decoder<TodoModel, TodoModel> =
    Decode.object {
        let! model = Decode.ask
        let! items = Decode.object.optional "items" (Decode.list Value.Decode.string)
        let! newItem = Decode.object.optional "newItem" Decode.string
        return
            { model with
                Items = defaultArg items IndexList.empty
                NewItem = defaultArg newItem model.NewItem }
    }
