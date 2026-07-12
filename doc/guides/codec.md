# The codec

The codec is where you describe **which parts of your model sync, and how each
part merges**. It is the anti-corruption layer between your app schema (the
Elmish model, free to change every release) and your state schema (the Y.Doc,
shared with peers you don't control and versions of your app you no longer
ship). See the README's *Background → Schema and codec* for why the two must
be decoupled.

Every code block in this guide is a verbatim excerpt of compiled code; the
source file is named above each block.

## One word per field is the merge choice

The whole taxonomy is in the README. In practice a codec is an `Encode.object`
whose fields each pick a combinator, and a `Decode.object` computation
expression that mirrors it. The demo app's codec, entire
([`examples/TodoCollaborative/Codec.fs`](../../examples/TodoCollaborative/Codec.fs)):

```fsharp
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
```

Things to notice:

- **The model type never appears in the doc.** Rename a record field, keep the
  string key, and old and new clients keep interoperating. When you *do* want
  to rename a key, see the dual-key recipe in [recipes.md](recipes.md).
- **The encoder walks live adaptive views** (`amodel.Todos`, `amodel.Theme`),
  so the runtime observes *deltas* — a one-character insert into a todo's note
  ships as a one-character splice, not a re-upload of the note.
- **The decoder is total.** Errors are path-tracked values, not exceptions; a
  malformed or newer-versioned doc never crashes the loop (`withYlmish` routes
  errors to your `OnError` and keeps the current model).

## `Decode.ask` and app-only state

`Decode.ask` returns the **current model** from the decoder's Reader
environment. That is how app-only state survives remote updates: the demo
model's `Draft` field is mentioned by neither `encode` nor `decode`, so a
remote `Set` rebuilds the model with `{ model with ... }` — carrying `Draft`
through untouched, and never writing it to the doc (the demo shows this end
to end).

The same shape gives you **decode-empty = init** for free: `withYlmish` never
writes at startup, it *decodes* the doc with your init model in the
environment. On an empty doc every `Decode.object.optional` comes back `None`,
every `defaultArg` picks your default, and the result *is* your init state —
the same code path as decoding a populated doc. There is no separate
"first run" logic to get wrong.

## Registers and the honest LWW

`Encode.string` / `int` / `float` / `bool` are last-writer-wins registers.
Two honest facts about them, both demonstrated in the demo and pinned by the
stress suite:

- The concurrent tiebreak is **deterministic, not temporal** (Yjs breaks ties
  by clientID). "Last writer" does not mean "most recent wall clock".
- **Only content changes write.** Setting a register to its current value
  emits nothing — you cannot "touch" an unchanged field to win LWW. This is
  also why a one-field edit of a keyed item cannot clobber a peer's concurrent
  edit to a *different* field of the same item.

## Lists hold values — by construction

`Encode.list` takes a `Value.Encoder<'a>`, a deliberately narrower type than
the codec's `Encoded`: primitives (`Value.Encode.string/int/float/bool`) and
domain types riding them. There is no injection from `Encoded` into
`Value.Encoder`, so a list of `Text` or a list of objects is a **compile
error**, not a runtime surprise
([`tests/Ylmish.Tests/Codec.fs`](../../tests/Ylmish.Tests/Codec.fs)):

```fsharp
// The lists-hold-values restriction is TYPE-LEVEL, so there is no runtime
// test for it; this is the should-not-compile record:
//
//     Encode.list Encode.text texts      // ✗ Encode.text : aval<Text> -> Encoded
//                                        //   is not a Value.Encoder<'a>
//     Encode.list (fun t -> Encode.object []) xs   // ✗ same reason
//
// There is no injection from Encoded into Value.Encoder, so lists hold
// primitives only; entities belong in Encode.map (keyed by identity).
```

Anything with identity belongs in `Encode.map`, keyed by an app-minted id —
that is what makes concurrent (and offline) creation safe, and it is where
per-item field merges live. See [recipes.md](recipes.md).

A domain type rides a primitive via `contramap`/`map`
([`tests/Ylmish.Tests/Codec.fs`](../../tests/Ylmish.Tests/Codec.fs)):

```fsharp
type TodoId = TodoId of string

module TodoId =
    let valueEncoder = Value.Encode.contramap (fun (TodoId s) -> s) Value.Encode.string
    let valueDecoder = Value.Decode.map TodoId Value.Decode.string
```

## Optional fields

`Encode.option` encodes presence as key-presence: `None` is an absent key
(deleted on the Some→None transition; delete beats concurrent edits inside),
`Some` is whatever the inner combinator writes. From the north-star model,
where `Note : Text option`
([`tests/Ylmish.Tests/NorthStar.fs`](../../tests/Ylmish.Tests/NorthStar.fs)):

```fsharp
module Codec =
    let encode (am : AdaptiveModel) : Encoded =
        Encode.object [
            "body", Encode.text am.Body
            "note", Encode.option Encode.text am.Note
            "todos", Encode.map (fun (title : string) -> Encode.string (AVal.constant title)) am.Todos
        ]

    let decode : Decoder<Model, Model> =
        Decode.object {
            let! model = Decode.ask
            let! body = Decode.object.optional "body" Decode.text
            let! note = Decode.object.optional "note" Decode.text
            let! todos = Decode.object.optional "todos" (Decode.map Decode.string)
            return
                { model with
                    Body = defaultArg body Text.empty
                    Note = note
                    Todos = defaultArg todos HashMap.empty }
        }
```

Note the decode side: an optional *encoded* field is just
`Decode.object.optional` returning `'a option` — there is no separate
`Decode.option`.

## Errors are values with paths

Decoding accumulates every failure rather than stopping at the first, and each
error carries the path to the offending element, innermost-first
([`tests/Ylmish.Tests/Codec.fs`](../../tests/Ylmish.Tests/Codec.fs)):

```fsharp
test "item errors accumulate, each with its index" {
    let e = Encode.list Value.Encode.string (AList.ofList [ "x"; "y" ])
    match decodeVia () (Decode.list Value.Decode.int) e with
    | Error [ UnexpectedValue (p0, _); UnexpectedValue (p1, _) ] ->
        Expect.equal p0 [ ArrayIndex 0 ] "first item's path"
        Expect.equal p1 [ ArrayIndex 1 ] "second item's path"
    | r -> failwithf "expected two indexed errors, got %A" r
}
```

## Unknown keys are never yours to delete

The binding only touches keys your encoder mentions. A doc key written by a
newer schema, an older schema, or another app entirely survives a whole
session untouched — that is what makes rolling migrations possible at all
(see the migration recipe in [recipes.md](recipes.md)).
