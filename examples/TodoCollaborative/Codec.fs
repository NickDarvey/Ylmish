module TodoCollaborative.Codec

open FSharp.Data.Adaptive
open Ylmish.Adaptive.Codec

// The codec is the whole sync story — and it stays out of the Elmish loop. `todos`
// is an element-wise, id-keyed `Encode.collection`: concurrent adds/removes MERGE
// (no lost items), each todo's `text` merges character-by-character, and `done` /
// `order` are per-id last-writer-wins. `NewItem` and `Filter` are local per-peer
// state and are deliberately not synced.

/// Project a todo to its CRDT shape (keyed by the immutable id).
let private toItem (t : AdaptiveTodo) : aval<CollectionItem> = adaptive {
    let! id = t.Id
    let! text = t.Text
    let! isDone = t.Done
    let! order = t.Order
    return {
        Id = id
        Fields = [ "done", (if isDone then "true" else "false"); "order", order ]
        Texts = [ "text", text ]
    }
}

let private fieldOf (ci : CollectionItem) name def =
    ci.Fields |> List.tryFind (fst >> (=) name) |> Option.map snd |> Option.defaultValue def

let private toTodo (ci : CollectionItem) : Todo =
    let text = ci.Texts |> List.tryFind (fst >> (=) "text") |> Option.map snd |> Option.defaultValue ""
    { Id = ci.Id
      Text = text
      Done = (fieldOf ci "done" "false" = "true")
      Order = fieldOf ci "order" "" }

/// Build the encoder. The `merged` cell is shared with `decode` (the converged
/// collection is written there by the collection binding and read back here),
/// exactly like `Encode.custom` / `Decode.custom`.
let encode (merged : cval<CollectionItem list>) (amodel : AdaptiveTodoModel) = Encode.object [
    "todos", Encode.collection [ "text" ] toItem merged amodel.Todos
]

let decode (merged : cval<CollectionItem list>) : Decoder<TodoModel, _, TodoModel> = Decode.object {
    let! current = Decode.object.ask ()
    let! items = Decode.object.required "todos" (Decode.collection merged)
    return {
        Todos = items |> List.map toTodo |> IndexList.ofList
        NewItem = current.NewItem
        Filter = current.Filter
    }
}
