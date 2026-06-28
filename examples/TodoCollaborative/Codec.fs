module TodoCollaborative.Codec

open FSharp.Data.Adaptive
open Ylmish.Adaptive.Codec

// Step 0 codec: a straightforward *structural* mapping so the app compiles and
// persists/syncs round-trip. It is intentionally NOT yet collaborative at the
// collection level — that arrives in Step 3, when `todos` becomes an element-wise
// `Encode.collection` (concurrent adds merge) with per-item CRDT text. `Filter` is
// local view state and is deliberately not persisted.

// Every scalar leaf is `Element<string>` (the codec's value type is uniform per
// object, and materialize/connect work over `Element<string>`), so `Done : bool`
// is encoded as a string and parsed back on decode.
let private todoEncode (t : AdaptiveTodo) = Encode.object [
    "id",    t.Id    |> Encode.value id
    "text",  t.Text  |> Encode.value id
    "done",  t.Done  |> Encode.value (fun b -> if b then "true" else "false")
    "order", t.Order |> Encode.value id
]

let encode (amodel : AdaptiveTodoModel) = Encode.object [
    "todos",   Encode.list todoEncode amodel.Todos
    "newItem", amodel.NewItem |> Encode.value id
]

let private todoDecode : Decoder<_, _, Todo> = Decode.object {
    let! id    = Decode.object.required "id" Decode.value
    let! text  = Decode.object.required "text" Decode.value
    let! doneStr = Decode.object.required "done" Decode.value
    let! order = Decode.object.required "order" Decode.value
    return { Id = id; Text = text; Done = (doneStr = "true"); Order = order }
}

let decode : Decoder<TodoModel, _, TodoModel> = Decode.object {
    // `Filter` is local, not in the doc — keep the current model's value via `ask`.
    let! current = Decode.object.ask ()
    let! todos = Decode.object.required "todos" (Decode.list.required todoDecode)
    let! newItem = Decode.object.required "newItem" Decode.value
    return {
        Todos = todos
        NewItem = newItem
        Filter = current.Filter
    }
}
