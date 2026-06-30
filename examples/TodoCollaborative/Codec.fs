module TodoCollaborative.Codec

open FSharp.Data.Adaptive
open Ylmish.Adaptive.Codec

// The codec is the whole sync story — and it stays out of the Elmish loop. `todos`
// is an element-wise, id-keyed `Encode.collection`: concurrent adds/removes MERGE
// (no lost items), each todo's `text` merges character-by-character, and `done` /
// `order` are per-id last-writer-wins. `NewItem` and `Filter` are local per-peer
// state and are deliberately not synced.

// --- Schema evolution: the codec is the migration boundary -------------------
//
// Schema v2 renamed the completion flag `done` -> `completed`. A collaborative
// CRDT document has NO single migration moment: v1 and v2 peers edit it at the
// same time. So the codec handles both shapes at once:
//   * the decoder READS BOTH — `completed` (v2), falling back to `done` (v1);
//   * the encoder DUAL-WRITES both keys, so a still-running v1 peer keeps seeing
//     the completion state.
// Once every peer is on v2 the `done` write (and the fallback) can be dropped.

// Ylmish ships no migration helpers — handling schema change is the consumer's
// job (it may become a library module later). Here we do it by hand: read both the
// new and old key, and dual-write both, all in plain F#.
let private fieldOpt (ci : CollectionItem) name =
    ci.Fields |> List.tryFind (fst >> (=) name) |> Option.map snd

/// Project a todo to its CRDT shape (keyed by the immutable id).
let private toItem (t : AdaptiveTodo) : aval<CollectionItem> = adaptive {
    let! id = t.Id
    let! text = t.Text
    let! isDone = t.Done
    let! order = t.Order
    let doneStr = if isDone then "true" else "false"
    return {
        Id = id
        // Dual-write `completed` (v2) and `done` (v1) so v1 peers still read it.
        Fields = [ "completed", doneStr; "done", doneStr; "order", order ]
        Texts = [ "text", text ]
    }
}

let private toTodo (ci : CollectionItem) : Todo =
    let text = ci.Texts |> List.tryFind (fst >> (=) "text") |> Option.map snd |> Option.defaultValue ""
    // Read `completed` (v2), falling back to `done` (v1) for older docs/peers.
    let completed = fieldOpt ci "completed" |> Option.orElse (fieldOpt ci "done") |> Option.defaultValue "false"
    { Id = ci.Id
      Text = text
      Done = (completed = "true")
      Order = fieldOpt ci "order" |> Option.defaultValue "" }

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
