namespace TodoCollaborative

open Adaptify
open Elmish
open FSharp.Data.Adaptive

open Ylmish

// Quoted verbatim by README.md (quickstart) and doc/guides/recipes.md.
/// One todo. A plain record of independent registers: because the codec
/// encodes each field separately (see Codec.fs), concurrent edits to
/// DIFFERENT fields of the same todo merge per field. `Order` is a fractional
/// index — reordering writes a number instead of moving structure, so
/// concurrent reorders converge without duplication.
type Todo = { Title : string; Done : bool; Order : float }

type Msg =
    | EditNote of (Text -> Text)
    | AddTodo of id : string * title : string * order : float
    | Rename of id : string * title : string
    | SetDone of id : string * value : bool
    | Reorder of id : string * order : float
    | RemoveTodo of id : string
    | SetTheme of string
    | Bump
    | SetDraft of string

/// The model's type IS the merge choice: Text merges interleaved, the keyed
/// map merges element-wise (app-minted ids make offline creation safe), Theme
/// is an honest last-writer-wins register, Hits comes back through the
/// escape-hatch counter, and Draft is app-only — the codec never mentions it,
/// so it never syncs.
[<ModelType>]
type TodoModel = {
    Note : Text
    Todos : HashMap<string, Todo>
    Theme : string
    Hits : int
    Draft : string
}

module TodoModel =
    let init = {
        Note = Text.empty
        Todos = HashMap.empty
        Theme = "light"
        Hits = 0
        Draft = ""
    }

    // The Bump case below is quoted verbatim by doc/guides/custom-elements.md.
    let private updateTodo id f todos =
        match HashMap.tryFind id todos with
        | Some t -> HashMap.add id (f t) todos
        | None -> todos

    let update (counter : GrowOnlyCounter) msg model =
        match msg with
        | EditNote f -> { model with Note = f model.Note }, Cmd.none
        | AddTodo (id, title, order) ->
            { model with Todos = model.Todos |> HashMap.add id { Title = title; Done = false; Order = order } }, Cmd.none
        | Rename (id, title) ->
            { model with Todos = model.Todos |> updateTodo id (fun t -> { t with Title = title }) }, Cmd.none
        | SetDone (id, value) ->
            { model with Todos = model.Todos |> updateTodo id (fun t -> { t with Done = value }) }, Cmd.none
        | Reorder (id, order) ->
            { model with Todos = model.Todos |> updateTodo id (fun t -> { t with Order = order }) }, Cmd.none
        | RemoveTodo id -> { model with Todos = HashMap.remove id model.Todos }, Cmd.none
        | SetTheme theme -> { model with Theme = theme }, Cmd.none
        | Bump ->
            // Optimistic local increment; the effect pushes a tick through the
            // counter binding, and the authoritative (summed) count returns
            // through Decode.custom on remote transactions.
            { model with Hits = model.Hits + 1 }, Cmd.ofEffect (fun _ -> counter.Bump ())
        | SetDraft value -> { model with Draft = value }, Cmd.none

    let view _ _ = ()
