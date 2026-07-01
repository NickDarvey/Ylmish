namespace TodoCollaborative

open Adaptify
open FSharp.Data.Adaptive

// A collaborative, *prioritised* todo list. This file is the Elmish loop and
// nothing else — there is no `Yjs` reference here. The model is plain immutable
// F# records; messages name intentions; `update` is pure. How it syncs and how
// the persisted shape evolves are the codec's job (Codec.fs), kept out of the loop.

/// A stable, immutable per-item identity (a guid). It is the **map key** — the
/// item's identity lives in the container's type, not in a field.
type TodoId = string

/// Local-only view state: which todos to show. Not synced (it's per-peer UI).
type Filter =
    | All
    | Active
    | Completed

type Msg =
    | SetNewItem of string
    /// Add the current `NewItem` under the given fresh id (the caller mints the
    /// guid, so `update` stays pure and testable).
    | Add of TodoId
    | Edit of TodoId * string
    | Toggle of TodoId
    /// Reorder: place `id` between its new neighbours `prev` and `next` (the ids
    /// either side of where it was dropped; `None` = the list end).
    | Move of id: TodoId * prev: TodoId option * next: TodoId option
    | Remove of TodoId
    | SetFilter of Filter

and [<ModelType>] Todo = {
    Text : string
    Done : bool
    /// Fractional-index key; the list is displayed in ascending `Order`.
    Order : string
}

and [<ModelType>] TodoModel = {
    /// Keyed by id — a `HashMap` says "collaborative, element-wise" in the type.
    Todos : HashMap<TodoId, Todo>
    NewItem : string
    Filter : Filter
}

module TodoModel =
    let init = {
        Todos = HashMap.empty
        NewItem = ""
        Filter = All
    }

    /// `(id, todo)` pairs in display (priority) order: ascending fractional-index
    /// `Order`, ties broken by `id` so the order is total and identical on every peer.
    let ordered (model : TodoModel) : (TodoId * Todo) list =
        model.Todos
        |> HashMap.toList
        |> List.sortBy (fun (id, t) -> t.Order, id)

    /// The todos a peer would actually render, after applying its local `Filter`.
    let visible (model : TodoModel) : (TodoId * Todo) list =
        ordered model
        |> List.filter (fun (_, t) ->
            match model.Filter with
            | All -> true
            | Active -> not t.Done
            | Completed -> t.Done)

    let private orderOf (model : TodoModel) (id : TodoId) : string option =
        model.Todos |> HashMap.tryFind id |> Option.map (fun t -> t.Order)

    let private change (id : TodoId) (f : Todo -> Todo) (model : TodoModel) =
        { model with Todos = model.Todos |> HashMap.alter id (Option.map f) }

    let update (msg : Msg) (model : TodoModel) : TodoModel =
        match msg with
        | SetNewItem value ->
            { model with NewItem = value }
        | Add id ->
            let lastOrder = ordered model |> List.tryLast |> Option.map (fun (_, t) -> t.Order)
            let todo = { Text = model.NewItem; Done = false; Order = Ordering.keyBetween lastOrder None }
            { model with Todos = model.Todos |> HashMap.add id todo; NewItem = "" }
        | Edit (id, text) ->
            model |> change id (fun t -> { t with Text = text })
        | Toggle id ->
            model |> change id (fun t -> { t with Done = not t.Done })
        | Move (id, prev, next) ->
            let key = Ordering.keyBetween (prev |> Option.bind (orderOf model)) (next |> Option.bind (orderOf model))
            model |> change id (fun t -> { t with Order = key })
        | Remove id ->
            { model with Todos = model.Todos |> HashMap.remove id }
        | SetFilter f ->
            { model with Filter = f }

    let view _ _ = ()
