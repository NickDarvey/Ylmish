# 0007 — A compelling collaborative todo example (prioritised, schema-evolving)

**This document is a proposal.** It states *what* we should build and *why*. The
detailed step breakdown is deliberately deferred (`Work breakdown` below is a
placeholder) until we concur on the direction.

Parent: builds on 0002 (text merge), 0003 (custom seam), 0004 (identity/`Scheme`),
0006 (element-wise collections, Option E + the `afterTransaction` read-back).

## State

**Last updated:** 2026-06-28 · **Status: NOT STARTED (broken into steps).**
Direction concurred; open questions resolved (see *Decisions*). Ready to execute
Step 0.

### Progress

- [x] **Step 0** — Pure Elmish todo: reshaped `Model.fs` (`Todo = {Id;Text;Done;
  Order}`, `Model = {Todos;NewItem;Filter}`, intention-named `Msg`, pure `update`,
  `ordered`/`visible` helpers); fractional-index ordering via a consumer
  `Ordering` module (`fractional-indexing`); a temporary structural `Codec`;
  updated `Demo`; rewrote the example tests (pure update + ordering + structural
  round-trip). No Yjs in `Model.fs`. 153 passing.
- [x] **Step 1** — Library `Encode.collection` / `Decode.collection` (in
  `Adaptive.Codec.fs`): an element-wise, id-keyed collection over one top-level
  `Y.Map` with `<id>/<field>` keys + an `@<id>` presence marker, reconciled by key
  against live Yjs. Concurrent add/remove merge; per-item fields are per-id LWW.
  `CollectionItem = { Id; Fields }`. Tests (`Codec.Collection.fs`, 4): concurrent
  adds both survive, different-item field edits merge, same-field edits converge
  (LWW), removal propagates. 157 passing.
- [x] **Step 2** — Per-item **CRDT text** under the collection: each text field
  lives in a top-level `Y.Text` root named `<name>/<id>/<field>`, mirrored by
  affix-diff and observed lazily per item. `CollectionItem` gains `Texts`;
  `Encode.collection` takes `textFields`. Tests (+2): same-item text merges
  character-by-character (not LWW), and survives a concurrent membership change.
  159 passing.
- [ ] **Step 3** — Wire the example codec with `Encode.collection` (text CRDT,
  done/order LWW) under `withYlmish`. Two-peer convergence tests: concurrent add,
  edit, reorder, toggle.
- [ ] **Step 4** — Schema-migration demo: a **field rename with v1/v2
  coexistence** (tolerant decoder reads both keys; non-destructive encoder). Tests
  proving a v1-authored doc loads in v2 and mixed-version peers converge.
- [ ] **Step 5** — Headless two-peer demo (`Demo.fs`): scripted concurrent
  scenario (adds survive, edits merge, reorder merges, a v1↔v2 beat). `npm run
  demo` runs clean.
- [ ] **Step 6** — Docs: README example section + an example walkthrough doc
  (model, codec mapping, ordering, the migration pattern).

### Decisions (resolved with the human, 2026-06-28)

1. **Collection lives in the *library*** as a reusable `Encode.collection` /
   `Decode.collection`. Ordering (fractional index) stays *consumer* code.
2. **Per-item text is CRDT** (character-merge), not LWW.
3. **Headless** — keep the runnable two-peer scripted demo; no browser UI.
4. **Migration = a field rename** demonstrating v1/v2 schema **coexistence**
   (decoder handles both shapes). `Order` is a *feature*, explicitly **not** the
   migration example.
5. **Docs are a dedicated step** (Step 6).

### Agent pickup prompt

> You are executing plan 0007. Work the steps in order, one green committed
> increment each (`npm test`). Keep the Elmish loop in `Model.fs` free of any Yjs
> reference — that readability is the bar. The collection combinator goes in the
> *library* (`src/Ylmish`); fractional-index ordering stays consumer code in the
> example. Per-item text is CRDT. The migration demo is a field rename with live
> v1/v2 coexistence (tolerant decoder + non-destructive encoder), not `Order`.
> Update the plan's Progress + Decisions as you go.

## The problem

The current `examples/TodoCollaborative` is not yet a compelling demonstration of
what Ylmish is *for*:

- Its `Items` is a plain `IndexList<string>` on the structural path — **whole-
  container LWW**. Two peers adding concurrently silently lose one add (the exact
  bug 0006 fixed but which the example doesn't yet use). Items have **no
  identity**, no done-state, and **no ordering** — so the canonical thing a todo
  app does (prioritise/reorder a shared list) isn't shown at all.
- The two genuinely differentiating values of Ylmish are under-served:
  1. **Sync, handled invisibly** — only partially shown (text merge via `Note`).
  2. **The codec as the schema / migration boundary** — *not shown at all*, yet
     this is a first-class real-world pain: persisted, collaboratively-edited
     documents outlive their schema, and (unlike a request/response app) there is
     **no single migration moment** — mixed-version peers coexist live.

We want the canonical artifact: a **prioritised, collaboratively-edited todo app**
whose Elmish loop reads like a textbook todo app, while Ylmish quietly handles
sync *and* is the obvious place where the persisted representation evolves.

## What we propose to build, and why

A todo app with: add, edit text, toggle done, remove, and **reorder
(prioritise)** — all merging correctly across peers — plus a worked **schema
evolution** demonstrating mixed-version coexistence. Three design commitments:

### 1. A textbook-clean Elmish loop (the readability bar)

The model is plain immutable F# records; the messages name intentions; `update`
is pure; Yjs appears **nowhere** in `Model.fs`.

```fsharp
type TodoId = string                          // an immutable guid
type Filter = All | Active | Completed
type Todo  = { Id : TodoId; Text : string; Done : bool; Order : string }
type Model = { Todos : Todo list; NewItem : string; Filter : Filter }

type Msg =
    | SetNewItem of string
    | Add                                      // new guid + Order after the last
    | Edit   of TodoId * string
    | Toggle of TodoId
    | Remove of TodoId
    | Move   of TodoId * before: TodoId option * after: TodoId option
    | SetFilter of Filter
```

`view` sorts `Todos` by `Order` and applies `Filter`. That sort is the *only*
place ordering shows up — the list is **derived**, never stored positionally.

### 2. Prioritisation = a fractional-index **order key per item** (the ordering call)

The user specifically flagged ordering. The options:

- **Array position** — wrong: `Y.Array` has no native move, so concurrent reorders
  delete+insert and may *duplicate* (0006's pinned limit).
- **Integer rank** — concurrent inserts collide and force O(n) renumbering, which
  itself merges badly.
- **Fractional-index string key per item** *(chosen)* — a reorder is **one LWW
  field write**: set the moved item's `Order` to `generateKeyBetween before after`.
  Concurrent reorders of *different* items merge cleanly; the list is recovered by
  sorting. Concurrent reorder of the *same* item is LWW (converges, one wins —
  acceptable). Concurrent insert-between-the-same-pair can pick equal keys → break
  ties by `Id`. (0004 already validated `fractional-indexing` end-to-end.)

**Ylmish does not magically order.** The consumer owns the `Order` field and brings
the `fractional-indexing` library; Ylmish merely **syncs `Order` as a per-item LWW
value** (which converges). This is the right division of labour — Ylmish handles
the hard distributed part (merge), the app expresses intent.

### 3. The codec maps the model to *mergeable* Yjs (the sync value, made concrete)

| Model field | Codec | Merge behaviour |
|---|---|---|
| `Todos` (the collection) | element-wise keyed by `Id` (0006 Option E) | concurrent add/remove **merge** — no lost items |
| `Todo.Text` | CRDT text, root named by `Id` (`Scheme.byKey`) | concurrent edits **character-merge** |
| `Todo.Done`, `Todo.Order` | per-item LWW value | concurrent toggles/reorders **converge** |

This is exactly the composition 0006 flagged as the next increment (element-wise
membership + id-named nested text + per-item LWW fields). The example **drives**
that composition into existence.

### 4. The codec as the schema / migration boundary (the under-served value)

The decoder is the single boundary between the app model and the persisted
representation, and it already has the current model via `ask`. We demonstrate a
real **v1 → v2 evolution** and articulate the principle that makes it matter:

- **No single migration moment.** In a CRDT document, peers of *different schema
  versions edit the same doc concurrently*. So migration is **continuous
  coexistence**, not a batch job. The consequences are a Ylmish guidance story:
  - **Decoders must be tolerant** — default missing fields, accept old shapes
    (e.g. v1 todos had no `Order` → assign fractional keys in id-order on read; a
    legacy `completedAt : timestamp option` → derive `Done`).
  - **Encoders must be non-destructive** — a peer must not clobber fields newer
    peers added that it doesn't model. This is *another* reason the element-wise /
    `connect` path beats whole-container `materialize`: each peer only writes the
    roots/fields it manages, so an old peer re-encoding can't drop a new field.
- Concretely the example will show: an additive field (`Order`) defaulted on read,
  and one restructure (rename/derive), with old and new peers interoperating live.

This makes "Ylmish is the place you handle schema changes in the synced
representation" a thing you can *run*, not just a claim.

## Why this is the right next thing

- It turns the headline 0006 result (element-wise merge) and the 0004 result
  (identity/ordering) into a **single coherent, runnable artifact** — the app
  everyone reaches for to understand a sync library.
- It forces the one real composition 0006 left open (nested CRDT text + LWW fields
  under a dynamic keyed collection) — so it advances the library, not just docs.
- It showcases the **schema-evolution** value that nothing in the repo currently
  demonstrates, and surfaces a genuinely sharp idea (continuous coexistence →
  tolerant decoders + non-destructive encoders) that differentiates Ylmish.
- It holds the line the user set: **maximally readable Elmish**, with Ylmish doing
  the sync and being the schema seam — not magically solving the consumer's domain.

## Scope / non-goals (for discussion)

- **In:** model + update + view + codec; element-wise todos; per-item collaborative
  text; fractional-index ordering; a runnable two-peer demo with a scripted
  concurrent scenario; a worked schema migration; tests proving merge + migration.
- **Out (proposed):** a polished drag-and-drop browser UI; a general schema-
  versioning *framework*; solving concurrent-same-item reorder beyond LWW.

## Open questions

Resolved — see *Decisions* in the State section above.

## Work breakdown — verify after every step

The checklist lives in *State → Progress*. Detail per step:

### Step 0 — Pure Elmish todo (no Ylmish)
`Todo = { Id; Text; Done; Order }`, `Model = { Todos; NewItem; Filter }`, the `Msg`
set above. `update` is pure: `Add` mints a guid id + an `Order` after the last
item; `Move` sets `Order = generateKeyBetween before after`; toggle/edit/remove are
ordinary. `view` derives the sorted+filtered list. A small consumer `OrderKey`
module wraps `fractional-indexing` (JS interop). Unit-test `update` — especially
`Move`/ordering and `Add` id minting. **Exit:** the loop reads like a textbook todo
app; tests green; zero Yjs in `Model.fs`.

### Step 1 — Library: element-wise keyed collection
`Encode.collection` / `Decode.collection` in `src/Ylmish`, generalising 0006's
Option E from "ids" to "records with an id + value fields": membership is
element-wise (concurrent add/remove merge), and per-item **value** fields
(`Done`, `Order`) are per-id LWW that converge. Validate with the 0006 harness
(differential vs raw Yjs) + property schedules. **Exit:** a list of `{id; done;
order}` records merges concurrent add/remove/toggle/reorder under the harness;
library API documented.

### Step 2 — Library: per-item CRDT text under the collection
Compose id-named `Y.Text` roots per item (0004 `Scheme.byKey`) with the Step 1
collection, so each item carries a character-merging text field. **Exit:**
concurrent edits to the *same* item's text merge and survive concurrent membership
changes/reorder; tests green.

### Step 3 — Wire the example codec under `withYlmish`
Express the Step 0 model through the codec: `Todos` via `Encode.collection` (text
CRDT, done/order LWW), `Note` via `Encode.text`. Two-peer `withYlmish` tests:
concurrent add (both survive), concurrent same-item edit (merge), concurrent
reorder of different items (merge), concurrent toggle (converge). **Exit:** the app
is genuinely collaborative end-to-end; tests green.

### Step 4 — Schema migration: a field rename, v1/v2 coexisting
Pick a scalar field rename (e.g. v1 `done` → v2 `completed`). The **decoder reads
both** keys (prefer v2, fall back to v1) and the **encoder is non-destructive**.
Tests: a v1-authored doc loads correctly in v2; a v1 peer and a v2 peer edit the
same doc concurrently and converge (continuous coexistence). Add a small `Decode`
ergonomic only if the read-both pattern is otherwise noisy. **Exit:** schema change
is *runnable*; the coexistence guarantee (and its limit) is tested + documented.

### Step 5 — Headless two-peer demo
Update `Demo.fs`'s scripted scenario to exercise the above (adds survive, edits
merge, reorder merges, a v1↔v2 coexistence beat) and print state legibly. **Exit:**
`npm run demo` runs clean and tells the story.

### Step 6 — Docs
README example section + an example walkthrough (the model and why it's readable,
the codec mapping table, ordering via fractional index, and the migration pattern:
tolerant decoders + non-destructive encoders, continuous coexistence). **Exit:**
docs match the code; a newcomer can follow the loop and the sync/migration story.
