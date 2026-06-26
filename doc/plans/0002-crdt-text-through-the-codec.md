# 0002 — CRDT Text Through the Codec

Resolves [#83](https://github.com/NickDarvey/Ylmish/issues/83): `withYlmish`
materializes state wholesale, so concurrent edits don't CRDT-merge.

## State

**Last updated:** 2026-06-26 · **Next step:** Step 6 (rewire `withYlmish` write
path to compose `connect` for text with the structural path for non-text).

### Progress

- [x] **Step 0** — Spike the Yjs assumptions — **DONE.** All six spikes
  (`tests/Ylmish.Tests/Y.Assumptions.fs`) green in the F#→Fable→Mocha suite and
  reproduced in plain-JS yjs 13.6. **A3 confirmed FALSE (clobber)** → Step 5
  takes the flattened-top-level-name path. A1/A6 confirmed, A2 throws, A4 ok.
- [x] **Step 1** — Add the `Element.Text` representation — **DONE.** Added
  `Element.Text of clist<char>` and `Element.Custom of IShareBinding` to
  `Adaptive.Codec.fs` (plus `Kind.Text`/`Kind.Custom`, `toKind`, a minimal
  `IShareBinding`). Suite still **105 passing**.
- [x] **Step 2** — Bridge `Element.Text` ↔ `Y.Element.Text` — **DONE.** Added
  `Y.Element.Text of Y.Text`; wired `Element.toAdaptive`/`ofAdaptive` (both
  Fable + .NET) to route `Text` through the existing `Text.attach`. New tests in
  `Y.Element.fs`: clist round-trip + two-doc convergence through the bridge.
  **Fixed a real empty-start encode bug** (see lessons). Suite **107 passing**.
- [x] **Step 3** — `Encode.text` / `Decode.text` — **DONE.** `Encode.text :
  aval<string> -> Encoded<_>` owns a stable `clist<char>` and mirrors successive
  strings into it via a minimal common-affix diff; `Decode.text` reads the live
  clist back as a string. New `Codec.Text.fs`: round-trip, **A5 minimal delta**,
  reactive decode, and a codec-level two-field interleave-on-sync. Suite
  **111 passing**.
- [x] **Step 4** — `Y.Doc.connect` for a single text root — **DONE.**
  `Y.Doc.connect doc encoded` get-or-creates each top-level text field as a
  `Y.Text` root keyed by the field name (A1) and `Text.attach`es it, returning a
  `CompositeDisposable`. New `Y.Doc.fs` "connect" test: two docs converge on
  concurrent edits with no pre-sync. The #83 headline fix is real at the connect
  layer. Suite **112 passing**.
- [x] **Step 5** — Generalise `connect` via a pluggable **`Scheme`** seam —
  **DONE** (pragmatic A3-safe slice, per the user's decision). `connect` now
  walks the tree recursively and flattens every **text** leaf (incl. nested in
  objects/lists) to a top-level `Y.Text` root named by a `Codec.Scheme`;
  `Scheme.flat` is the A3-safe default and consumers can pass their own
  (`connectWith`). Non-text leaves are skipped (left on the structural/LWW path,
  composed in Step 6). Tests: nested-text flatten, custom-scheme seam, multi-root,
  A4 teardown. **Deferred:** A2 kind-drift guard, the `IShareBinding` dogfood
  refactor, and full path-flattening of *non-text* containers. Suite
  **116 passing**.
- [ ] **Step 6** — Rewire `withYlmish` write path to `connect`.
- [ ] **Step 7** — Rewire `withYlmish` read path to live decode + `ask`.
- [ ] **Step 8** — End-to-end acceptance test through `withYlmish`.
- [ ] **Step 9** — Example + docs.

### Decisions & lessons

- **A3 is false (clobber).** Nested concurrent get-or-create loses one peer's
  data; Step 5 uses flattened top-level names, relying only on A1.
- **A2 throws** on root type drift → `connect` records a kind per root name and
  surfaces a clear schema-drift error.
- **A6:** `ymap.set(key, ytext)` integrates the handle *in place* (`t === read
  back`); always edit/observe via the integrated handle.
- **Initial-state reconciliation is deferred to Step 6/7 (noted in Step 4).**
  `connect` only wires `attach`; it does not seed a fresh `Y.Text` from a
  non-empty initial model, nor seed an empty model from existing doc state. With
  the Step 2 empty-start fix, attach's initial-echo is *skipped* when the clist
  starts non-empty, so a model that starts with text would not push that text
  into an empty root. Step 4's test starts both peers empty (in sync), which is
  correct for the A1 slice. `withYlmish` `init` (Steps 6/7) owns the
  materialise-or-decode decision for existing/initial state; `connect` will gain
  an explicit initial reconciliation there.
- **A5 confirmed (Step 3).** A common-affix diff (shared prefix + suffix,
  replace the middle) yields a minimal `clist`/`Y.Text` delta for a single-char
  change (`"hello"`→`"hełlo"` = 2 ops, not a 10-op clear+reinsert). LCS/Myers
  weren't needed; revisit only if multi-region edits in one batch matter.
- **`lastKnown` became "diff against the live clist."** Rather than track a
  separate `lastKnown` string, `Encode.text` diffs the new value against
  `System.String.Concat chars` (the clist's current contents). This is the
  single reconciliation point the plan called for: after a remote merge the
  clist already holds the merged text, so a subsequent whole-string model set
  still diffs minimally, and a value that already matches yields no delta — echo
  suppression falls out for free, complementing the attach `active` guard.
- **Empty-start encode bug (fixed in Step 2, load-bearing for #83).**
  `Text.attachEncode` skipped the *first* `AddCallback` firing to drop the
  initial content echo — but that echo only fires for a **non-empty** list. A
  freshly-created (empty) text field has no echo, so the flag swallowed the
  first real keystroke and it never reached the `Y.Text`. Fix: initialise the
  skip flag to `not (Seq.isEmpty atext)` — non-empty behaviour unchanged, empty
  lists now propagate their first edit. **The same latent bug exists in
  `Array.attachEncode` / `Map.attachEncode`**; fix them when Step 5 exercises
  fresh (empty) lists/maps.
- **Standalone `Y.Text` reads as `""` until integrated** (A6 nuance). A
  `Y.Text.Create "hello"` reports empty `toString()`/content until it is set
  into a doc (`ymap.set`); only then does the pending content materialise. Any
  round-trip/read-back must integrate first. `connect` (Steps 4–5) must create
  children *via* the parent or otherwise integrate before reading.
- **Step 1 layering.** `IShareBinding` lives in the **codec** layer but is kept
  *Y-agnostic for now* (only `abstract Kind : Kind`); the concrete
  `Connect`/`BindContext` surface — which needs Fable.Yjs types — is added in
  Step 5, so Step 1 doesn't drag Y into the codec prematurely. Adding the two
  union cases only forced two real matches to grow (`Element.ofAdaptive`,
  `elementToY` in `Y.fs`); the `Decode` combinators already have catch-alls.
  Both new `Y.fs` arms `failwith` with a "lands in Step 2/5" message — compiles,
  unexercised, so the suite stays green.
- **Env:** the web sandbox has Node but **not** the .NET SDK. Install it with
  `~/.dotnet/dotnet` via `dotnet-install.sh --version 10.0.300` (per
  `global.json`), then `export PATH="$HOME/.dotnet:$PATH"`. Full verify =
  `npm test` (runs `adaptify` then `fable … --run mocha`); baseline is
  **105 passing**.

### Blockers

- None. (The Step 5 wire-format decision below is **resolved** — kept for the
  rationale.)

#### Resolved: Step 5 layout scheme (decision 2026-06-26)

**Decision (user):** ship the *pragmatic A3-safe slice* now (text → flattened
roots; non-text stays on the structural/LWW path), and make the layout a
**consumer seam** — a `Scheme` passed into the encode/decode setup. A full
path-flattened scheme may ship in the box later; consumers can supply their own.
Implemented as `Codec.Scheme` (`RootName : Path -> string`) with `Scheme.flat`
the default, and `Y.Doc.connectWith scheme`. This is the layout counterpart to
the `IShareBinding` merge-type seam (Step 1).

The original analysis (why the schema is load-bearing) is preserved below.

- **Step 5 wire-format design decision (needs a human call).** Generalising
  `connect` beyond text fields fixes the **persisted state schema** (the
  on-the-wire format), which is expensive to change later, and the A3 spike
  forces every collaborative container to be a top-level *root* (A1), not a
  nested shared type. Two sub-problems fall out and the plan deliberately left
  the exact scheme to be chosen at implementation time:
  1. **Scalar (LWW) fields can't be Yjs roots** (roots are only Text/Array/Map),
     so they must live inside a root `Y.Map`. The top level becomes "one root
     value-map **plus** flattened text/list/map roots," not purely flat roots —
     and binding a *subset* of an object's fields (the scalars) to one root map
     while routing its text/list/map fields to other roots is the awkward part
     without the per-leaf `IShareBinding` walk.
  2. **Text nested inside a *list*** (`items[2].body`) needs a *stable* root
     name; indices aren't stable across peers, so this needs a per-item stable
     id the model doesn't currently carry.

  Candidate directions (the question that didn't reach the user):
  - **A — Pragmatic A3-safe slice:** flat top-level object only (text→roots,
    scalars→one root value-map, list/map→structural roots via existing attach);
    deep-nested text relies on dynamic creation / pre-sync; document the
    stable-id limitation.
  - **B — Full path-flattened scheme** (README TODO 6): every nested
    leaf/container is a root keyed by dotted path; forces a list-item id
    convention now.
  - **C — Spec first:** write the flattened-schema design note (scalars, nested
    text, list-item ids, `IShareBinding` shape) for review before any code.
  - **D — Skip to Steps 6–9:** wire `withYlmish` to the current single-object
    connect path (value-map + flattened text roots, top level only), prove
    end-to-end + example, leave deep nesting for a later plan.

  Recommendation: **A** (keeps momentum, A3-safe, defers only the hardest
  sub-problem) — but this fixes the schema, so confirm before committing code.
  The full `IShareBinding`/`BindContext` dogfood refactor is also deferred until
  this is settled; the seam itself already exists (`Element.Custom`, Step 1).

### Agent pickup prompt

> You are continuing plan 0002. Work **one step at a time**, in order, and keep
> the suite green. Each iteration:
>
> 1. **Pick** the first unchecked item in **Progress**. Read its full entry in
>    *Work breakdown* and the assumptions/decisions it depends on.
> 2. **Implement** just that step — the smallest change that satisfies its exit
>    check. Match surrounding code style. Add the step's test(s).
> 3. **Verify.** Ensure the .NET SDK is on `PATH` (`export PATH="$HOME/.dotnet:$PATH"`;
>    install via `dotnet-install.sh --version 10.0.300` if missing), then run
>    `npm test`. All tests must pass and the new test(s) must be present and
>    green. If the step's exit check is behavioural, confirm it explicitly.
> 4. **Update State.** Tick the step, bump *Last updated* and *Next step*, and
>    record any decision/lesson under *Decisions & lessons*. If blocked, leave
>    the box unchecked, write the blocker under *Blockers*, and stop for the
>    user.
> 5. **Commit & push** one focused commit to `claude/github-issues-visibility-8k12g3`
>    (do not open a PR unless asked).
> 6. **Compact context**, then continue with the next step.
>
> Stop when every step is checked, or when a blocker needs a human decision
> (e.g. a runtime spike contradicts a desk verdict, or a step needs an API the
> bindings don't expose).

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

Throwaway-but-keep tests in `tests/Ylmish.Tests/Y.Assumptions.fs` (written and
registered) that pin A1, A2, A3, A4, A6 against the real bindings. Pure
verification; decides the nesting-vs-flattening question *before* any design
depends on it.

- **Status:** spikes written and wired into the suite. A1/A2/A3/A6 reproduced
  empirically against plain-JS yjs 13.6 (results below); A4 runs via the F#
  spike in CI. **A3 confirmed FALSE (clobber)** → Step 5 takes the
  flattened-top-level-name path. The rest of the plan is unchanged.

#### Step 0 — research findings (desk check, 2026-06-26)

Read of the Yjs docs/source and ecosystem *before* writing the runtime spikes.
Desk verdicts below still get a runtime test (docs describe semantics; we
confirm empirically — especially A2 and the exact A3 content-loss behaviour),
but the design direction is already decided.

- **A1 — CONFIRMED.** Top-level types are got-or-created by name via
  `ydoc.get(name, Type)` (the `getMap`/`getText`/`getArray` wrappers).
  "Every peer that defines 'my array' like this will sync content with this
  peer." Repeated same-name calls return the same instance; two peers naming the
  same root converge. → `connect` may safely rely on root get-or-create; create
  roots once and cache them.

- **A3 — CONFIRMED FALSE (this is the load-bearing result).** Y.Map resolves
  concurrent writes to the same key as a register: *"When users
  create/update/delete the same property concurrently, only one change will
  prevail."* So if two peers each find a nested key absent and each
  create-and-set a **fresh** `Y.Text`/`Y.Map` there, the CRDT converges on **one
  survivor and silently discards the other shared type along with every edit
  made into it**. Convergence is preserved; *content is lost*. There is **no
  built-in nested get-or-create** — the safe get-or-create primitive
  (`ydoc.get(name, Type)`) exists at the **root only**. → **We take the
  flattened-top-level-name path for Step 5** (README TODO 6): a field at, e.g.,
  `items.2.body` becomes a root shared type keyed by a stable name/id, relying
  solely on A1. Nested *structure under a single map key* is still fine when only
  one writer ever creates it before divergence; concurrent first-creation is the
  unsafe case we design out.

- **A6 — SUPPORTED.** `ymap.set(key, new Y.Text())` integrates the nested type
  into the parent (`ymap.set(key: …|Y.AbstractType)`). The create-then-parent
  order works **provided we use the integrated handle afterward** (observe/edit
  the value as retrieved from the parent, not the pre-insert local object).

- **A2 — CONFIRMED (throws).** The docs don't state it, but empirically Yjs'
  underlying `get(name, Type)` throws on type mismatch: *"Type with the name x
  has already been defined with a different constructor."* A loud failure is the
  safe outcome; `connect` still records a kind per root name so the error
  surfaces as a clear schema-drift diagnostic rather than a raw Yjs throw.

##### Ecosystem / extensions surveyed

- **`yjs/y-utility` → `YKeyValue`.** A more efficient key-value store than Y.Map.
  Critically: *"Y.Map needs to retain all key values that were created in history
  to resolve potential conflicts"* — so a churny Y.Map bloats the doc (benchmark:
  500k ops over 1k keys = **2.99 MB** as Y.Map vs **31 KB** as YKeyValue), and
  YKeyValue shrinks on delete. **Implications for us:** (1) another nail in
  `materialize`'s coffin — its repeated root set/delete per update would bloat the
  doc unboundedly; (2) `YKeyValue` is a candidate backing for the `AMap` kind /
  the flattened-name root index, and pairs naturally with the A3 flattened-name
  decision. Also ships `YMultiDocUndoManager` (cross-document undo) — relevant
  later if we adopt subdocuments. y-utility is a likely dependency.

- **`YousefED/SyncedStore`.** Closest prior art: presents **plain JS
  objects/arrays over Y.Map/Y.Array**, the same "plain model over a CRDT"
  philosophy Ylmish pursues from F#. Its open issues on **initialization /
  using an existing doc** (#29, #46) map exactly onto our get-or-create and
  "doc already has state" concerns — study its `boxed`/nesting approach for
  lessons, not as a dependency.

- **No off-the-shelf nested get-or-create.** Neither the core nor the surveyed
  extensions provide a convergent nested get-or-insert; this confirms A3 is a
  genuine gap we must design around rather than import a fix for.

Sources: [Y.Map docs](https://docs.yjs.dev/api/shared-types/y.map) ·
[Working with shared types](https://docs.yjs.dev/getting-started/working-with-shared-types) ·
[Y.Text docs](https://docs.yjs.dev/api/shared-types/y.text) ·
[yjs#255 nesting](https://github.com/yjs/yjs/issues/255) ·
[yjs/y-utility](https://github.com/yjs/y-utility) ·
[SyncedStore](https://github.com/YousefED/SyncedStore)

##### Empirical confirmation (plain-JS yjs 13.6, 2026-06-26)

The desk verdicts were reproduced against the real `yjs` package with a small
Node script (same logic as the F# spikes in `tests/Ylmish.Tests/Y.Assumptions.fs`,
run directly because the F#→Fable pipeline needs the .NET SDK):

```
A1.id    PASS same instance
A1.conv  s1="BBBAAA" s2="BBBAAA" converge=true bothSurvive=true
A2.err   Type with the name x has already been defined with a different constructor
A2       PASS throws
A3       s1="AAA" s2="AAA" converge=true bothSurvive=false => CLOBBER (A3 FALSE confirmed)
A6       content="hello!" hasDoc=true sameHandle=true editable=true
```

Every desk verdict held: A1 idempotent + shared-root edits interleave, A2 throws,
**A3 clobbers (one side's text silently lost)**, A6 integrates in place. All six
spikes (A1, A2, A3, A4, A6) also pass in the real F#→Fable→Mocha suite
(`105 passing`). The flattened-top-level-name decision for Step 5 is therefore
confirmed by observation, not just docs.

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
and refactor `text`/`list`/`map` attach into instances of it. **Step 0's desk
check decided the flattened-top-level-name path** (A3 confirmed false): each
collaborative leaf is a root shared type keyed by a stable name/id, relying only
on A1. Keep all location logic behind one function so the choice stays swappable
if the runtime A3 spike surprises us. Consider `YKeyValue` (y-utility) as the
backing for the flattened root index given Y.Map's history-retention bloat.

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
| A1 | `doc.getMap/getText/getArray(name)` is idempotent — repeated calls return the *same* root shared type, and two peers naming the same root converge on one type after sync. | **Confirmed (desk)** — see findings | Call twice on one doc, assert reference equality; two docs get same-named root, edit each, sync, assert convergence. | Roots must be created once and cached by `connect`; never re-fetched per update. |
| A2 | A root fetched as one type can never be safely re-fetched as another type (`getMap` then `getText` on the same name). | **Confirmed — throws (desk + JS)** | Assert that mismatched re-fetch throws or is detectably wrong; `connect` must guard against it. | `connect` must record the kind per root name and reject schema drift loudly. |
| **A3** | **Nested get-or-create is convergent**: two peers that both find `ymap.get(key)` absent and both create-and-set a new nested `Y.Map`/`Y.Text` there will *converge*, not clobber. | **Confirmed FALSE (desk)** — see findings | Two docs, no initial sync; both create the same nested key as a fresh `Y.Text`; both insert different text; sync both ways; assert **both** insertions survive. | **Design change forced** (see below): represent nesting by flattened top-level names so only A1 idempotency is relied on. |
| A4 | Applying a remote update that *deletes* a root key the local peer is actively `attach`ed to does not leave a dangling observer / null deref. | **Confirmed (F# spike)** | Attach to a key, apply a remote update removing it, assert the observer tears down cleanly and the model reflects removal. | `attach` lifecycle must subscribe to parent structural events, not just the child. |
| A5 | `Encode.text`'s `clist<char>` ↔ `Y.Text` mirror produces a **minimal** delta for a whole-string replacement (i.e. `lastKnown` reconciliation works), not a full clear+reinsert. | Medium | Replace `"hello"`→`"hełlo"`, assert the Y.Text delta is a single-char insert, not delete-5/insert-6. | Acceptable for correctness; revisit diff algorithm (Myers) before claiming efficiency. |
| A6 | A `Y.Text` created standalone and *then* inserted into a parent `Y.Map`/`Y.Array` retains its content and identity (needed if `connect` builds children before parenting). | **Confirmed (desk + JS)** | Create `Y.Text`, set content, `ymap.set(key, ytext)`, assert content intact and `.doc` is now the parent doc. | `connect` must create children *via* the parent (`parent.set` returning the integrated type) rather than standalone-then-attach. |

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
