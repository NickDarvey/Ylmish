# 0007 — A compelling collaborative todo example (prioritised, schema-evolving)

**This document is a proposal.** It states *what* we should build and *why*. The
detailed step breakdown is deliberately deferred (`Work breakdown` below is a
placeholder) until we concur on the direction.

Parent: builds on 0002 (text merge), 0003 (custom seam), 0004 (identity/`Scheme`),
0006 (element-wise collections, Option E + the `afterTransaction` read-back).

## State

**Last updated:** 2026-06-28 · **Status: PROPOSAL (awaiting concurrence).** No code
yet. Once the direction + open questions are settled we break it into steps.

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

## Open questions (resolve before the step breakdown)

1. **Collection combinator — library or example?** Promote 0006's Option E into the
   library as a reusable `Encode.collection` (keeps the example readable; makes
   Ylmish a bit more "batteries-included"), or keep it as example-level consumer
   code (keeps Ylmish minimal, shows how a consumer assembles it)? *Recommendation:
   a thin library combinator for the keyed collection, with ordering/fractional
   index staying in the consumer.*
2. **Per-item text — CRDT or LWW for v1?** Character-merge per item (the harder
   composition, the full story) or LWW text first (simpler, and a nice *later*
   migration to CRDT)? *Recommendation: CRDT per item — it's the differentiator and
   0004 already proved the id-named-root half.*
3. **UI — headless two-peer demo (like today) or a minimal browser UI?**
   *Recommendation: keep it headless/scripted for testability; the readable Elmish
   loop is the artifact. A minimal DOM UI is an optional stretch.*
4. **Migration depth — additive `Order` default only, or also a restructure +
   the coexistence/tolerant-decoder thesis?** *Recommendation: do both — the
   restructure is what makes the point land.*
5. **Do we want small `Decode` ergonomics** for the migration pattern (e.g.
   "optional with default", shape coercion), or just demonstrate with what exists?

## Work breakdown

**TBD — pending concurrence on the proposal above.** Once we agree on direction and
the open questions, expand into ordered steps (one green, committed increment
each), in the usual style: model+update+view → codec composition (collection +
nested text + LWW order) → fractional-index ordering → schema-migration demo →
two-peer demo + tests → docs.
