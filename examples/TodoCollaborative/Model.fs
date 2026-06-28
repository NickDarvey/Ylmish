namespace TodoCollaborative

open Adaptify
open FSharp.Data.Adaptive

// A collaborative, *prioritised* todo list. This file is the Elmish loop and
// nothing else — there is no `Yjs` reference here. The model is plain immutable
// F# records; messages name intentions; `update` is pure. How it syncs and how
// the persisted shape evolves are the codec's job (Codec.fs), kept entirely out
// of the loop. That separation is the point.

/// A stable, immutable per-item identity (a guid). Item identity must never
/// change — ordering is a *separate* concern (the fractional-index `Order` key).
type TodoId = string

/// Local-only view state: which todos to show. Not synced (it's per-peer UI).
type Filter =
    | All
    | Active
    | Completed

type Msg =
    | SetNewItem of string
    /// Add the current `NewItem` as a todo with the given fresh id (the caller
    /// mints the guid, so `update` stays pure and testable).
    | Add of TodoId
    | Edit of TodoId * string
    | Toggle of TodoId
    /// Reorder: place `id` between its new neighbours `prev` and `next` (the ids
    /// either side of where it was dropped; `None` = the list end). The view knows
    /// the rendered order, so it supplies the neighbours.
    | Move of id: TodoId * prev: TodoId option * next: TodoId option
    | Remove of TodoId
    | SetFilter of Filter

and [<ModelType>] Todo = {
    Id : TodoId
    Text : string
    Done : bool
    /// Fractional-index key; the list is displayed in ascending `Order`.
    Order : string
}

and [<ModelType>] TodoModel = {
    Todos : IndexList<Todo>
    NewItem : string
    Filter : Filter
}

module TodoModel =
    let init = {
        Todos = IndexList.empty
        NewItem = ""
        Filter = All
    }

    /// Todos in display (priority) order: ascending fractional-index `Order`, ties
    /// broken by `Id` so the order is total and identical on every peer.
    let ordered (model : TodoModel) : Todo list =
        model.Todos
        |> IndexList.toList
        |> List.sortBy (fun t -> t.Order, t.Id)

    /// The todos a peer would actually render, after applying its local `Filter`.
    let visible (model : TodoModel) : Todo list =
        ordered model
        |> List.filter (fun t ->
            match model.Filter with
            | All -> true
            | Active -> not t.Done
            | Completed -> t.Done)

    let private orderOf (model : TodoModel) (id : TodoId) : string option =
        model.Todos |> IndexList.toList |> List.tryPick (fun t -> if t.Id = id then Some t.Order else None)

    let private mapId (id : TodoId) (f : Todo -> Todo) (model : TodoModel) =
        { model with Todos = model.Todos |> IndexList.map (fun t -> if t.Id = id then f t else t) }

    let update (msg : Msg) (model : TodoModel) : TodoModel =
        match msg with
        | SetNewItem value ->
            { model with NewItem = value }
        | Add id ->
            // Append: a key after the current last item.
            let lastOrder = ordered model |> List.tryLast |> Option.map (fun t -> t.Order)
            let todo = { Id = id; Text = model.NewItem; Done = false; Order = Ordering.keyBetween lastOrder None }
            { model with Todos = model.Todos.Add todo; NewItem = "" }
        | Edit (id, text) ->
            model |> mapId id (fun t -> { t with Text = text })
        | Toggle id ->
            model |> mapId id (fun t -> { t with Done = not t.Done })
        | Move (id, prev, next) ->
            let key = Ordering.keyBetween (prev |> Option.bind (orderOf model)) (next |> Option.bind (orderOf model))
            model |> mapId id (fun t -> { t with Order = key })
        | Remove id ->
            { model with Todos = model.Todos |> IndexList.toList |> List.filter (fun t -> t.Id <> id) |> IndexList.ofList }
        | SetFilter f ->
            { model with Filter = f }

    let view _ _ = ()
