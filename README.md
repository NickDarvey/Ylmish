# Ylmish

[![.github/workflows/build.yml](https://github.com/primacydotco/Ylmish/actions/workflows/build.yml/badge.svg)](https://github.com/primacydotco/Ylmish/actions/workflows/build.yml)

Real-time, collaborative apps with a delightful programming model.

Here lie libraries for integrating [Yjs](https://github.com/yjs/yjs) and [Elmish](https://github.com/elmish/elmish), via [Fable](https://github.com/fable-compiler/fable) and [Adaptive](https://github.com/fsprojects/FSharp.Data.Adaptive).

> 😸 I like building apps with Elmish, I like sharing state with Yjs. Let's conjugate!
>
> I want (select) changes to an Elmish model to propagate to a Yjs document, and changes to a Yjs document to reflect in the Elmish model.

Your model stays a plain immutable record. Your `update` stays pure. You write a small codec saying which fields sync and how each one merges — then `Program.withYlmish` binds the loop to a `Y.Doc`: local updates flow out as precise CRDT deltas, remote transactions flow in as ordinary messages, and concurrent edits **merge** instead of clobbering.

## Quickstart

The snippets below are from the working example in [`examples/TodoCollaborative`](examples/TodoCollaborative); every code sample in these docs is an excerpt of compiled code in `examples/` or `tests/`.

The model is a plain immutable record. Each field's type picks its merge behaviour (see the table below); the only Ylmish type here is the note's `Text`.

<!-- sample: todo-record -->
```fsharp
/// One todo. A record of independent registers plus a collaborative note:
/// because the codec encodes each field separately (see Codec.fs), concurrent
/// edits to DIFFERENT fields of the same todo merge per field — and concurrent
/// edits to the SAME note merge as text. `Order` is a fractional index —
/// reordering writes a number instead of moving structure, so concurrent
/// reorders converge without duplication.
type Todo = { Title : string; Done : bool; Order : float; Note : Text }
```

<!-- sample: todo-model -->
```fsharp
/// The model's type IS the merge choice: the keyed map merges element-wise
/// (app-minted ids make offline creation safe) and each todo's Note merges as
/// collaborative text, Theme is an honest last-writer-wins register, Hits
/// comes back through the escape-hatch counter, and Draft is app-only — the
/// codec never mentions it, so it never syncs.
[<ModelType>]
type TodoModel = {
    Todos : HashMap<string, Todo>
    Theme : string
    Hits : int
    Draft : string
}
```

`[<ModelType>]` is [Adaptify](https://github.com/krauthaufen/Adaptify)'s attribute. It generates the incremental `AdaptiveTodoModel` companion Ylmish uses to observe deltas between successive models.

The codec names the fields that sync and picks one combinator per field — including inside each keyed item. `Draft` is not mentioned, so it never syncs.

<!-- sample: quickstart-encode -->
```fsharp
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
```

<!-- sample: quickstart-decode -->
```fsharp
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

`Decode.ask` returns the current model, so app-only fields survive remote updates; an empty doc decodes to your init state through the same code path (`withYlmish` writes nothing at startup). See [doc/guides/codec.md](doc/guides/codec.md).

`Program.withYlmish` binds the loop to a `Y.Doc`:

<!-- sample: quickstart-main -->
```fsharp
/// Create a Program.withYlmish-wired Elmish program for a given Y.Doc. Each
/// peer owns one counter binding — created here so update's Bump effect and
/// the codec share the instance.
let makeProgram (doc : Y.Doc) =
    let counter = GrowOnlyCounter ()
    Program.mkProgram (fun () -> TodoModel.init, Cmd.none) (TodoModel.update counter) TodoModel.view
    |> Program.withYlmish {
        Doc = doc
        Create = AdaptiveTodoModel.Create
        Update = fun a b -> a.Update b
        Encode = Codec.encode counter
        Decode = Codec.decode
        OnError = Program.OnError.log
    }
```

Each local update becomes one origin-tagged Y transaction (text as splices, keyed items per key, registers as sets); each remote transaction becomes one `Set` message carrying the re-decoded model; your own writes never echo back. Syncing the `Y.Doc` is up to you — y-websocket, WebRTC, or explicit, as the demo does.

## The model's type is the merge choice

| Model field | Combinator | Backed by | Concurrent behaviour |
| --- | --- | --- | --- |
| `Ylmish.Text` | `Encode.text` | `Y.Text` | splices interleave and merge — nobody's keystrokes lost |
| `string`/`int`/`float`/`bool` (or a domain type riding one via `Value.Encode.contramap`) | `Encode.string`/`int`/`float`/`bool` (`Encode.value` in general) | map entry | last-writer-wins register, deterministic tiebreak |
| record | `Encode.object` | `Y.Map` | per-field merge of whatever each field chose |
| `HashMap<string, 'T>` | `Encode.map` | `Y.Map` | element-wise by key: different items never conflict; per-item fields merge per their own encodings — **the shape for anything creatable offline** |
| `IndexList<'a>` (values only) | `Encode.list` | `Y.Array` | insert/delete merge, diff-reconciled; items are `Value` primitives *by construction* — entities with identity belong in `Encode.map` |
| `'a option` | `Encode.option` | key presence | `None` = absent key; Some→None deletes it |
| any subtree | `Encode.atomic` | one plain value | deliberate whole-subtree last-writer-wins |
| anything | `Encode.custom` | a Yjs type you bind | consumer-defined — see [the escape hatch](doc/guides/custom-elements.md) |

### The honest limits

CRDTs make choices, and Ylmish would rather you read them here than discover them in production. Each is demonstrated live in the [demo](#demo):

- **Last-writer-wins is a deterministic tiebreak, not a clock.** When two peers write the same register concurrently, one wins by Yjs's clientID tiebreak — "last" does not mean "most recent".
- **Anything creatable offline needs a unique key.** If two offline peers create the *same-keyed* nested container and sync, one creation wins wholesale. Model entities in `Encode.map` under app-minted ids and the situation cannot arise ([the recipe](doc/guides/recipes.md)).
- **Delete beats concurrent edits inside.** Removing a keyed item wins over a peer's simultaneous edit within it; the edit cannot resurrect the item.
- **Structural moves can duplicate — so order is data, not structure.** A list "move" is delete+insert and two concurrent moves of the same item can duplicate it; use a fractional `Order` field instead — a reorder is one register write.
- **Only content changes write.** Re-setting a field to its current value emits nothing; you cannot "touch" a field to win LWW.

## Layers and dependencies

```
public   ┌────────────────────────────────────────────────────────────┐
         │ Ylmish.Program.withYlmish     Elmish integration           │
         │ Ylmish.Codec (Encode/Decode)  schema + merge policy        │
         │ Ylmish.Text                   mergeable text value         │
         │ Ylmish.Codec.CustomElement    escape hatch (Yjs verbatim)  │
         ├────────────────────────────────────────────────────────────┤
internal │ Ylmish.Internal.Binding       encoded tree ↔ Y.Doc         │
         │ Ylmish.Internal.Delta         list-delta application       │
         └────────────────────────────────────────────────────────────┘
```

Dependency posture: **Elmish and Yjs are public vocabulary** — Yjs deliberately so, in the escape hatch, where `BindContext` hands you real `Y.Text`/`Y.Map`/`Y.Array` instances. **FSharp.Data.Adaptive is plumbing**: it appears on the public surface only at the Adaptify seam (`Create`/`Update` in the options record) and is being squeezed inward over time.

## Background

### Data and communication

Our **app data** describes what is in memory while our app is running. Our **state data** describes what is persisted in browser storage and synchronized with peers.

Changes need to be communicated bi-directionally. That is, any changes made to **state data**, in Yjs, need to be made to **app data**, in Elmish, and (some) changes made to app data (such as through interactions with the app) need to be made to state data.

If we were to observe a running [**Elmish** loop](https://elmish.github.io/elmish/#dispatch-loop), we wouldn't see the operations being applied to the app's model. They're opaque to an observer because they're inside the `update` functions. Instead, we only have access to each successive `'model`, that is, the consequence of operations.

> For example, an Elmish `'model` may contain a list and an interaction may add two items into that list, but from the outside, we only see the new list, not two 'add' operations.

If we observe a **Yjs** document, we'd see all of the operations that occur and those are shared with peers for synchronization.

> For example, a Yjs `Y.Array` and an interaction may add two items to that array and from the outside, we can observe two add operations. (And we can combine all the operations to see the 'current' state of the array.)

We need to bridge these two worlds, that is, we need to be able to go from our changes represented as successive models to our changes represented as the operations themselves.

```
'model -> 'model -> 'operations
# which looks just like a classic differencing (diffing) function...
'document -> 'document -> 'delta
```

['Incremental computation'](https://github.com/fsprojects/Fabulous/issues/258#issue-391515540) has already been used where people want to build apps that use immutable data structures but performantly update a mutable DOM. Part of this work has been to [efficiently](https://github.com/fsharp/fslang-suggestions/issues/768) diff two models.

**Design**

We bridge Elmish and Yjs through an intermediate, incremental model using [FSharp.Data.Adaptive](https://github.com/fsprojects/FSharp.Data.Adaptive) (an fsharp implementation of incremental computing) and [Adaptify](https://github.com/krauthaufen/Adaptify) (to incrementalize an existing Elmish model).

Successive Elmish models are used to update the incremental model. The (calculated) changes to the incremental model are observed and (the deltas) are applied to the Yjs document.

```
┌─Elmish─┐         ┌─Adaptive─────────┐         ┌─Yjs───┐
│        │ --[1]-> │                  │ --[2]-> │       │
│ Model  │         │ IncrementalModel │         │ Y.Doc │
│        │ <-[4]-- │                  | <-[3]-- │       │
└────────┘         └──────────────────┘         └───────┘
App data                                    State data

Using Ylmish.Program.withYlmish:
[1] Successive Elmish models are used to update the incremental model.
[2] Changes to the incremental model are observed and (the deltas) are applied to the Yjs document.
[3] Changes to the Yjs document are observed and decoded back into an Elmish model, dispatched as a message.
[4] Local and remote changes meet in the one Elmish model your view renders.
```

### Schema and codec

Our **app schema** describes the structure of our app data, that is, what is in memory while our app is running. Our **state schema** describes the structure of our state data, that is, what is persisted in browser storage and synchronized with peers via the app's companion.

Our state schema _must_ be decoupled from our app schema because:

1. Our app schema will change over time as our app changes. The state schema must be protected from breaking changes to the app schema.
   So, we need an anti-corruption layer between the two schemas.
1. Only a subset of app data needs to be persisted.
   So, we need to be able to select what should be persisted.

If we have this decoupling, we need an explicit description of how our app schema translates to our state schema and vice versa. We need to be able to _encode_ that app data as state data and _decode_ state data into app data.

**Design**

`Ylmish.Codec` is that description: `Encode.*` combinators build a live encoding over the adaptive model (so the runtime sees deltas), and a total, error-accumulating `Decode.*` reads doc state back under the current model's environment. The walkthrough is [doc/guides/codec.md](doc/guides/codec.md); migration across schema versions is a recipe in [doc/guides/recipes.md](doc/guides/recipes.md).

## Demo

`npm run demo` runs [`examples/TodoCollaborative`](examples/TodoCollaborative): two complete
`withYlmish` programs in one process, each over its own `Y.Doc`, with sync performed
explicitly between steps — so every kind of concurrency is staged deliberately and the
output below is reproducible byte for byte (clientIDs are pinned). Every step prints the
peers' **Elmish models** — what a UI would render — never the docs directly.

<!-- output: demo -->
```
TodoCollaborative — two Elmish programs, one shared document, no server.

Step 1 — an empty doc decodes to your init state
  Both peers start against empty docs. Nothing is written at startup:
  init is what an empty doc decodes to, not something to persist.
  A | theme light | hits 0 | draft ""
  B | theme light | hits 0 | draft ""

Step 2 — concurrent edits to the same todo's note interleave
  A creates the first todo, writes its note, and syncs.
  ~ sync ~
  Offline, A appends to that note while B prepends to the same note.
  before the network heals:
  A | theme light | hits 0 | draft ""
  A |   [ ] buy milk — "hello world"  (a-1, order 1)
  B | theme light | hits 0 | draft ""
  B |   [ ] buy milk — "oh, hello"  (a-1, order 1)
  ~ sync ~
  after: both edits survive, interleaved — nobody's keystrokes lost.
  A | theme light | hits 0 | draft ""
  A |   [ ] buy milk — "oh, hello world"  (a-1, order 1)
  B | theme light | hits 0 | draft ""
  B |   [ ] buy milk — "oh, hello world"  (a-1, order 1)

Step 3 — offline creation is safe under app-minted keys
  Still offline, each peer creates a todo. The ids are the app's own
  (anything creatable offline needs a unique key — that's the rule).
  ~ sync ~
  after sync: BOTH creations survive (keyed element-wise merge).
  A | theme light | hits 0 | draft "eggs too?"
  A |   [ ] buy milk — "oh, hello world"  (a-1, order 1)
  A |   [ ] walk dog — ""  (b-1, order 2)
  A |   [ ] water plants — ""  (a-2, order 3)
  B | theme light | hits 0 | draft ""
  B |   [ ] buy milk — "oh, hello world"  (a-1, order 1)
  B |   [ ] walk dog — ""  (b-1, order 2)
  B |   [ ] water plants — ""  (a-2, order 3)

Step 4 — same todo, different fields: per-field merge
  Concurrently, A ticks 'buy milk' done while B renames it.
  ~ sync ~
  after sync: both stick — a todo is a record of independent registers.
  A | theme light | hits 0 | draft "eggs too?"
  A |   [x] buy oat milk — "oh, hello world"  (a-1, order 1)
  A |   [ ] walk dog — ""  (b-1, order 2)
  A |   [ ] water plants — ""  (a-2, order 3)
  B | theme light | hits 0 | draft ""
  B |   [x] buy oat milk — "oh, hello world"  (a-1, order 1)
  B |   [ ] walk dog — ""  (b-1, order 2)
  B |   [ ] water plants — ""  (a-2, order 3)

Step 5 — same register, concurrent writes: an honest clobber
  Both flip the theme at once. A register is last-writer-wins: one value
  survives, deterministically (clientID tiebreak) — NOT 'whoever was later'.
  ~ sync ~
  A | theme sepia | hits 0 | draft "eggs too?"
  A |   [x] buy oat milk — "oh, hello world"  (a-1, order 1)
  A |   [ ] walk dog — ""  (b-1, order 2)
  A |   [ ] water plants — ""  (a-2, order 3)
  B | theme sepia | hits 0 | draft ""
  B |   [x] buy oat milk — "oh, hello world"  (a-1, order 1)
  B |   [ ] walk dog — ""  (b-1, order 2)
  B |   [ ] water plants — ""  (a-2, order 3)

Step 6 — delete beats concurrent edits inside
  A deletes 'walk dog' while B concurrently ticks it done.
  ~ sync ~
  after sync: the todo is gone on both — ticking it could not resurrect it.
  A | theme sepia | hits 0 | draft "eggs too?"
  A |   [x] buy oat milk — "oh, hello world"  (a-1, order 1)
  A |   [ ] water plants — ""  (a-2, order 3)
  B | theme sepia | hits 0 | draft ""
  B |   [x] buy oat milk — "oh, hello world"  (a-1, order 1)
  B |   [ ] water plants — ""  (a-2, order 3)

Step 7 — reordering is data, not structure
  Concurrently: A moves 'water plants' to the top (order 0.5) while B
  pushes 'buy oat milk' to the bottom (order 4). Order is a fractional
  index: a reorder writes one number, so reorders cannot duplicate items.
  ~ sync ~
  after sync: one converged order, every item exactly once.
  A | theme sepia | hits 0 | draft "eggs too?"
  A |   [ ] water plants — ""  (a-2, order 0.5)
  A |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)
  B | theme sepia | hits 0 | draft ""
  B |   [ ] water plants — ""  (a-2, order 0.5)
  B |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)

Step 8 — the escape hatch: a merge no built-in provides
  Hits is a consumer-authored counter over a raw Y.Array (see Counter.fs).
  Offline, A bumps twice and B bumps once — optimistically:
  A | theme sepia | hits 2 | draft "eggs too?"
  A |   [ ] water plants — ""  (a-2, order 0.5)
  A |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)
  B | theme sepia | hits 1 | draft ""
  B |   [ ] water plants — ""  (a-2, order 0.5)
  B |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)
  ~ sync ~
  after sync: the counts SUM — concurrent increments are all kept.
  A | theme sepia | hits 3 | draft "eggs too?"
  A |   [ ] water plants — ""  (a-2, order 0.5)
  A |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)
  B | theme sepia | hits 3 | draft ""
  B |   [ ] water plants — ""  (a-2, order 0.5)
  B |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)

Step 9 — app-only state never syncs
  A's draft has said "eggs too?" since step 3 — B never saw it, because
  the codec never mentions Draft. It is not in the doc either:
  A | theme sepia | hits 3 | draft "eggs too?"
  A |   [ ] water plants — ""  (a-2, order 0.5)
  A |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)
  B | theme sepia | hits 3 | draft ""
  B |   [ ] water plants — ""  (a-2, order 0.5)
  B |   [x] buy oat milk — "oh, hello world"  (a-1, order 4)
  A's doc, top-level register keys: [theme]
  A's doc has a 'draft' key: false

The models above are what each peer's UI renders — no peer ever read
another's memory, only Yjs updates travelled.
```

## Guides

- [doc/guides/codec.md](doc/guides/codec.md) — writing codecs: schema decoupling, `Decode.ask`, app-only state, lists vs keyed maps, options, errors.
- [doc/guides/text.md](doc/guides/text.md) — `Ylmish.Text`: semantics, clamping, `edit`'s ambiguity, when to reach for the real `Y.Text`.
- [doc/guides/custom-elements.md](doc/guides/custom-elements.md) — the escape hatch: writing a binding, the summing counter, handing a live `Y.Text` to an editor.
- [doc/guides/recipes.md](doc/guides/recipes.md) — app-minted keys for offline creation, fractional-index ordering, dual-key rolling migration.

## Developing

```bash
npm install    # restore .NET + npm dependencies
npm test       # adaptify codegen → Fable compile → Mocha tests
npm run demo   # build and run the transcript above
npm run docs   # re-inject the doc code samples and demo transcript from source
```

The code samples in this README and the guides are single-sourced from compiled code: each fenced block is injected from a marked region of `examples/` or `tests/` by [`doc/sync-samples.mjs`](doc/sync-samples.mjs), and CI fails if they drift.

Design history lives in [doc/plans](doc/plans) — [0002-ylmish-redesign.md](doc/plans/0002-ylmish-redesign.md) is the executed redesign this library implements, including the validated-assumptions table pinning Yjs's concurrency semantics. Agent/contributor conventions are in [AGENTS.md](AGENTS.md).
