# 0002 — CRDT Text Through the Codec

Resolves [#83](https://github.com/NickDarvey/Ylmish/issues/83): `withYlmish`
materializes state wholesale, so concurrent edits don't CRDT-merge.

## Problem

There are two sync mechanisms in the codebase and the high-level path uses the
wrong one.

1. **Granular attach** (`src/Ylmish/Y.fs` — `Text.attach`, `Array.attach`,
   `Map.attach`). Bi-directional, delta-level observers that map adaptive
   `clist`/`cmap` mutations to/from Yjs deltas. This is the path that yields
   true CRDT convergence. Only `Text.attach` is reachable today, and only from
   the `Y.Text` unit tests.

2. **Whole-tree materialize** (`src/Ylmish/Y.fs` — `Doc.materialize` /
   `Doc.dematerialize`, driven by `Program.withYlmish`). On every Elmish update
   it re-encodes the entire model tree and rewrites the Y.Doc root map
   (`Program.fs:81`, `Y.fs:601-633`), reading back via
   `observeDeep → dematerialize → decode`.

These two are **architecturally irreconcilable**, which is the real root cause —
not merely "strings are stored as atomic `Y.Map` entries":

- CRDT merge requires **stable, identity-preserving shared types that accumulate
  operations over time**. A `Y.Text` merges character edits only because it is
  the *same* object collecting ops across edits.
- `materialize` is the opposite: a **stateless re-projection** that computes
  `keysToDelete` and re-`set`s every key each update. Even if a nested `Y.Text`
  existed, materialize would destroy and recreate it every update, annihilating
  the CRDT history that merging depends on.

Two representational gaps keep path (1) unreachable from the codec:

- **No `Text` element kind.** `Adaptive.Codec.Element` is `Value | AList | AMap`
  (`Adaptive.Codec.fs:99-109`). Nothing routes a field to a `Y.Text`.
- **Incomplete `Element ↔ Y` bridge.** Only the list/value cases are wired
  end-to-end (Objective 1 of `0001-making-ylmish-functional.md`).

## Guiding insight

The README already solves this problem for lists:

> an Elmish model may contain a list and an interaction may add two items into
> that list, but from the outside, we only see the new list, not two 'add'
> operations.

Text is the same problem one level down: we observe **successive immutable
strings** and recover character inserts/deletes by **diffing**. The library's
philosophy already contains the answer; it was never applied to strings. This
plan *completes* the README's `[1][2][3][4]` pipeline rather than replacing it —
`materialize` was an interim shortcut that bypassed the delta-nature of steps
`[2]`/`[3]`.

## Principles to preserve

1. **MVU stays plain.** A developer's model field is `string`, not a Yjs handle.
2. **Adaptive is the differ.** Successive models → operations. For text, that
   means a string-diff recovers char-ops — the same job Adaptive already does
   for lists.
3. **The codec is the anti-corruption layer.** Merge semantics are *declared*
   per field in the codec, decoupling app schema from state schema.
4. **Declarative, composable, extensible.** Built-in strategies are ordinary
   instances of one open contract, so consumers can add their own without
   forking.

---

## Design

### Merge semantics are a per-field codec choice

The fix is **not** to make every string mergeable — last-writer-wins is correct
for an ID, an enum, or a toggle. The fix is to give developers the vocabulary to
say which fields are collaborative text. Merge semantics become a declarative,
per-field choice:

| Combinator | Yjs backing | Merge semantics | Use for |
|---|---|---|---|
| `Encode.value` | `Y.Map` entry | last-writer-wins | toggles, enums, numbers, IDs |
| `Encode.text` | `Y.Text` | character-level CRDT | prose, collaborative bodies |
| `Encode.list` / `map` / `object` | `Y.Array` / `Y.Map` | structural CRDT | collections |

### The `Text` kind

`Element<'Value>` gains a `Text` case backed by a live `clist<char>`, and
`Y.Element` gains `Text of Y.Text`:

```fsharp
// Adaptive.Codec.fs
type Element<'Value> =
    | Value  of 'Value                            // atomic register (LWW)
    | Text   of clist<char>                       // character CRDT
    | AList  of alist<Element<'Value> option>
    | AMap   of amap<string, Element<'Value> option>
    | Custom of IShareBinding                      // extension seam (see below)
```

```fsharp
// Y.fs
type Element =
    | String of string
    | Text   of Y.Text
    | Array  of Y.Array<Element option>
    | Map    of Y.Map<Element option>
```

`Kind`, `Error`, and the `Element.toAdaptive`/`ofAdaptive` bridge extend to
cover the new case. `Text.attach` already exists and is correct; this finally
makes it reachable from the codec.

### Connect, not materialize (the radical change)

Retire whole-tree materialize from the per-update hot path. Introduce
`Y.Doc.connect`, which walks the encoded Element tree **once** and, per node,
**gets-or-creates** the matching Yjs shared type at a stable location, then calls
the existing `attach` to wire bi-directional delta sync.

- **Get-or-insert is mandatory** (README TODO 6/7). Idempotent
  `doc.getMap/getText/getArray` for roots; for nested types reuse
  `ymap.get(key)` if present, else create-and-set. This is what makes two peers'
  *first* writes converge instead of clobbering each other's root.
- After `connect`, the adaptive model and the Y.Doc are **live-bound**. A local
  Elmish update flows `model → Adaptify.Update → adaptive deltas → attach →
  Yjs deltas` — **O(delta), not O(state)** — and remote Yjs deltas flow back
  through the decode-direction observers. Stable identity ⇒ CRDT history
  preserved ⇒ concurrent edits merge.

`withYlmish` simplifies accordingly: `init` runs `connect(doc, encoded)` once;
each `update` just `transact (options.Update am m)` and lets the observers
propagate. `options.Update` (Adaptify diffing the whole model into the adaptive
model) stays — it is step `[1]` and is correct. Only step `[2]` changes.

### Read-back stops dematerializing the whole doc

With `connect`, the adaptive model is already kept current by the
decode-direction observers, so the subscription stops doing
`observeDeep → dematerialize → decode whole model`. Instead: the adaptive model
changed → run the decoder against the **live** adaptive tree → dispatch `Set`.
This is where the Reader-monad decoder earns its keep: `Decoder<'model,
'Element, 'Result>` already carries the current Elmish model via `Decoder.ask`,
so **non-persisted app fields survive** the round-trip — impossible when each
read produced a fresh tree.

`Doc.dematerialize` survives only as a one-shot snapshot helper (initial
"does the doc already have state?" detection, debugging). It leaves the hot path.

---

## How text deltas are produced — 5(A) now, `Ref<>` later

Adaptify generates a `cval<string>` for a `string` field, and a `cval` is an
atomic register: replacing `"hello"` with `"hełlo"` is a whole-value set, not a
char delta. `Text.attach` needs a `clist<char>`. There are two ways to bridge,
and we ship them in order.

### Now: 5(A) — diff successive string values

`Encode.text : aval<string> -> Encoded<Element<_>>`. The model field stays a
plain `string`. Internally the encoder owns a `clist<char>` that mirrors the
latest string and a `lastKnown : string` of the clist's current contents:

- **Local edit** (Elmish pushes a new string): diff `lastKnown` vs the new
  value (LCS / Myers), apply the minimal `IndexListDelta<char>` to the clist,
  update `lastKnown`. `Text.attach` forwards the delta to the `Y.Text`.
- **Remote edit** (Yjs delta → clist via the decode observer): update
  `lastKnown` from the clist, and the decoder reads `clist → String.Concat` so a
  fresh `string` reaches the Elmish model via `Set`.
- **No echo, minimal deltas.** `lastKnown` is the single reconciliation point:
  local diffs are computed against it (so a model setter that replaces the whole
  string after a remote merge still yields a minimal diff), and remote updates
  refresh it before any local diff runs. The existing `active` reentrancy guard
  prevents a remote-applied change from being re-encoded as a local one.

This keeps Principles 1 and 2 intact: the model is plain immutable F#, and the
Adaptive layer recovers operations from successive snapshots — exactly its
stated job. Cost is one string-diff per change batch, which is negligible for
human-typed text.

`Decode.text : Decoder<_, _, string>` reads the backing `Y.Text`/`clist` as a
`string`, so decoders stay symmetric with `Decode.value`.

### Later: `Ref<>` — retain delta structure in the app model

For fields where the delta structure is important enough to live in the app
model (large bodies, rich text, high-frequency edits), we will add a wrapper
type — sketched as `Ref<'T>` — that the model holds directly. When a field is a
`Ref`, the adaptive model already owns the live `clist`/CRDT structure, so the
app authors deltas directly and the diff step in 5(A) is skipped:

```fsharp
// future, illustrative
type Model = { Body : Ref<CollaborativeText> }   // delta-native, no diffing
// vs today
type Model = { Body : string }                   // plain, diffed by Encode.text
```

Both target the **same** `Element.Text` binding; they differ only in the
*source* of the deltas (diff-derived vs app-authored). `Encode.text` gains an
overload (or `Encode.ref`) accepting a `Ref<>`-backed field and skipping the
diff. Shipping 5(A) first means `Ref<>` is a pure performance/fidelity opt-in,
not a prerequisite — and the plain-`string` path keeps working unchanged.

`Ref<>` is explicitly **out of scope for this plan** beyond reserving the seam;
it gets its own plan once 5(A) lands.

---

## Extension seam — third-party merge strategies

We must let consumers add their own CRDT-backed field types (a counter, a
rich-text type, a custom mergeable structure) **without editing our union or
forking the library**, and without making the common case messy.

The seam is a single open contract, `IShareBinding`, surfaced through the closed
union's one escape-hatch case `Element.Custom`. Well-known kinds
(`Value`/`Text`/`AList`/`AMap`) stay concrete; everything else goes through one
door:

```fsharp
/// A consumer-defined binding between an adaptive source and a Yjs shared type.
/// Built-in text/list/map are themselves expressible as instances of this.
type IShareBinding =
    /// For error reporting / Kind dispatch.
    abstract Kind : Kind
    /// Get-or-create the shared type under the parent container at this key/index,
    /// wire bi-directional sync, and return a disposable that tears both directions down.
    abstract Connect : BindContext -> IDisposable

/// What a binding needs to attach itself: the parent Yjs container, the slot
/// (root name / map key / array index), the shared reentrancy guard, and the doc.
type BindContext = {
    Doc    : Y.Doc
    Parent : ParentContainer            // Root | Map of Y.Map | Array of Y.Array
    Slot   : Slot                       // Named of string | Index of int
    Active : bool ref                   // shared reentrancy guard
}
```

- The built-in `text`/`list`/`map` strategies are refactored to *be* instances
  of this contract, so the extension point is dogfooded rather than bolted on.
- A consumer ships `Encode.myCounter` / `Decode.myCounter` that produce
  `Element.Custom (MyCounterBinding …)`; the bridge dispatches `Custom` to
  `binding.Connect`. No change to our union is required for new strategies.
- The closed cases keep the common path tidy and exhaustive-match-friendly; the
  single `Custom` case absorbs open-ended growth. This is the "leave space
  without making everything messy" requirement made concrete.

`Connect` is deliberately the same primitive `Y.Doc.connect` uses internally, so
there is exactly one attach contract in the system.

---

## Work breakdown — incremental, verify after every step

Optimised so each step is **one commit that leaves the build green and adds a
test that proves the step**. Run `npm test` (Fable → Mocha; `npm run
test+watch` for a live loop) after each. The headline capability — concurrent
text edits converging — is proven progressively *earlier and earlier* (element
layer → codec layer → connect layer → Elmish layer) rather than only at the end,
so a regression is caught at the lowest layer that introduced it.

Each step names the **assumption(s)** (A1–A6) it relies on or pins, and its
**exit check** — the concrete thing that must be true to move on. Steps are
strictly dependency-ordered; do not start one until the previous exit check is
green.

### Step 0 — Spike the Yjs assumptions (no production code)

Throwaway-but-keep tests in a new `tests/Ylmish.Tests/Y.Assumptions.fs` that pin
A1, A2, A3, A4, A6 against the real bindings. Pure verification; decides the
nesting-vs-flattening question *before* any design depends on it.

- **Exit check:** all five tests run and their pass/fail is recorded in this doc.
  **A3's result chooses the path** for Step 5 (nested types vs flattened names).
  If A3 fails, the rest of the plan is unchanged except Step 5's location logic.

### Step 1 — Add the `Element.Text` representation (compile-green only)

Add `Element.Text of clist<char>` and `Element.Custom of IShareBinding` to
`Adaptive.Codec.fs`; extend `Kind`, `toKind`, `Error`. No behaviour. The
compiler's exhaustiveness checking forces every `match` on `Element` to
acknowledge the new cases — that *is* the verification.

- **Exit check:** `dotnet build` / `npm test` compiles; existing suite still
  green. No new runtime test yet. Relies on nothing.

### Step 2 — Bridge `Element.Text` ↔ `Y.Element.Text` (element layer, no Elmish)

Add `Y.Element.Text of Y.Text`; complete `Element.toAdaptive`/`ofAdaptive` for
`Text`, and the still-missing `Value`/`Map` cases (plan 0001 Objective 1).

- **Test:** round-trip a `clist<char>` through `ofAdaptive`→`toAdaptive`, assert
  equality; **two raw docs sync a text element and converge** (A6).
- **Exit check:** CRDT merge provably works at the element layer, with zero
  codec or Elmish involvement. Pins **A6**.

### Step 3 — `Encode.text` / `Decode.text` (5A diff mirror, codec layer)

Implement the `string ↔ clist<char>` mirror with `lastKnown` reconciliation and
a diff (LCS first; Myers only if Step's A5 test shows it matters).
`Encode.text : aval<string> -> Encoded<_>`, `Decode.text : Decoder<_,_,string>`.

- **Tests:** whole-string replacement `"hello"`→`"hełlo"` yields a **single-char**
  Y.Text delta (**A5**); remote edit reaches the decoded string; no echo under
  the reentrancy guard; **two codec-encoded text fields interleave on sync**.
- **Exit check:** the issue's core capability works through the codec, still with
  no `withYlmish`. Pins **A5**.

### Step 4 — `Y.Doc.connect` for a single text root (narrowest connect slice)

Just enough `connect` to get-or-create *one top-level* text root and attach it
bi-directionally. No nesting, no list/map yet — relies only on **A1**.

- **Test:** the **acceptance scenario, early** — two docs `connect`ed to the same
  named text root, concurrent edits at different offsets, no pre-sync, exchange
  updates both ways, assert interleaved result.
- **Exit check:** the headline #83 fix is demonstrably real at the connect layer
  *before* the `withYlmish` rewire. Pins **A1**.

### Step 5 — Generalise `connect` to full trees via `IShareBinding`

Walk the whole `Encoded<Element<_>>` tree; define `IShareBinding`/`BindContext`
and refactor `text`/`list`/`map` attach into instances of it. Apply the Step 0
decision: nested shared types **or** flattened top-level names. Keep all location
logic behind one function so the choice stays swappable.

- **Tests:** nested model (list of objects with a text field) converges across two
  docs; mismatched-kind re-fetch rejected (**A2**); deleting an attached key
  tears down cleanly (**A4**).
- **Exit check:** arbitrary codec trees connect and merge. Pins **A2, A4**;
  realises the Step 0 **A3** decision.

### Step 6 — Rewire `withYlmish` write path to `connect`

`init`: `connect` once (decode existing state via the live tree). `update`: drop
`materialize`; just `transact (options.Update am m)` and let observers
propagate.

- **Test:** existing `TodoCollaborative` sequential-sync tests pass unchanged
  against `connect` instead of `materialize` (behaviour-preserving swap).
- **Exit check:** no materialize in the write hot path; suite green.

### Step 7 — Rewire `withYlmish` read path to live decode + `ask`

Replace the `observeDeep → dematerialize → decode-whole-model` subscription with:
adaptive model changed → decode the **live** tree → dispatch `Set`. Use
`Decoder.ask` so non-persisted app fields survive.

- **Test:** remote edit reaches the Elmish model; a **non-persisted field set in
  the model survives a remote round-trip** (the `ask` test).
- **Exit check:** read path is O(delta) and model-preserving. `dematerialize`
  now used only as a one-shot snapshot helper.

### Step 8 — End-to-end acceptance test through `withYlmish`

The full Elmish-level test: two `withYlmish` programs over two synced docs make
concurrent edits to the same text field; assert interleaved convergence in the
**Elmish models**, not just the docs.

- **Exit check:** #83 closed at the top layer. This is the same scenario as Step
  4, now proven end-to-end. If it only passes via the flattened-name fallback,
  record that here.

### Step 9 — Example + docs

Give `examples/TodoCollaborative` a collaboratively-edited body field via
`Encode.text`/`Decode.text`; update the README merge-semantics table; note
`Ref<>` as a reserved future seam.

- **Exit check:** `npm run demo` shows two peers merging body text; docs match
  shipped behaviour.

### Dependency / verification map

```
Step 0 (spikes) ─┬─> Step 2 (element merge, A6)
                 │       └─> Step 3 (codec merge, A5)
                 │               └─> Step 4 (connect-1-root merge, A1) ── EARLY headline proof
                 │                       └─> Step 5 (full trees, A2/A4, A3-decision)
Step 1 (repr) ───┘                               └─> Step 6 (write path)
                                                         └─> Step 7 (read path, ask)
                                                                 └─> Step 8 (e2e acceptance)
                                                                         └─> Step 9 (example/docs)
```

Steps 0 and 1 are independent and can land in either order. Everything else is a
chain; each link is a green commit.

---

## Assumptions to validate by test

Every objective below carries assumptions about Yjs behaviour that we are **not
confident in** and must pin with a test *before* building on them. Each test
asserts the assumption directly so that, if Yjs does not behave this way, we find
out at the seam rather than three layers up. The riskiest is nested
get-or-create (A3) — flagged as a likely design-forcing failure.

| # | Assumption | Confidence | Pinning test | If it's false |
|---|---|---|---|---|
| A1 | `doc.getMap/getText/getArray(name)` is idempotent — repeated calls return the *same* root shared type, and two peers naming the same root converge on one type after sync. | High (documented Yjs) | Call twice on one doc, assert reference equality; two docs get same-named root, edit each, sync, assert convergence. | Roots must be created once and cached by `connect`; never re-fetched per update. |
| A2 | A root fetched as one type can never be safely re-fetched as another type (`getMap` then `getText` on the same name). | Medium | Assert that mismatched re-fetch throws or is detectably wrong; `connect` must guard against it. | `connect` must record the kind per root name and reject schema drift loudly. |
| **A3** | **Nested get-or-create is convergent**: two peers that both find `ymap.get(key)` absent and both create-and-set a new nested `Y.Map`/`Y.Text` there will *converge*, not clobber. | **Low — likely FALSE** | Two docs, no initial sync; both create the same nested key as a fresh `Y.Text`; both insert different text; sync both ways; assert **both** insertions survive. | **Design change forced** (see below): represent nesting by flattened top-level names so only A1 idempotency is relied on. |
| A4 | Applying a remote update that *deletes* a root key the local peer is actively `attach`ed to does not leave a dangling observer / null deref. | Low | Attach to a key, apply a remote update removing it, assert the observer tears down cleanly and the model reflects removal. | `attach` lifecycle must subscribe to parent structural events, not just the child. |
| A5 | `Encode.text`'s `clist<char>` ↔ `Y.Text` mirror produces a **minimal** delta for a whole-string replacement (i.e. `lastKnown` reconciliation works), not a full clear+reinsert. | Medium | Replace `"hello"`→`"hełlo"`, assert the Y.Text delta is a single-char insert, not delete-5/insert-6. | Acceptable for correctness; revisit diff algorithm (Myers) before claiming efficiency. |
| A6 | A `Y.Text` created standalone and *then* inserted into a parent `Y.Map`/`Y.Array` retains its content and identity (needed if `connect` builds children before parenting). | Medium | Create `Y.Text`, set content, `ymap.set(key, ytext)`, assert content intact and `.doc` is now the parent doc. | `connect` must create children *via* the parent (`parent.set` returning the integrated type) rather than standalone-then-attach. |

**A3 is the load-bearing risk.** If the failing-then-passing concurrency test
(Objective E) can't be made to pass with nested get-or-create, the fallback —
already anticipated by README TODO 6 — is to **only use top-level named shared
types** and encode nesting as flattened names (e.g. a field at `items[2].body`
becomes a root `Y.Text` named `"items.2.body"` or a stable id). That trades
nested structure for the idempotency we *do* trust (A1). The plan deliberately
keeps `connect`'s location logic behind one function so this swap is local.

## Risks / open questions

- **Diff granularity (5A).** A model setter that replaces the whole string
  produces a minimal delta only because we diff against `lastKnown`. If a
  consumer bypasses the codec and sets wildly divergent strings, deltas grow;
  documented as expected, and the `Ref<>` path is the escape hatch.
- **Reentrancy across the whole doc.** Per-attach `active` guards exist; a
  shared guard threaded through `BindContext` generalizes them. Assume
  single-threaded JS for now (README TODO 2) but keep the guard structured so a
  .NET multi-threaded story is possible later.
- **`getMap()` root identity** (README TODO 6). `connect` must commit to a
  documented root convention (named roots, nested by get-or-insert) so repeated
  calls and multiple peers agree on the same shared types.
- **Schema evolution / split directions** (plan 0001 Objective 6). `connect`'s
  encode and decode directions should remain separable so encode and decode can
  use different schemas across versions; the `IShareBinding` contract keeps both
  directions in one place but they need not be symmetric.

---

## Out of scope

- `Ref<>` implementation (reserved seam only).
- Dependency/toolchain upgrades (plan 0001 Objective 0).
- Ycs / Yrs backends.
