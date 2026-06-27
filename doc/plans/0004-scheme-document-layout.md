# 0004 — Scheme: stable document layout for nested collaborative state

Completes the layout seam introduced by
[0002](./0002-crdt-text-through-the-codec.md): make `Codec.Scheme` produce
**stable** root names for collaborative leaves nested inside reorderable
collections, and close the A3 gap that plan 0002's pragmatic slice left open for
non-text containers.

Parent: plan 0002. No separate issue yet.

## State

**Last updated:** 2026-06-27 · **Status: NOT STARTED.** `Scheme` exists as
`{ RootName : Path -> string }` with an A3-safe-for-*flat*-models default
(`Scheme.flat`, dotted path names). It flattens **text** leaves to roots; non-text
stays on the structural/materialize path. Next step: **Step 0** (reproduce the
instability).

### Progress

- [ ] **Step 0** — Spike: reproduce divergence/clobber for **index-named** text
  nested in a list when two peers reorder/insert concurrently. Pins the motive.
- [ ] **Step 1** — Decide the **stable-id convention**: how an item's identity
  reaches the scheme (a `PathSegment.KeyById`, an `itemId : Element -> string`
  resolver, or a reserved id field). Decision step (fixes wire format).
- [ ] **Step 2** — Make `connect`'s list walk emit id-based path segments and add
  `Scheme.byId` (or make `flat` id-aware). Text nested in a list gets a stable
  root name.
- [ ] **Step 3** — Test: a list of objects with a collaborative text field
  converges across two peers **under concurrent reorder/insert** (the A3 tail).
- [ ] **Step 4** *(bigger, optional)* — Generalise `connect` so **non-text
  containers** (lists/maps) are also flattened roots, closing A3 for them too —
  or formalise the hybrid and document the trade-off.
- [ ] **Step 5** — Example + docs: a reorderable list with collaborative text
  items; README note on choosing/ writing a `Scheme`.

### Decisions & lessons

- _(none yet)_

### Blockers

- None.

### Agent pickup prompt

> You are continuing plan 0004. Work **one step at a time**, in order, keeping the
> suite green. Each iteration: pick the first unchecked **Progress** item; read
> its *Work breakdown* entry; implement the minimal change + its test(s); verify
> with `export PATH="$HOME/.dotnet:$PATH" && npm test`; update **State** (tick,
> bump *Last updated*, record decisions/blockers); commit one focused commit to
> the working branch (no PR unless asked); compact context; continue. Stop when
> all steps are checked or a blocker needs a human decision — especially the
> Step 1 wire-format decision.

## Problem

Plan 0002 chose the *flattened-top-level-name* path (A3: nested concurrent
get-or-create clobbers, so every collaborative leaf is a top-level root, relying
on A1). `Scheme.flat` names a leaf by its flattened path:

```
[ObjectKey "items"; ArrayIndex 2; ObjectKey "body"]  ->  "items.2.body"
```

Two gaps remain:

1. **Indices aren't stable identities.** `ArrayIndex 2` is positional. If peers
   insert/remove/reorder list items concurrently, "the body of item 2" means
   different items on different peers, so they bind to the *same* root name for
   *different* logical text — divergence, or one item's text leaking into
   another. Plan 0002 documented this as the deferred A3 tail.
2. **Non-text containers aren't roots.** Nested lists/maps still live in the
   structural root map (materialize path), so two peers concurrently *creating*
   the same nested list/map can clobber (A3). 0002's pragmatic slice accepted
   this (the #83 focus was text).

## Goal

- A **stable** scheme in the box: a collaborative leaf nested in a list is named
  by the item's *identity*, not its index — `"items.<id>.body"` — so peers agree
  regardless of order. Define how identity flows from model → codec → scheme.
- Optionally make the layout fully A3-safe for non-text containers too, or
  formalise the hybrid.
- Keep `Scheme` the single place that defines the persisted wire format, so
  consumers can swap layouts without forking `connect`.

## Design

### Identity in the path, not position

The cleanest fix is to replace positional segments with identity segments when
walking a keyed collection. Introduce an id-bearing path segment and teach
`connect`'s list walk to use it:

```fsharp
type PathSegment =
    | ObjectKey of string
    | ArrayIndex of int        // kept for positional/uniquely-created cases
    | KeyById   of string      // stable identity of a collection item
```

`connect`, when walking an `AList` whose items carry an id, emits
`KeyById id :: path` instead of `ArrayIndex i :: path`. `Scheme.flat` over an
id-path yields `"items.<id>.body"` — stable across reorder. How the id is
obtained is the Step 1 decision:

- **(A)** A resolver supplied alongside the encoder: `itemId : Element -> string`
  (reads a known field of each item). Most flexible; no path-type change forced.
- **(B)** A reserved id field convention (e.g. each collaborative list item must
  encode an `"id"` value); `connect` reads it. Simplest, most opinionated.
- **(C)** `Scheme` itself resolves identity (given the item element). Keeps it in
  the layout seam.

This is a **wire-format decision** (the names are persisted), so it is the
plan's first real choice and gets a `Blockers`-style call-out before code.

### Optionally: containers as roots

To close A3 for non-text, `connect` would get-or-create lists/maps as top-level
roots (named by the scheme) via `Y.Array`/`Y.Map` root get-or-create, instead of
nesting them in the structural map — the full README-TODO-6 flattening. This is
larger and reshapes how `withYlmish` reads non-text back (the structural path
would shrink). Step 4 weighs doing this vs. documenting the hybrid as intentional
for collections that are only ever created by one writer.

### `Scheme` may grow beyond `RootName`

If the scheme is to govern non-text layout too, `RootName : Path -> string` is
not enough — it needs the element/kind at the path to decide placement. The plan
considers widening it (e.g. `Locate : Path -> Kind -> Location`) only if Step 4
is pursued; otherwise `RootName` stays minimal and id-aware.

## Work breakdown — incremental, verify after every step

### Step 0 — Reproduce the instability (no production code)

A spike test: two docs, a list with a collaborative text item; concurrently
insert an item at the front on one peer while editing an existing item's text on
the other; sync; show that index-based names bind the wrong roots (divergence or
cross-talk).

- **Exit check:** the failure is demonstrated and pinned, justifying the design.

### Step 1 — Decide & document the stable-id convention

Choose (A) resolver / (B) reserved id field / (C) scheme-resolves. Record the
decision (it fixes the wire format). Add the `KeyById` segment if needed.

- **Exit check:** decision written under *Decisions*; types compile; suite green.

### Step 2 — id-aware connect + `Scheme.byId`

`connect`'s `AList` walk emits id segments per the convention; `Scheme.byId`
(or an id-aware `flat`) names roots stably. Keep `Scheme.flat` for positional/
flat use.

- **Test:** a nested-list text field gets a name keyed by id, not index.
- **Exit check:** stable names; suite green.

### Step 3 — Convergence under reorder

The headline test: a list of objects each with a collaborative text field; two
peers concurrently reorder/insert items *and* edit text; after sync the right
text stays attached to the right item and edits merge.

- **Exit check:** the A3 tail is closed for text-in-lists. Pins A1 + the id
  convention.

### Step 4 *(optional/bigger)* — containers as roots

Generalise `connect` to flatten non-text lists/maps to roots, or formalise the
hybrid. If pursued, rework the `withYlmish` read-back accordingly and possibly
widen the `Scheme` surface.

- **Exit check:** either full A3-safety for containers, or a documented,
  test-backed statement of the hybrid's guarantees.

### Step 5 — Example + docs

A reorderable collaborative list in/near `TodoCollaborative`; README guidance on
picking `Scheme.flat` vs `Scheme.byId` and writing a custom `Scheme`.

- **Exit check:** docs match behaviour; example runs.

## Assumptions / risks

- **Wire-format permanence.** Root names are persisted; changing the scheme later
  is a migration. Step 1 is therefore a deliberate, hard-to-reverse decision.
- **Model must carry identity.** Stable naming needs per-item ids; models without
  them keep using `Scheme.flat` (positional) with its documented caveat.
- **Interaction with `Encode.list`.** The encoder maps items without ids today;
  the id convention must thread an id through the list walk without forcing every
  list to be keyed.
- **Step 4 scope.** Full container flattening reshapes the non-text read path
  (the 0002 hybrid); only undertake it with the same incremental, test-each-step
  discipline.

## Out of scope

- The merge *strategy* of a field — that is plan 0003 (`CustomElement`).
- Non-Yjs backends.
- Automatic id assignment/migration for existing persisted documents.
