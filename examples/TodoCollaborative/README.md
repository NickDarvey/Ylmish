# TodoCollaborative — a prioritised, collaborative todo list

A worked example of what Ylmish is *for*: a todo app whose **Elmish loop reads
like a textbook todo app**, while Ylmish quietly handles two things that are
genuinely hard — keeping replicas in sync, and letting the **persisted schema
evolve** while old and new peers edit the same document.

Run it (two real processes that relay Yjs updates over IPC):

```
npm run demo
```

You'll see concurrent adds where **both survive**, two peers editing the **same
todo's text** and both edits landing, completion toggles converging, a
**reorder** that merges, and a **v1-authored todo loading under the v2 schema**.

## The shape

| File | Role |
|---|---|
| `Model.fs` | The Elmish loop: model, `update`, `view`. **No `Yjs` reference.** |
| `Ordering.fs` | Fractional-index ordering (the consumer's concern, not Ylmish's). |
| `Codec.fs` | The sync + schema boundary: how the model maps to mergeable Yjs. |
| `Main.fs` | Wires the program to a `Y.Doc` via `Program.withYlmish`. |
| `Migration.fs` | A vignette: a v1-authored doc loading in the v2 codec. |
| `Demo.fs` | The runnable two-peer harness and scripted scenario. |

## The readable loop (`Model.fs`)

```fsharp
type Todo  = { Id : TodoId; Text : string; Done : bool; Order : string }
type Model = { Todos : Todo list; NewItem : string; Filter : Filter }

type Msg =
    | SetNewItem of string
    | Add of TodoId
    | Edit of TodoId * string
    | Toggle of TodoId
    | Move of id: TodoId * prev: TodoId option * next: TodoId option
    | Remove of TodoId
    | SetFilter of Filter
```

`update` is pure; the view derives the displayed list by **sorting on `Order`**.
Nothing here knows the model is synchronised — that is the whole point.

## Prioritisation is a field, not a position (`Ordering.fs`)

A todo's place in the list is a **fractional-index key** (`Order`). Reordering is
a single field write: `Move` sets the moved item's key to one *between* its new
neighbours (`generateKeyBetween`, from the `fractional-indexing` library). Two
peers reordering *different* items each make one independent write that merges;
the list is recovered by sorting. Ylmish does not own ordering — it just syncs the
key as a per-item value. (Array position would be wrong: a CRDT array has no native
move, so concurrent moves duplicate.)

## The sync story lives in the codec (`Codec.fs`)

`todos` is an element-wise **`Encode.collection`** keyed by the immutable `Id`:

| Field | Merge |
|---|---|
| membership (add/remove) | element-wise — concurrent adds both survive |
| `text` | character-level CRDT (per-item `Y.Text`) |
| `done`, `order` | per-id last-writer-wins |

`NewItem` and `Filter` are local per-peer UI state and are deliberately **not**
synced. None of this leaks into `Model.fs`.

## The codec is also the schema-migration boundary

A collaborative document has **no single migration moment** — peers on different
schema versions edit it at the same time. So the codec handles both shapes at once.
v2 renamed the completion flag `done` → `completed`; the codec:

- **reads both** — `completed` (v2), falling back to `done` (v1); and
- **dual-writes both** — so a still-running v1 peer keeps seeing the state.

Once every peer is on v2, drop the `done` write and the fallback. `Migration.fs`
shows a v1-authored todo loading correctly under this v2 codec. This is the value:
schema change is expressed in *one place*, declaratively, and old/new peers coexist.

## What to watch in the demo

- **Concurrent adds** (`milk` on A, `walk the dog` on B) → both appear on both.
- **Concurrent text edits** to todo 1 (`buy milk` vs `milk (2%)`) → they merge.
- **Toggles** of different todos → both completions stick.
- **Reorder** (prioritise todo 2) → converges on both peers.
- **Migration vignette** → a v1 todo (`done` key) loads in v2.
