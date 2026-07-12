module TodoCollaborative.Codec

// The schema, decoupled from the model: one word per field is the merge
// choice. Draft appears in neither direction — app-only state never reaches
// the doc (it survives remote updates through `Decode.ask`).
//
// Quoted verbatim by README.md (quickstart) and doc/guides/codec.md.

open FSharp.Data.Adaptive

open Ylmish
open Ylmish.Codec

/// Per-field encoding of one todo: three independent registers plus a
/// collaborative note under the item's key, so concurrent edits to different
/// fields of the same todo both stick (only fields whose content changed are
/// written) and concurrent edits to the same note interleave.
let private todo (t : Todo) : Encoded =
    Encode.object [
        "title", Encode.string (AVal.constant t.Title)
        "done", Encode.bool (AVal.constant t.Done)
        "order", Encode.float (AVal.constant t.Order)
        "note", Encode.text (AVal.constant t.Note)
    ]

let encode (counter : GrowOnlyCounter) (amodel : AdaptiveTodoModel) : Encoded =
    Encode.object [
        "todos", Encode.map todo amodel.Todos
        "theme", Encode.string amodel.Theme
        "hits", Encode.custom counter
    ]

let private decodeTodo : Decoder<TodoModel, Todo> =
    Decode.object {
        let! title = Decode.object.required "title" Decode.string
        let! isDone = Decode.object.required "done" Decode.bool
        let! order = Decode.object.required "order" Decode.float
        let! note = Decode.object.optional "note" Decode.text
        return { Title = title; Done = isDone; Order = order; Note = defaultArg note Text.empty }
    }

let decode : Decoder<TodoModel, TodoModel> =
    Decode.object {
        let! model = Decode.ask
        let! todos = Decode.object.optional "todos" (Decode.map decodeTodo)
        let! theme = Decode.object.optional "theme" Decode.string
        let! hits = Decode.object.required "hits" Decode.custom
        return
            { model with
                Todos = defaultArg todos HashMap.empty
                Theme = defaultArg theme model.Theme
                Hits = hits }
    }
