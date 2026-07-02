# Ylmish

[![.github/workflows/build.yml](https://github.com/primacydotco/Ylmish/actions/workflows/build.yml/badge.svg)](https://github.com/primacydotco/Ylmish/actions/workflows/build.yml)

Real-time, collaborative apps with a delightful programming model.

Here lie libraries for integrating [Yjs](https://github.com/yjs/yjs) and [Elmish](https://github.com/elmish/elmish), via [Fable](https://github.com/fable-compiler/fable) and [Adaptive](https://github.com/fsprojects/FSharp.Data.Adaptive).

> 😸 I like building apps with Elmish, I like sharing state with Yjs. Let's conjugate!
> 
> I want (select) changes to an Elmish model to propagate to a Yjs document, and changes to a Yjs document to reflect in the Elmish model.

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
[3] Changes to the Yjs document are observed and the deltas are applied to the incremental model
[4] Changes to the incremental model are observed and a updated Elmish model is set for each.
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

We provide `Ylmish.Adaptive.Codec` for writing encoders and decoders. 

```
┌─Adaptive─────────────────────────────────┐
│ AdaptiveModel --[1]-> AMap<string, AVal> │
│                                          │
│ AVal<Model>   <-[2]-- AMap<string, AVal> |
└──────────────────────────────────────────┘
App schema                      State schema

Using Ylmish.Adaptive.Codec:
[1] .Encoder
[2] .Decoder
```

### Merge semantics

When two peers change the same field concurrently, what should happen? That is a
*per-field* decision, and you make it in the codec by choosing a combinator. The
field in your Elmish model stays plain immutable F# — a `string` is a `string`,
not a Yjs handle.

The **model's type is the merge choice** — pick the field type that matches the
merge you want, and the codec follows:

| Model field | Combinator | Yjs backing | Merge semantics |
|---|---|---|---|
| scalar | `Encode.value` / `Decode.value` (`Encode.bool` for `bool`) | `Y.Map` entry | last-writer-wins |
| `string` (prose) | `Encode.text` / `Decode.text` | `Y.Text` (own root) | character-level CRDT |
| record | `Encode.object` / `Decode.object` | `Y.Map` keys | per-field |
| `HashMap<K, Record>` | `Encode.map` / `Decode.map` | `Y.Map` + per-item `Y.Text` (own roots) | **element-wise, keyed**: add/remove merge, fields per-id LWW, text CRDT |
| `IndexList<Value>` | `Encode.sequence` / `Decode.sequence` | `Y.Array` (own root) | **CRDT sequence**: add/remove/reorder merge (no per-item edit) |
| `IndexList<_>` | `Encode.list` / `Decode.list` | `Y.Array` | whole-container LWW (small / uncontended) |
| anything | `Encode.custom` / `Decode.custom` | your choice (own root) | **you define it** |

`Encode.text : aval<string> -> _` keeps the model field a plain `string`. The
Adaptive layer recovers the character inserts/deletes by **diffing successive
string values** — the same "successive models → operations" idea this library
already applies to lists, one level down — so concurrent edits *interleave*
instead of clobbering. See [`examples/TodoCollaborative`](examples/TodoCollaborative)
for a runnable, two-peer demo (`npm run demo`) of a prioritised collaborative todo
list where concurrent adds both survive, two peers editing the *same* todo's text
both land, reordering merges, and a v1-authored document still loads under the v2
schema.

```fsharp
let encode (m : AdaptiveModel) = Encode.object [
    "title", m.Title |> Encode.value id    // last-writer-wins
    "body",  m.Body  |> Encode.text        // character-level CRDT merge
]
```

Under the hood, `Program.withYlmish` `connect`s each collaborative-text field to
its own top-level `Y.Text` root and keeps it in identity-preserving,
delta-level sync — the alternative to re-projecting the whole document on every
update, which destroyed the CRDT history that merging depends on.

**Choosing where roots live (the layout `Scheme`).** Collaborative leaves are
laid out across the document's top-level roots by a `Codec.Scheme`. The default,
`Scheme.flat`, names each root by its flattened path (e.g. `body`, `doc.body`).
Consumers who need a different persisted layout can supply their own `Scheme` to
`Y.Doc.connectWith` rather than forking the library.

*Lists: positional vs id-based naming.* For a collaborative leaf **inside a
list**, `Scheme.flat` names it by **position** (`items.2.body`). That is correct
for an append-only list, but if peers **reorder or insert concurrently** they
disagree about which item is "2", so the same root name binds to different
logical items and their edits split. Name by a **stable, immutable id** instead:

```fsharp
use _ = Y.Doc.connectWith (Scheme.byKey "id") doc encoded   // items.<id>.body
```

`Scheme.byKey "id"` reads each list item's `id` field and names its leaves
`items.<id>.body`, so the text stays with its logical item across reorder.
**The id must be immutable** (a guid minted at creation) — it names a persisted
root, so it must never change. Ordering is a *separate*, mutable concern: keep a
`order` field of ordinary state and sort by it, typically a **fractional index**
([`fractional-indexing`](https://github.com/rocicorp/fractional-indexing) —
`generateKeyBetween` gives a sort key between any two neighbours, so a reorder is
a single LWW field write). Ylmish doesn't bundle fractional indexing; bring your
own. See
[`examples/TodoCollaborative/ReorderableList.fs`](examples/TodoCollaborative/ReorderableList.fs)
(printed by `npm run demo`) for two peers holding a list in different orders whose
edits still merge onto the right item.

*Editable collections — model them as a `HashMap` (`Encode.map`).* A collection
several peers edit at once is a `HashMap<key, Record>`: the map key is the item's
identity, and the value is an ordinary record. `Encode.map` runs each value through
an **object codec** (the same `Encode.object` / `Decode.object` you'd write for any
record) and keys an element-wise `Y.Map` (+ per-item `Y.Text` roots) off the map
key. So concurrent **adds/removes merge** (no lost items), scalar fields are
**per-id LWW**, and text fields **merge character-by-character** — an item is just
an object, with no `"id"` argument and no cell to thread:

```fsharp
[<ModelType>] type Todo  = { Text : string; Done : bool; Order : string }   // key holds the id
[<ModelType>] type Model = { Todos : HashMap<TodoId, Todo>; ... }

let encodeTodo (t : AdaptiveTodo) = Encode.object [
    "done",  Encode.bool t.Done            // per-id LWW
    "order", t.Order |> Encode.value id    // per-id LWW
    "text",  Encode.text t.Text            // per-item character CRDT
]
let decodeTodo = Decode.object {
    let! isDone = Decode.object.required "done" Decode.bool
    let! order  = Decode.object.required "order" Decode.value
    let! text   = Decode.object.required "text" Decode.text
    return { Done = isDone; Order = order; Text = text }
}

"todos", Encode.map encodeTodo amodel.Todos                                  // encode
let! todos = Decode.object.required "todos" (Decode.map decodeTodo)          // decode → HashMap
```

A `HashMap` is **unordered** (identity + membership only), so a display order is a
**field** you sort by — typically a fractional index
([`fractional-indexing`](https://github.com/rocicorp/fractional-indexing):
`generateKeyBetween` gives a sort key between any two neighbours, so a reorder is
one LWW field write). Ylmish doesn't bundle it; bring your own. This layout also
sidesteps the fact that a CRDT array has no native *move*.

*Keyless value lists — `Encode.sequence`.* When the elements are plain **values**
you add/remove/reorder as a whole (tags, log lines) — not records you edit in
place — model an `IndexList<Value>` and use `Encode.sequence`: a CRDT sequence over
a `Y.Array` where concurrent inserts/removes merge, no key required (the value *is*
the content).

Under the hood both keep their state on **top-level roots** (concurrent creation
converges — A1), reconcile against live Yjs, and expose their converged value so
the decoder reads it straight off the element — no threaded cell; `withYlmish` keeps
them live via a whole-document remote-transaction read-back. The approach was
derived rigorously in plan [0006](doc/plans/0006-element-wise-container-crdt.md)
(a differential correctness harness vs raw Yjs, property-based concurrent
schedules) and unified onto the object codec in
[0008](doc/plans/0008-one-keyed-codec.md). See
[`examples/TodoCollaborative`](examples/TodoCollaborative) for the worked app.

**Writing a custom element.** The built-in combinators don't have to be the end of
the list. When a field needs a merge strategy of its own — a counter that *sums*
concurrent increments, a grow-only set, a mergeable register — you can define it
in your own code, without editing Ylmish's `Element` union or forking the
library, through the `Custom` seam. You implement one contract:

```fsharp
type CustomElement =
    abstract Kind    : Kind
    /// Get-or-create your shared type at (Parent, Slot), wire both sync
    /// directions honouring the shared Active reentrancy guard, and return a
    /// disposable that tears both down.
    abstract Connect : BindContext -> System.IDisposable
```

`Encode.custom` wraps your binding into the encoded tree and `Decode.custom`
reads its merged value back, exactly like `Encode.text` / `Decode.text`:

```fsharp
"hits", Encode.custom (Counter.element model.Hits mergedCell)   // in your encoder
let! hits = Decode.object.required "hits" (Decode.custom mergedCell)  // in your decoder
```

Like text, a custom element is get-or-created at its **own top-level root** (the
A3-safe `Parent = Root`, relying on A1 root get-or-create) — never a freshly
created nested shared type two peers would race to make — and the layout `Scheme`
still chooses its root name. `Y.Doc.connect` dispatches built-in text and your
custom elements through the *same* `Connect` contract. See
[`examples/TodoCollaborative/Counter.fs`](examples/TodoCollaborative/Counter.fs)
for a runnable grow-only counter (printed by `npm run demo`) whose two peers each
increment once, concurrently, and converge on the **sum**.

**Reserved for later — `Ref<>`.** For fields where the *delta structure itself*
is worth keeping in the app model (large bodies, rich text, very high-frequency
edits), a future `Ref<>` wrapper will let the model hold the live CRDT structure
directly and author deltas without the diff step. It targets the same
`Element.Text` binding as `Encode.text`, so it is a pure performance/fidelity
opt-in, not a prerequisite. It is out of scope for now beyond reserving the seam.


## TODO


1. Investigate failing `Ylmish.Adaptive.Codec.roundtrip updates` test.

1. Implement adaptive-to-Y attaching (syncing), but separate directions

	Right now `Y.Text.attach` sets up bi-directional sync. This should be separated into single directions so that encoding can work with different schemas to decoding which is important for when schemas change over time.

	1. Write a test for the motivator of this change, that is
		1. ~~ensuring existing (state data) maps aren't overwritten~~
		1. ~~ensuring new maps can be added from the app data~~
	   Added to tests in  `Program.fs`
	1. Find another way to handle the sentinel thingo so it doesn't get into a loop. (Ideally considering running multi-threaded in .NET, though minimally we could just assume single-threaded JS for now.)

1. Implement the actual attaching of the adaptive model to the Y.Doc in Program.withYlmish so that the tests in Program.withYlmish pass.

1. Ylmish.Adaptive.Codec.Decoders will need access to the Elmish model.

   The app data will have elements not persisted by state data. (For example, data that is only relevant to current interactions or the current session.) This app data needs to be retained through changes to state data.
   Therefore, when the developer writes a decoder they need access to the current Elmish model to express how app data and state data should be merged.

   `Decoder<'Element, 'Result>` is already a Reader monad so this might be accomplished by tupling the Elmish model into the `'Element` env parameter and implementing an 'ask' function to the `Decode.object` builder

1. We're using `doc.getMap ()` to get a Y.Map in our Program.fs tests. I'm guessing that doesn't get us a 'root' map though and that subsequent calls don't give us _the same_ map. (What does it give us?) We might need make the developer pass in a root map instead.

1. Consider get-or-insert semantics for nested Y types so our maps and arrays aren't overwritten by two clients initializing shared types.

   We could code around this it by only using top-level maps and arrays, representing nested types by name.
   For example, `Y.Doc.getMap('x.y.z')` to represent a map `z` inside a map `y` inside the top-level map `x`.
   Maybe the [key-value type] will support this?(https://github.com/yjs/yjs/issues/255).

1. IndexList in FSharp.Data.Adaptive starts at 1 is probably why the Delta tests are failing

1. Elmish has different versions for Fable and .NET. We need to use the right one.

   https://github.com/elmish/elmish#using-elmish

1. Investigate supporting [Ycs](https://github.com/yjs/ycs) or [Yrs](https://github.com/y-crdt/y-crdt (with a FFI binding) (in addition to [Yjs](https://github.com/yjs/yjs) (./src/Fable.Yjs)).

1. Some app data might need to be resolved with additional network requests.

   For example, the author ID might be persisted in the state data, but the author name and display image might be kept by a different service.

   It might be such a common case we should provide hooks in decoding to resolve this data. In the meantime, this could be left up to the user to do in the Elmish layer.