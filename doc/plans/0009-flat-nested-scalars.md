# 0009 — Flatten nested object scalars (flat-by-default) + `Encode.atomic`

Split out of 0008 because it is the one piece that touches the **core
`materialize`/`dematerialize` path** and so could destabilise the rest. It is a
follow-on to 0008 (the keyed-codec taxonomy) and should land after 0008's `map`.

## The problem

A **top-level** scalar already merges: `materialize` writes it as an entry in the
single root `Y.Map`, so concurrent edits to different keys merge and the same key
is LWW (correct for a scalar). But a **nested** record (`Encode.object` inside
`Encode.object`) becomes a *freshly created* nested `Y.Map` set under the parent
key — **replaced wholesale every update** → whole-container LWW (two peers editing
`Author.Name` and `Author.Bio` at once clobber), and it trips the nested-create
hazard too. So nesting is the only place the structural path silently loses edits.

The path-naming machinery to fix this **already exists**: the `Scheme`/`connect`
walk derives a dotted name from a leaf's position (it's how a nested `Encode.text`
becomes the root `"author.bio"` today). We just don't apply it to *value* leaves.

## The goal

Nested `Encode.value` leaves flatten to **path-keyed entries in the root `Y.Map`**
(auto-named, e.g. `"author.name"`), so nested scalars merge per-key — with
**unchanged consumer code**: `Encode.object` / `Decode.object` are written exactly
as today; only what they *compile to* changes. Plus an explicit opt-out:

- **`Encode.atomic` / `Decode.atomic`** — encode a subtree as a *single* LWW value
  (the current "replace the whole record" behaviour, now a deliberate choice for
  when you want atomic replacement instead of per-field merge).

## State

**Last updated:** 2026-07-02 · **Status: COMPLETE.** All steps done (172 tests
passing). Nested-record scalars flatten to dotted root-map keys (`author.name`) and
merge per-field; `materialize` skips no-op re-stamps so concurrent edits to
different fields both survive; `dematerialize` reassembles the nested tree so
decoders are unchanged; field names are escaped around the separator.
`Encode.atomic` / `Decode.atomic` (a new `Element.Atomic` case) is the wholesale-LWW
opt-out and round-trips as an ordinary nested object. Consumer code
(`Encode.object` / `Decode.object`) is unchanged — only what it compiles to changed.

### Progress

- [x] **Step 0/1** — Flattened representation + reassembly. **Dotted keys in the
  root `Y.Map`** (`"author.name"`) — reuses the same convention `Scheme.flat` gives
  text/custom roots. `materialize` walks the object tree, descending nested `AMap`s
  and emitting each scalar leaf as a dotted root entry; `dematerialize` groups the
  dotted keys back into the nested `AMap` tree. Separator `.` with a backslash
  **escape** (`\.`, `\\`) so a field name containing `.` isn't read as nesting.
  **Minimality:** a scalar whose stored value already matches is *not* re-stamped —
  this is what lets a local edit to one field avoid colliding with a peer's
  concurrent edit to another, so they truly merge. Structural tests that asserted
  nested `Y.Map`s were updated deliberately.
- [x] **Step 2** — `Encode.atomic` / `Decode.atomic` via a new `Element.Atomic`
  case: `materialize` emits the wrapped subtree as one wholesale `Y.Map`/value
  (last-writer-wins), and it dematerializes back to a plain nested `AMap`, so
  `Decode.atomic` is the inner decoder unchanged (no JSON needed).
- [x] **Step 3** — Composition proven: a nested object holding a flattened scalar
  **and** a CRDT text field works — the scalar flattens to `doc.title`, the text
  stays its own connect-managed root `doc.body`. Lists/customs nested in an object
  keep their own single entry/root as before.
- [x] **Step 4** — Tests demonstrate per-field merge (concurrent edits to different
  fields both survive) and the `atomic` contrast (whole-record LWW); multi-field
  round-trip and separator-escaping pinned; README merge-semantics table + a nested
  records section updated. `Encode.list` question resolved below.

### Agent pickup prompt

> Plan 0009 is COMPLETE. Nothing to pick up.

## Design (as built)

- **materialize:** walks the root `AMap`; descends nested `AMap`s (records),
  emitting each scalar leaf as a dotted root-map entry (`author.name`). Text/custom
  leaves are skipped (connect-managed roots); lists and `Atomic` subtrees are one
  wholesale entry. A scalar is written only if its stored value differs (the
  merge-enabling minimality).
- **dematerialize:** reads the root map, splits each key on the (escaped) separator,
  and reassembles the nested `AMap` tree — so a decoder walks the shape it encoded. A
  genuine nested `Y.Map` (an `atomic` subtree, or an older peer's state) is read as a
  single leaf, so both representations converge on the same nested `Element.AMap`.
- **`atomic`:** `Element.Atomic e` marks a subtree for wholesale materialization;
  because it round-trips as a plain nested object, decode needs no counterpart work.

### Resolved: keep `Encode.list`

`Encode.atomic` now generalizes "store this subtree as one wholesale LWW value" to
*any* subtree, and for an `IndexList` it coincides with `Encode.list` (both project a
whole `Y.Array` and replace it wholesale). **Keep `Encode.list`**: it is the
ergonomic, typed combinator for an ordered `alist` (paired with `Decode.list`'s
positional traversal), whereas `atomic` is the general escape hatch aimed at records.
They are complementary, not redundant.

## Scope / non-goals

- **In:** flatten nested *scalar* leaves; `Encode.atomic`/`Decode.atomic`;
  composition with text/custom/map nested in objects; example + docs.
- **Out:** nested *collections* — a list/map nested in an object is a collection,
  handled by 0008's `Encode.map` / `Encode.sequence`, not by this flattening;
  non-string-keyed maps; migration.

## Risks

- **Blast radius on the core path.** `materialize`/`dematerialize` and the
  `Example` model (nested `PropC`/`PropE`) and many tests assume nested `Y.Map`s.
  Expect to update structural assertions; that's the cost of the split and the
  reason it's isolated here.
- **Key collisions** if a field name contains the separator — pick an escaping rule
  at Step 0.
- **Round-trip fidelity** — `decode (encode model) = model` must still hold at
  quiescence after the representation change (a property test guards it).
