namespace TodoCollaborative

open Adaptify
open FSharp.Data.Adaptive

type Msg =
    | AddItem of string
    | RemoveItem of string
    | SetNewItem of string

and [<ModelType>] TodoModel = {
    Items : IndexList<string>
    NewItem : string
}

module TodoModel =
    let init = {
        Items = IndexList.empty
        NewItem = ""
    }

    let update msg model =
        match msg with
        | AddItem item ->
            { model with Items = model.Items.Add item; NewItem = "" }
        | RemoveItem item ->
            { model with Items = model.Items.Remove item }
        | SetNewItem value ->
            { model with NewItem = value }

    let view _ _ = ()
