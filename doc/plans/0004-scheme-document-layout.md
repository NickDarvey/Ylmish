# 0004 — Scheme: pluggable document layout for nested collaborative state

Completes the layout seam introduced by
[0002](./0002-crdt-text-through-the-codec.md): make `Codec.Scheme` expressive
enough that a consumer can give collaborative leaves **stable** root names even
when they live inside reorderable collections — by threading each item's
*identity* into the scheme — and resolve, by research + spike, how far the
library should go in flattening non-text containers to roots.

Parent: plan 0002. No separate issue yet.

## State

**Last updated:** 2026-06-27 · **Status: IN PROGRESS.** Steps 0–2 done. Step 0
(research + spikes) re-confirmed A1/A3/reorder against real `yjs` 13.6.30 and
validated the reorderable scenario with `fractional-indexing` — see *Step 0 —
findings*. Step 1 fixed the **identity convention** (`PathSegment.KeyById of
string`, an immutable id). Step 2 **threaded it through `connect`**: `Scheme`
gained `ListKeyField : Path -> string option`, the list walk emits `KeyById id`
(reading the item's id field) for keyed lists, and `Scheme.byKey "id"` is the
convenience — so a list's text leaves get `items.<id>.body` roots. `Scheme.flat`
stays positional. *(125 tests green; new: "Scheme.byKey names … by item id".)*
Next step: **Step 3** (convergence under concurrent reorder).

### Progress

- [x] **Step 0** — **Research + spikes (go online).** Re-confirmed A1 / A3 /
  reorder against real `yjs` 13.6.30, and validated the end-to-end reorderable
  list with `fractional-indexing` + `y-utility` survey. Findings recorded below.
  *(No production code.)*
- [x] **Step 1** — **Identity convention decided:** a `PathSegment.KeyById of
  string` segment carries an item's stable, immutable id into the `Scheme`
  (emitted by the list walk from a resolver in Step 2). `Scheme.flat` /
  `Path.toString` handle it. Wire-format decision recorded in *Decisions*; types
  compile, suite green. *(124 tests.)*
- [x] **Step 2** — Threaded identity through `connect`'s list walk: `Scheme`
  gained `ListKeyField : Path -> string option`; the walk emits `KeyById id`
  (extracting the item's id field) when a list is keyed, else `ArrayIndex i`.
  `Scheme.byKey "id"` convenience added; `Scheme.flat` stays positional.
  Extraction lives in the walk so `Scheme` stays `Element`-free. *(125 tests.)*
- [ ] **Step 3** — Test: a list of objects with a collaborative text field
  converges across two peers **under concurrent reorder/insert**, using a
  consumer-supplied stable id (a fractional index or a guid).
- [ ] **Step 4** — *(open — defer to what Step 0 discovers)* Non-text containers
  and full A3-safety: flatten lists/maps to roots, keep the hybrid, or adopt an
  extension. The agent decides based on Step 0's findings; see *Open question*.
- [ ] **Step 5** — Example + docs: a reorderable collaborative list; README
  guidance on `Scheme.flat` vs id-based naming and the fractional-index pattern.

### Decisions & lessons

- **Index instability is a consumer policy, not a library bug (decided).**
  Positional root names (`items.2.body`) diverge if peers reorder/insert
  concurrently. That is *acceptable*: the library's job is the **seam**, not the
  ordering policy.
  - A consumer whose list is **constant-indexed** (append-only, no reorder) uses
    `Scheme.flat` as-is.
  - A consumer who **reorders** supplies a **stable, *immutable* id per item** (a
    guid minted at creation), carried in their model, and an **id-aware `Scheme`**
    names roots by it.
  - This plan's deliverable is therefore to make identity *reachable* by the
    scheme (Steps 1–2), not to implement ordering in the library.
- **Identity convention: `PathSegment.KeyById of string` (Step 1, decided).** Of
  the three shapes the plan floated — a `KeyById` path segment, an
  `itemId : Element -> string` resolver param on `connect`, or the scheme reading
  a reserved field — we chose the **`KeyById` segment**. Rationale: identity is a
  *layout/path* concern, and `Path` is exactly the layout vocabulary the `Scheme`
  already consumes; adding a segment keeps all naming logic in one place (the
  scheme's `RootName : Path -> string`) and lets a scheme mix positional and
  id-named segments freely. The list walk emits `KeyById id` (from a resolver
  wired in Step 2) instead of `ArrayIndex i` when an id is available; positional
  `ArrayIndex` stays the default for constant-indexed lists. The wire format for
  an id-named root is therefore the scheme's rendering of the path (e.g.
  `Scheme.flat` → `items.<id>.body`) — a deliberate, persisted choice.
- **Naming id ≠ ordering id (Step 0 finding, decided).** A
  [fractional index](https://www.figma.com/blog/realtime-editing-of-ordered-sequences/)
  is *mutable by design* — you change it to reorder — so it must **not** name the
  root (renaming on reorder would orphan the text's CRDT history). A reorderable
  list needs **both**: an immutable guid for the `Scheme` root name, and a
  separate mutable fractional `order` field (ordinary `Encode.value` state) for
  display order. The library documents the
  [`fractional-indexing`](https://github.com/rocicorp/fractional-indexing) recipe
  for `order`; it does not bundle or implement fractional indexing. *(Spiked end
  to end in Step 0.)*

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
> Step 1 wire-format decision. For **Step 0**, actually go online (WebSearch /
> WebFetch), and where feasible **run** an extension in a spike to confirm it
> behaves as documented before relying on it; record what you found and tried.

## Problem

Plan 0002 took the flattened-top-level-name path (A3: nested concurrent
get-or-create clobbers, so every collaborative leaf is a top-level root, relying
on A1). `Scheme.flat` names a leaf by its flattened path:

```
[ObjectKey "items"; ArrayIndex 2; ObjectKey "body"]  ->  "items.2.body"
```

Two gaps remain — and the plan treats them very differently:

1. **Indices aren't identities — `OK, consumer's choice`.** `ArrayIndex 2` is
   positional; under concurrent reorder/insert peers disagree about which item is
   "2", so they bind the same root name to different logical text. Per the
   decision above, the fix is a **stable id chosen by the consumer** (fractional
   index or guid) plus an id-aware scheme — the library only has to make identity
   reachable by the scheme and document the pattern.

2. **Non-text containers aren't roots — `open, agent decides`.** Nested
   lists/maps still live in the structural root map, so two peers concurrently
   *creating* the same nested list/map can clobber (A3). Whether to flatten
   containers to roots, keep the hybrid, or lean on a Yjs extension is **left to
   what Step 0's research and spikes turn up** (see *Open question*).

## Open question (Step 4) — deferred to agent discovery

**Should `connect` flatten non-text containers (lists/maps) to top-level roots
too, and if so how?** This is intentionally *not* pre-decided. The agent must let
**Step 0's online research and extension spikes** drive it. Inputs to gather:

- Re-confirm A3 for nested containers (does concurrent first-creation of a nested
  `Y.Array`/`Y.Map` still clobber on the current Yjs?).
- Survey and **try** Yjs ecosystem options that bear on keyed/ordered nested
  state — e.g. `yjs/y-utility` `YKeyValue` (avoids `Y.Map` history bloat),
  fractional-indexing libraries, subdocuments, or any newer "keyed collection"
  support. Validate behaviour in a spike rather than trusting docs.
- Then choose, with a written rationale: (a) full path-flattening of containers
  to roots; (b) keep the 0002 hybrid and document its guarantees; (c) adopt a
  specific extension. Record the choice and evidence in *Decisions* before Step 4
  code.

The expectation is a **researched, spike-backed** decision — not a guess.

## Design

### Identity reachable by the scheme

The scheme can only name by what the walk hands it. Today that is a `Path` of
`ObjectKey`/`ArrayIndex`. To name a list item by a stable id, the walk must
surface that id. Likely shape (final form decided in Step 1 from Step 0
findings):

```fsharp
type PathSegment =
    | ObjectKey of string
    | ArrayIndex of int        // positional; fine for constant-indexed lists
    | KeyById   of string      // a stable identity supplied by the consumer
```

`connect`, walking an `AList`, asks an identity resolver for each item and emits
`KeyById id` instead of `ArrayIndex i` when one is available. An id-aware
`Scheme` then yields `items.<id>.body`, stable across reorder. The resolver is
the consumer's hook to plug in **their** id — a fractional index field, a guid,
whatever. The library ships `Scheme.flat` (positional) and a small convenience
(e.g. `Scheme.byKey "id"`), and documents the fractional-index recipe; it does
not implement fractional indexing.

### `Scheme` may widen only if Step 4 needs it

If Step 4 decides to flatten non-text containers, `RootName : Path -> string`
likely needs the element/kind at the path to place containers — at which point a
wider surface (e.g. `Locate : Path -> Kind -> Location`) is considered. If Step 4
keeps the hybrid, `RootName` stays minimal and id-aware. Don't widen speculatively.

## Work breakdown — incremental, verify after every step

### Step 0 — Research + spikes (go online; record findings here)

Pure investigation, like plan 0002's Step 0. **Go online** (WebSearch/WebFetch)
and, where feasible, **run** what you find:

- Re-confirm the load-bearing Yjs assumptions: root get-or-create idempotency at
  scale (A1), nested concurrent create clobber (A3), and how reorder interacts
  with root names. Spike against the real `yjs` (plain JS is fine for raw checks,
  mirroring 0002's approach) before trusting docs.
- Survey **and try** ecosystem options relevant to keyed/ordered nested state:
  `y-utility`/`YKeyValue`, fractional-indexing libraries (e.g. the common
  `fractional-indexing` package and any Yjs-specific ones), subdocuments. Note
  maintenance, fit, and whether they actually behave as claimed in a small spike.
- Write a **"Step 0 — findings"** subsection in this file capturing: what was
  validated, what was tried (with the observed result), and how it steers Step 1
  (identity convention) and Step 4 (containers).

- **Exit check:** findings recorded here; the Step 1 and Step 4 decisions have
  concrete, spike-backed evidence to draw on. No production code.

### Step 0 — findings

*Done 2026-06-27. Spiked against `yjs` 13.6.30 (the version this repo pins) with
plain-JS scripts; surveyed + ran `fractional-indexing`; read `y-utility`.*

**Yjs assumptions re-confirmed (spiked, not just read).**

| # | Claim | Spike result |
|---|---|---|
| A1 | Two peers independently get-or-create the *same* top-level root → converge, both edits survive | ✅ both `getText('body')` → `"AAABBB"` on both docs |
| A3 | Two peers concurrently *first-create* a **nested** shared type under the same key → **clobber** | ✅ both `root.note` → `"BBB"`; one side's `"AAA"` lost |
| — | **Positional** root names (`items.<index>.body`) under concurrent insert/reorder | ✅ **broken as predicted** — two *different* logical items merged into one root (`"Q-textP-text"`) |
| — | **Immutable-id** root names (`items.<id>.body`) under the same concurrent insert/reorder | ✅ each id keeps its own text; concurrent edits to the shared item **merge** (`"X-text-d2edit-d1edit"`); no cross-item clobber |

So the plan's foundation holds: flattening collaborative leaves to **A1 roots** is
correct, **A3** is why we don't create nested shared types per item, and
**positional naming is genuinely unstable** while **id naming is stable**.

**The sharpening for Step 1 — naming id must be *immutable*.** A fractional index
is *mutable by design* (you change it to reorder), so it is the wrong thing to
**name** a root by — renaming a text root on every reorder would orphan its CRDT
history. The two concerns are distinct and a reorderable list needs **both**:

- an **immutable item id** (a guid minted at creation) → the **`Scheme` root name**
  (`items.<guid>.body`), stable across reorder; this is what makes the text
  converge;
- a **fractional `order` field** (mutable, LWW string) → **ordering only**; reorder
  = update this field, items are sorted by it.

This was validated end-to-end with `fractional-indexing`'s `generateKeyBetween`:
a list of `{ id(immutable), order(fractional) }` with text named by `id`, edited +
reordered concurrently on two peers, **converged** (both agree order `idP, idX,
idQ`) and the shared item's text **merged both edits** (`"X21"`). `generateKeyBetween`
behaves as documented (`a0`, `a1`, `a0V` between) and is a tiny, dependency-free,
widely-used rocicorp package — a good thing to *point consumers at*, not bundle.

**Ecosystem survey (for Step 4 — containers).**

- **`y-utility` `YKeyValue`** — stores `{key,val}` pairs in **one top-level
  `Y.Array`**, no nested shared types; concurrent same-key adds are **LWW**
  (rightmost wins, older entries GC'd). Relevance: a single id-keyed *top-level*
  array sidesteps the **A3 nested-create race** for *value* records (there is no
  per-item nested type to race on), at the price of LWW values and "no nested Yjs
  types". A real candidate for Step 4's non-text container layout — but it does
  *not* give structural/CRDT merge of container contents.
- **Subdocuments** — noted as an option for fully isolating per-item state into
  its own `Y.Doc`; heavier (load/sync lifecycle per subdoc). Not spiked; parked
  for Step 4 to weigh only if YKeyValue/flattening don't fit.

**How this steers the plan.**

- **Step 1:** the identity that reaches the `Scheme` must be an *immutable* id.
  Lean toward a `PathSegment.KeyById of string` emitted by the list walk from a
  consumer-supplied resolver (the resolver reads the item's immutable id field);
  `Scheme.byKey "id"` as the convenience. The fractional `order` stays ordinary
  model state encoded with `Encode.value` — the library does **not** implement
  fractional indexing (consumers bring `fractional-indexing` or similar).
- **Step 4:** the spike-backed container options are (a) flatten containers to
  roots by id (consistent with text/custom), or (b) a single top-level
  `YKeyValue`-style id-keyed array for value records (no nested-create race, LWW
  values). Decide there with a written rationale, per *Open question*.

### Step 1 — Decide & document the identity convention

From Step 0, choose how identity reaches the scheme: `KeyById` segment +
resolver / reserved id field / scheme-reads-element. Record it (it fixes the
wire format for id-named roots).

- **Exit check:** decision under *Decisions*; types compile; suite green.

### Step 2 — Identity-aware connect + `Scheme` support

`connect`'s `AList` walk surfaces item identity per the convention; add an
id-aware naming path (id-aware `flat` and/or `Scheme.byKey`). Keep positional
`Scheme.flat`.

- **Test:** a nested-list text field is named by id, not index, given a resolver.
- **Exit check:** stable names available; suite green.

### Step 3 — Convergence under reorder

Headline test: a list of objects each with a collaborative text field; two peers
concurrently reorder/insert items **and** edit text, using a consumer-supplied
stable id; after sync the right text stays with the right item and edits merge.
(Use a guid or a fractional index as the id — whichever the spike in Step 0
showed is cleanest to demo.)

- **Exit check:** the reorder case works *given a stable id*; the index caveat is
  now a documented consumer choice, not a defect. Pins A1 + the convention.

### Step 4 — Containers (open; per Step 0 findings)

Execute whatever *Open question* resolved to: flatten containers to roots, keep
the hybrid (with a test-backed statement of guarantees), or wire in the chosen
extension. Follow the same incremental, test-each-step discipline; if it reshapes
the `withYlmish` non-text read path, mirror plan 0002's care there.

- **Exit check:** the resolved option is implemented or the hybrid is formally
  documented + tested; suite green.

### Step 5 — Example + docs

A reorderable collaborative list in/near `TodoCollaborative`; README guidance on
`Scheme.flat` vs id-based naming, and a worked **fractional-index** recipe for
reorderable lists (pointing at an off-the-shelf library, not a bundled one).

- **Exit check:** docs match behaviour; example runs.

## Assumptions / risks

- **Wire-format permanence.** Id-named roots are persisted; the Step 1 convention
  is a deliberate, hard-to-reverse decision — hence the Step 0 research first.
- **Identity is the consumer's.** Stable naming needs per-item ids the consumer
  provides (fractional index / guid). Models without ids keep `Scheme.flat` and
  its documented positional caveat — which is fine (decided).
- **Extension risk (Step 0).** Any adopted Yjs extension must be *tried*, not
  assumed; check it converges and is maintained before depending on it.
- **Step 4 scope.** Full container flattening reshapes the 0002 hybrid read path;
  only undertake it with evidence from Step 0 and step-by-step verification.

## Out of scope

- The merge *strategy* of a field — that is plan 0003 (`CustomElement`).
- **Implementing** fractional indexing in the library (consumers bring their own).
- Non-Yjs backends; automatic id assignment/migration for existing documents.
