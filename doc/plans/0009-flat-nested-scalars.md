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

**Last updated:** 2026-06-28 · **Status: NOT STARTED (proposal + steps).**
Depends on 0008 (`Encode.map`) landing first.

### Progress

- [ ] **Step 0** — Spike/decide the flattened representation: dotted keys in the
  root `Y.Map` (`"author.name"`) vs individual roots; and how `dematerialize`
  **reassembles** the nested `Element` tree from flat keys so `Decode.object` is
  unchanged. Pick a separator + escaping rule for field names containing it.
- [ ] **Step 1** — Flatten nested *value* leaves in `materialize`/`dematerialize`
  (objects of scalars → dotted root-map entries → reassembled tree on read). Keep
  the whole suite green; update the structural tests that assert nested `Y.Map`s.
- [ ] **Step 2** — `Encode.atomic` / `Decode.atomic`: a subtree as one LWW value
  (JSON-ish), for explicit whole-record replacement.
- [ ] **Step 3** — Composition: a nested object holding *both* scalars and
  text/custom/`map` still works (scalars flatten to dotted keys; text/custom/map
  keep their own roots — the `Scheme` already names them by the same path).
- [ ] **Step 4** — Migrate the example/tests to show a nested scalar now merges
  per-key; docs (README + walkthrough); revisit the "keep `Encode.list`?" question
  now that `atomic` exists.

### Agent pickup prompt

> You are executing plan 0009 (do 0008's `map` first). The bar: consumer code
> (`Encode.object`/`Decode.object`) is **unchanged** — only the backend changes so
> nested scalars merge per-key. `Encode.atomic` is the opt-in for whole-subtree
> LWW. This touches `materialize`/`dematerialize`; keep `npm test` green every step
> and update structural tests deliberately, not reflexively.

## Design sketch

- **materialize:** walk the `Element` tree; a nested `AMap` of value leaves emits
  dotted root-map entries (`author.name`, `author.bio` if a value) rather than a
  nested `Y.Map`. The walk already exists for text/custom; extend it to values.
- **dematerialize:** read the root map; **reassemble** dotted keys back into the
  nested `AMap` tree, so `Decode.object` walks it unchanged.
- **`atomic`:** one key holds a serialized snapshot of the subtree; decode parses
  it. The escape hatch that preserves "replace as a whole".

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
