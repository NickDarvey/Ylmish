# 0008 — A migration module: declarative schema evolution for objects and collections

The minimal helpers (`CollectionItem.fieldOr` / `writeAll`) name the two halves of
a field rename, but a consumer still wires them by hand, per field, twice (read and
write), with stringly-typed values. This plan adds a **declarative migration
vocabulary** — a field's *type* and its *evolution history* declared once — that
drives both the read (tolerant) and the write (non-destructive) directions, and
works for **both** ordinary object fields *and* element-wise collection items.

Parent: builds on 0007 (the example + `Encode.collection` + the minimal helpers).

## State

**Last updated:** 2026-06-28 · **Status: NOT STARTED (proposal + steps).**

### Progress

- [ ] **Step 0** — Decide the abstraction + the object/collection unification fork
  (below) with a cheap prototype of the read path on both backends. Record the
  decision in *Decisions*.
- [ ] **Step 1** — Typed field leaves + **read-both + default** over
  `CollectionItem` (collection backend). Replace the example's manual reads.
- [ ] **Step 2** — **Dual-write** (non-destructive) over `CollectionItem` (encode).
- [ ] **Step 3** — The **same vocabulary over `Decode.object` / `Encode.object`**
  (object backend), so a top-level field rename reads/writes identically.
- [ ] **Step 4** — **Value transforms** (type change, e.g. legacy
  `completedAt : timestamp option` → `Done : bool`) + a worked object-field rename
  test.
- [ ] **Step 5** — Docs + migrate the example to the vocabulary; README + the
  example walkthrough; note the "drop the old name once all peers are vN" endgame.

### Decisions & lessons

- *(none yet)*

### Agent pickup prompt

> You are executing plan 0008. Settle Step 0's abstraction first with a cheap
> prototype, then work in order, one green commit each (`npm test`). The migration
> vocabulary must be expressible **once per field** and drive both read (tolerant:
> read-both, default) and write (non-destructive: dual-write) directions, for both
> object fields and collection items. Keep the Elmish loop and the codec readable.

## Why this, and why it's subtle

A collaborative CRDT document has **no single migration moment**: peers on
different schema versions edit it concurrently. So the right model is *continuous
coexistence* — tolerant decoders (read the old shape too) + non-destructive
encoders (keep writing the old shape until everyone's upgraded). The minimal
helpers proved the pattern; this plan makes it *declarative and typed* so it isn't
re-derived by hand for every field.

**Why two backends exist (the thing to reconcile).** Ordinary fields decode via
`Decode.object` over the `Element` tree that `dematerialize` reads from the doc's
structural root map. Element-wise collection *items* never enter that tree — the
collection binding reads its own top-level `Y.Map` and hands back a flat
`CollectionItem` (a string-keyed bag). So today there are **two** field-access
worlds (Element/`Decode.object` vs `CollectionItem.Fields`), and a migration helper
must serve both or a consumer learns the concept twice.

## The fork (Step 0 decides)

- **(U) Unify the representations.** Make the collection surface each item as an
  `Element.AMap` so items decode with `Decode.object` and *one* migration layer
  covers everything. Cleanest end state, but it re-plumbs the working
  `Encode.collection` / `Decode.collection` surface (0007) and the binding's
  fast flat-string reconcile — higher risk.
- **(S) Shared vocabulary, two thin adapters** *(recommended)*. Keep the flat
  `CollectionItem` (it suits the binding) and define the migration combinators over
  an **abstract field source/sink**, with one adapter for `CollectionItem` and one
  for `Decode.object`/`Encode.object`. The *logic* (rename fallback, default,
  parse, dual-write) is shared; only the raw get/put-by-key primitive differs.
  Lower risk, one mental model, no re-architecture.

Step 0 prototypes the read path on both backends to confirm the shared abstraction
is clean before committing.

## The vocabulary (sketch — Step 0 firms it up)

Declare a field once: its name, its type, its history.

```fsharp
// current name "completed", a bool, formerly "done"
let completed = Field.bool "completed" |> Field.renamedFrom "done"
let order     = Field.string "order"
let title     = Field.string "title" |> Field.renamedFrom "name"   // works for object fields too

// READ (tolerant): current name, then older names, then default; typed.
let isDone = completed |> Field.read source          // source = CollectionItem OR Decoder env
// WRITE (non-destructive): dual-writes every known name; rendered from the type.
let entries = completed |> Field.write isDone         // -> the key/value(s) to emit
```

Combinators: `Field.string/bool/int` (typed leaf, kills the stringly-typing),
`Field.renamedFrom name` (adds a prior name → read-fallback + dual-write),
`Field.defaultsTo v` (additive field), `Field.migrate (old -> new)` (type change /
coercion). Backends: `Field.read`/`write` against a `CollectionItem`, and
`Decode.object.field` / an `Encode.object` counterpart against the Element tree.

## Scope / non-goals

- **In:** typed fields; rename (read-both + dual-write); default; simple value
  transforms; both backends; migrate the example onto it; docs incl. the
  drop-the-old-name endgame.
- **Out:** a general *version-branching* framework; automatic one-shot data
  rewrites (there is no single moment to run them); multi-field/relational
  migrations beyond a single value transform (flag as future); choosing the LWW
  winner across schema versions (that's the CRDT's job, unchanged).

## Open question

- A **version-tag escape hatch** (`Field`/decoder branching on an explicit
  `schemaVersion`) for changes too structural for tolerant read-both — include a
  minimal form, or commit to tolerant-only and document the limit? Decide at Step 4
  once the transform case is built.
