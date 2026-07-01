module TodoCollaborative.Codec

open FSharp.Data.Adaptive
open Ylmish.Adaptive.Codec

// The codec is the whole sync story — and it stays out of the Elmish loop. `Todos`
// is an element-wise `Encode.map` keyed by the model's map key: concurrent
// adds/removes MERGE (no lost items), each todo's `text` merges character-by-
// character, and `done` / `order` are per-id last-writer-wins. An item is just an
// object. `NewItem` and `Filter` are local per-peer state and are not synced.

// --- Schema evolution is the consumer's job (Ylmish ships no migration helpers).
// v2 renamed the completion flag `done` -> `completed`. A CRDT document has no
// single migration moment, so the codec reads BOTH keys (prefer `completed`) and
// dual-writes both (so a v1 peer still sees the state). Drop `done` once all peers
// are v2. This is ordinary object-codec code — no special support.

let private encodeTodo (t : AdaptiveTodo) = Encode.object [
    "completed", Encode.bool t.Done      // v2 name
    "done",      Encode.bool t.Done      // v1 name (dual-write for coexistence)
    "order",     t.Order |> Encode.value id
    "text",      Encode.text t.Text
]

let private decodeTodo : Decoder<_, _, Todo> = Decode.object {
    let! completed = Decode.object.optional "completed" Decode.bool   // v2
    let! legacy    = Decode.object.optional "done" Decode.bool         // v1 fallback
    let! order = Decode.object.required "order" Decode.value
    let! text  = Decode.object.required "text" Decode.text
    return {
        Done = completed |> Option.orElse legacy |> Option.defaultValue false
        Order = order
        Text = text
    }
}

let encode (amodel : AdaptiveTodoModel) = Encode.object [
    "todos", Encode.map encodeTodo amodel.Todos
]

let decode : Decoder<TodoModel, _, TodoModel> = Decode.object {
    // NewItem and Filter are local per-peer state, not synced; keep them via `ask`.
    let! current = Decode.object.ask ()
    let! todos = Decode.object.required "todos" (Decode.map decodeTodo)
    return {
        Todos = todos
        NewItem = current.NewItem
        Filter = current.Filter
    }
}
