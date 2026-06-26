module TodoCollaborative.Codec

open FSharp.Data.Adaptive
open Ylmish.Adaptive.Codec

let encode (amodel : AdaptiveTodoModel) = Encode.object [
    "items", Encode.list (fun s -> Encode.value id (AVal.constant s)) amodel.Items
    "newItem", amodel.NewItem |> Encode.value id
    // Collaborative text: character-level CRDT merge instead of last-writer-wins.
    "note", amodel.Note |> Encode.text
]

let decode : Decoder<TodoModel, _, TodoModel> = Decode.object {
    // `note` lives in its own Y.Text root (connect), so it is absent from the
    // structural root map that raw `dematerialize` reads. Decode it optionally
    // and fall back to the current model's note via `ask`; under `withYlmish`
    // the live tree always carries it, so the merged text is what we read.
    let! current = Decode.object.ask ()
    let! items = Decode.object.required "items" (Decode.list.required Decode.value)
    let! newItem = Decode.object.required "newItem" Decode.value
    let! note = Decode.object.optional "note" Decode.text
    return {
        Items = items
        NewItem = newItem
        Note = note |> Option.defaultValue current.Note
    }
}
