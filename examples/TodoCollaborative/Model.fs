namespace TodoCollaborative

open Adaptify
open FSharp.Data.Adaptive

type Msg =
    | AddItem of string
    | RemoveItem of string
    | SetNewItem of string
    | SetNote of string

and [<ModelType>] TodoModel = {
    Items : IndexList<string>
    NewItem : string
    // A collaboratively-edited free-text note. Encoded with Encode.text, so
    // concurrent edits from different peers CRDT-merge (interleave) rather than
    // clobbering last-writer-wins.
    Note : string
}

module TodoModel =
    let init = {
        Items = IndexList.empty
        NewItem = ""
        Note = ""
    }

    let update msg model =
        match msg with
        | AddItem item ->
            { model with Items = model.Items.Add item; NewItem = "" }
        | RemoveItem item ->
            { model with Items = model.Items.Remove item }
        | SetNewItem value ->
            { model with NewItem = value }
        | SetNote value ->
            { model with Note = value }

    let view _ _ = ()
