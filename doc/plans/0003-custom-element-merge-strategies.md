# 0003 — CustomElement: consumer-defined merge strategies

Completes the extension seam reserved by
[0002](./0002-crdt-text-through-the-codec.md): let library *consumers* add their
own CRDT-backed field types (a counter, a rich-text type, a mergeable set) —
without editing our `Element` union or forking the library — through the
`Element.Custom of CustomElement` case.

Parent: plan 0002 (the `#83` collaborative-text work). No separate issue yet.

## State

**Last updated:** 2026-06-27 · **Status: COMPLETE.** All steps (0–6) done.
`BindContext`/`Slot`/`ParentContainer` + `CustomElement.Connect` (Option A);
structural path skips `Element.Custom`; `connect` dispatches it to a
scheme-named root (`Parent = Root`, A3-safe) through one shared `dispatch`;
public `Encode.custom` / `Decode.custom` with a consumer counter that **sums**
concurrent increments; the built-in `Text` connects through the *same*
`CustomElement.Connect` contract (`Text.binding`); and a runnable
`examples/TodoCollaborative/Counter.fs` (printed by `npm run demo`) plus a
README "Writing a custom element" section. *(124 tests green; demo runs.)*

One known gap remains — custom fields are not yet wired into the `withYlmish`
*model-readback* path; see *Follow-ups*.

### Progress

- [x] **Step 0** — Option-A skeleton (layering already decided): `Adaptive.Codec.fs`
  opens `Yjs`; `BindContext` / `ParentContainer` / `Slot` name the Fable.Yjs `Y`
  types directly. Compile-green skeleton only. *(121 tests green.)*
- [x] **Step 1** — Add `Connect : BindContext -> IDisposable` to `CustomElement`
  (`BindContext` / `ParentContainer` / `Slot` already in place from Step 0).
  Compile-green. *(121 tests green.)*
- [x] **Step 2** — Skip `Element.Custom` in the structural path
  (`materialize`/`elementToY` AMap iteration skip it; `mergeReadback` takes the
  live side) exactly as `Text` is skipped — custom lives in its own root. Bare
  `Custom` arms in `elementToY`/`ofAdaptive` `failwith` "connect-managed root"
  (mirroring `Text`). *(122 tests green; new: "materialize skips a Custom field…".)*
- [x] **Step 3** — Dispatch `Element.Custom` in `Y.Doc.connect` to
  `binding.Connect ctx`, flattening it to a scheme-named top-level root
  (`Parent = Root`, A3-safe — same discipline as `Text`). Proven by an in-test
  grow-only counter (Y.Array of ticks) whose concurrent increments merge across
  two peers (no LWW). *(123 tests green.)*
- [x] **Step 4** — Public `Encode.custom` / `Decode.custom` helpers (the consumer
  threads one merged-value cell into both, so reading never reaches inside the
  opaque binding). A consumer-style grow-only counter (Y.Array of ticks) sums
  two peers' concurrent increments, decoded back to the same value on both.
  *(124 tests green.)*
- [x] **Step 5** — Dogfood: `Text.binding` expresses the built-in text attach as
  a `CustomElement`; `connect`'s `dispatch` builds one `BindContext` and calls
  `binding.Connect` for *both* text and consumer customs — one attach contract.
  `Element.Text` stays a concrete case (ergonomics + exhaustiveness). All text
  tests unchanged. *(124 tests green.)*
- [x] **Step 6** — Example + docs: `examples/TodoCollaborative/Counter.fs` (a
  grow-only counter on the public seam), exercised in-process by `npm run demo`
  (`[counter] merged value: A=2 B=2 — the SUM`); README gains a `Custom` row in
  the merge-semantics table and a "Writing a custom element" subsection.
  *(124 tests green; demo runs.)*

### Decisions & lessons

- **Layering: Option A (decided).** `CustomElement` — the type named by
  `Element.Custom` — must live in `Adaptive.Codec.fs`, which compiles **before**
  `Y.fs`, so it *cannot* reference `Ylmish.Y` (that would be a backward
  dependency). It *can* reference the **Fable.Yjs** bindings (`Yjs.fs`:
  `Y.Doc`/`Y.Map`/`Y.Array`), a project dependency available everywhere. So:
  - the **codec** `open`s `Yjs` and `BindContext` / `ParentContainer` name those
    Fable.Yjs `Y` types directly — no abstraction layer, no Y-agnostic hedge;
  - the **dispatch** (`Y.Doc.connect`) and the **binding implementations**
    (built-in and consumer) live in `Y.fs` (or consumer code) and use the
    `Ylmish.Y` helpers — `Text.attach`, `CompositeDisposable`, etc. — directly.

  This accepts a `codec → Fable.Yjs` reference, which is benign: the codec's
  whole purpose is to map to Yjs, and Ylmish already depends on Fable.Yjs. The
  anti-corruption boundary is app-schema-vs-state-schema, not "avoid Yjs". Option
  B (a Y-layer sub-contract resolved by cast) is rejected as needless indirection.

- **`Decode.custom` reads a consumer-threaded value cell, not the element.** A
  `CustomElement` is opaque (`Kind` + `Connect`), so unlike `Decode.text` — which
  reads the `clist` *inside* `Element.Text` — `Decode.custom` reads an
  `aval<'a>` the consumer threads into both `Encode.custom`'s binding and the
  decoder. Clean and Fable-safe (no downcast), but it means custom round-tripping
  is a same-scope concern; see *Follow-ups* for what this implies under
  `withYlmish`.

- **One attach contract (Step 5).** Built-in `Text` and consumer customs both
  flow through `connect`'s single `dispatch` → `BindContext` → `binding.Connect`.
  `Element.Text` stays a concrete case for ergonomics/exhaustiveness; only the
  *connect primitive* is shared (`Text.binding`), not the union case.

### Blockers

- None.

### Follow-ups (out of this plan's scope; for a future plan)

- **Custom fields aren't in the `withYlmish` model-readback path (known gap).**
  `withYlmish`'s `subs` wires two readback triggers: the `"ylmish-ydoc"` observer
  on the structural root map, and the `"ylmish-text"` observer whose `gather`
  walks the encoded tree collecting **`Element.Text`** leaves and observing their
  `clist`s. `gather`'s catch-all skips `Element.Custom` (and the structural
  observer never sees a custom's own root). **Consequence:** a *remote* edit to a
  custom field merges into its Y root and updates the binding's value cell, but
  no `Set` is dispatched, so the Elmish model doesn't reflect it until some other
  change triggers a readback. Customs therefore fully work at the **connect
  layer** (proven by tests + the demo) but are **not yet first-class under
  `withYlmish`**. Fixing it means giving `gather`/readback a way to observe a
  custom's merged-value cell — most naturally by having `CustomElement` expose an
  observable "changed" signal (or its value `aval`) that connect subscribes to,
  the same way it subscribes to text `clist`s. That also subsumes the
  value-cell-threading awkwardness above (the binding could surface its value
  directly). This is the natural **plan 0005**.

### Agent pickup prompt

> You are continuing plan 0003. Work **one step at a time**, in order, and keep
> the suite green. Each iteration:
>
> 1. **Pick** the first unchecked item in **Progress**. Read its full entry in
>    *Work breakdown* and the decisions it depends on.
> 2. **Implement** just that step — the smallest change that satisfies its exit
>    check. Match surrounding code style. Add the step's test(s).
> 3. **Verify.** Ensure the .NET SDK is on `PATH` (`export PATH="$HOME/.dotnet:$PATH"`;
>    install via `dotnet-install.sh --version 10.0.300` if missing), then run
>    `npm test`. All tests must pass and the new test(s) must be present and green.
> 4. **Update State.** Tick the step, bump *Last updated* / *Next step*, and
>    record any decision/lesson. If blocked, leave the box unchecked, write the
>    blocker under *Blockers*, and stop for the user.
> 5. **Commit & push** one focused commit to the working branch (no PR unless asked).
> 6. **Compact context**, then continue with the next step.
>
> Stop when every step is checked, or when a blocker needs a human decision.

## Problem

`CustomElement` is reserved but inert:

```fsharp
type CustomElement =
    abstract Kind : Kind                 // that's all there is, today

type Element<'Value> =
    | Value | Text | AList | AMap
    | Custom of CustomElement            // every bridge currently throws on this
```

`Y.Doc.connect`, `Element.ofAdaptive`, and `elementToY` all `failwith` on
`Element.Custom`. So a consumer cannot, today, define a field whose merge
behaviour isn't one of the four built-ins. The built-in `Text`/`AList`/`AMap`
strategies are also hard-wired into `connect`'s walk and the `Y.fs` attach
modules — they are *not* expressed through one shared contract, so the seam is
neither usable nor dogfooded.

## Goal

A consumer ships `Encode.myThing` / `Decode.myThing` that produce
`Element.Custom (MyBinding …)`, where `MyBinding` knows how to get-or-create its
Yjs shared type and wire bi-directional, delta-level sync. `Y.Doc.connect`
dispatches it. Nothing in our union changes; nothing is forked. The built-in
text path is (eventually) re-expressed as one such binding, proving the seam.

## Design

### The contract grows a `Connect`

Per the layering decision (Option A), `Adaptive.Codec.fs` `open`s `Yjs` and the
types below name the Fable.Yjs `Y` bindings directly:

```fsharp
// What a custom element needs in order to attach itself.
// `Y.Doc` / `Y.Map` / `Y.Array` here are the Fable.Yjs bindings.
type BindContext = {
    Doc    : Y.Doc            // the document
    Parent : ParentContainer // where this element is placed
    Slot   : Slot            // its slot within the parent
    Active : bool ref        // the shared reentrancy guard threaded by connect
}
and ParentContainer =
    | Root                   // a top-level root, named by Slot (the A3-safe case)
    | InMap   of Y.Map<obj>
    | InArray of Y.Array<obj>
and Slot =
    | Named of string        // root name / map key
    | Index of int           // array index

type CustomElement =
    abstract Kind    : Kind
    /// Get-or-create the shared type at (Parent, Slot), wire both sync
    /// directions, and return a disposable that tears both down.
    abstract Connect : BindContext -> IDisposable
```

`Y.Doc.connect`'s walk, on `Element.Custom binding`, builds a `BindContext` from
the current parent/slot/active-guard and calls `binding.Connect ctx`, adding the
returned `IDisposable` to its `CompositeDisposable`. `Connect` is deliberately
the *same* primitive the built-ins use, so there is exactly one attach contract.

### Custom elements live in their own root (like `Text`)

A `Custom` element is connect-managed, not part of the structural root map. So
every structural-path site that already skips `Text` must also skip `Custom`:
`materialize` / `elementToY` (don't write it to the root map),
`Element.ofAdaptive` / `elementToY` (no `Y.Element` projection), and
`withYlmish`'s `mergeReadback` (take it from the live side, never from
`dematerialize`). This keeps `connect` ⟂ `materialize` composition intact.

### Naming is still the `Scheme`'s job

A custom binding receives a `Slot`; it does not invent root names. The
`Codec.Scheme` (plan 0004) decides the name, so layout stays in one place. A
binding author is responsible only for *convergent placement given a slot* —
and, per plan 0002's A1/A3 findings, the safe default is a top-level root
(`Parent = Root`), never a freshly-created nested shared type both peers race to
make.

## Work breakdown — incremental, verify after every step

### Step 0 — Option-A skeleton (compile-green)

Layering is decided (Option A — see *Decisions*), so this step just lays the
wiring: `Adaptive.Codec.fs` `open`s `Yjs`; add the `BindContext` /
`ParentContainer` / `Slot` types naming the Fable.Yjs `Y` bindings directly.
`CustomElement` keeps only `Kind` for now (the `Connect` member lands in Step 1).
No dispatch; bridges still `failwith` on `Custom`.

- **Exit check:** the new types compile with the codec referencing Fable.Yjs;
  suite green. (If the codec turns out *not* to need `open Yjs` until `Connect`
  is added in Step 1, fold this into Step 1 — but keep the Option-A decision.)

### Step 1 — Add `Connect` to `CustomElement` (compile-green)

With `BindContext`/`ParentContainer`/`Slot` in place (Step 0), add
`abstract Connect : BindContext -> IDisposable` to `CustomElement`. No dispatch
yet; `connect`/bridges still `failwith` on `Custom`.

- **Exit check:** compiles; suite green. The exhaustiveness checker keeps forcing
  every `Element` match to acknowledge `Custom`.

### Step 2 — Skip `Custom` in the structural path

Mirror the `Text` skips: `materialize`/`elementToY` omit `Custom`;
`Element.ofAdaptive`/`elementToY` no longer need to project it; `mergeReadback`
treats `Custom` like `Text` (live side wins).

- **Test:** a model with a `Custom` field still `materialize`s its non-custom
  fields without error (the custom field is simply absent from the root map).
- **Exit check:** structural path never throws on `Custom`; suite green.

### Step 3 — Dispatch `Custom` in `connect`

In `connect`'s `walk`, on `Element.Custom binding`, build `BindContext` and call
`binding.Connect`. Handle both a top-level `Custom` (Parent = Root, Slot = the
scheme name) and a nested one (under a Map/Array — though prefer Root per A3).

- **Test:** an in-test `CustomElement` backed by, e.g., a `Y.Map` LWW register or
  a `Y.Array`-based grow-only counter; two docs `connect` it, edit concurrently,
  sync, assert convergence.
- **Exit check:** a custom element merges through `connect`. Relies on A1.

### Step 4 — Consumer-style helpers + a counter

Provide the ergonomic surface a consumer uses: `Encode.custom` / `Decode.custom`
(or a documented constructor) producing `Element.Custom`. Ship a real example
binding — a counter (grow-only or PN) backed by a `Y.Map`/`Y.Array`.

- **Test:** two peers increment the counter concurrently; the merged value is the
  sum (no lost increments) — something LWW could never do.
- **Exit check:** a consumer can define & use a custom element end-to-end.

### Step 5 — Dogfood the built-in `Text`

Route `Text`'s connect through the same attach primitive `CustomElement.Connect`
uses (one attach contract). Keep `Element.Text` concrete for ergonomics and
exhaustiveness — this is about *sharing the connect primitive*, not collapsing
the case. Decide during implementation whether `Text` literally becomes a
`CustomElement` instance internally or just shares the helper.

- **Test:** all existing text tests stay green; no behaviour change.
- **Exit check:** there is one attach/connect contract in the system.

### Step 6 — Example + docs

Add a `CounterElement` near `examples/TodoCollaborative` (or a focused sample),
and a README "writing a custom element" subsection alongside the merge-semantics
table.

- **Exit check:** docs match shipped behaviour; example runs.

## Assumptions / risks

- **Same A1/A3 discipline.** A custom binding that creates a *nested* shared type
  both peers race to make will clobber (A3). The safe placement is a top-level
  root (A1). The `BindContext` exposes `Parent = Root`; document that bindings
  should prefer it. Pins **A1/A3** (already validated in 0002).
- **Reentrancy guard sharing.** `Active` is threaded from `connect`; a binding
  must honour it across both directions, like the built-in attach does.
- **Lifecycle.** `Connect` must return a disposable that tears down *both*
  directions; it composes into `connect`'s `CompositeDisposable`.
- **Layering coupling (decided).** Option A couples the codec to Fable.Yjs.
  Accepted; the only thing that would reopen it is a non-Yjs backend (Ycs/Yrs),
  at which point `BindContext` would need to be parameterised over the backend.

## Out of scope

- Naming/layout of roots — that is plan 0004 (`Scheme`).
- Non-Yjs backends.
- Collapsing the built-in `Text`/`AList`/`AMap` cases into `Custom` (kept concrete).
