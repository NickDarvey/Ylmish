module TodoCollaborative.Codec

open FSharp.Data.Adaptive
open Ylmish.Adaptive.Codec

let encode (amodel : AdaptiveTodoModel) = Encode.object [
    "items", Encode.list (fun s -> Encode.value id (AVal.constant s)) amodel.Items
    "newItem", amodel.NewItem |> Encode.value id
]

let decode : Decoder<TodoModel, _, TodoModel> = Decode.object {
    let! items = Decode.object.required "items" (Decode.list.required Decode.value)
    let! newItem = Decode.object.required "newItem" Decode.value
    return {
        Items = items
        NewItem = newItem
    }
}
