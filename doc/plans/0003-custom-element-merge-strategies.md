# 0003 — CustomElement: consumer-defined merge strategies

Completes the extension seam reserved by
[0002](./0002-crdt-text-through-the-codec.md): let library *consumers* add their
own CRDT-backed field types (a counter, a rich-text type, a mergeable set) —
without editing our `Element` union or forking the library — through the
`Element.Custom of CustomElement` case.

Parent: plan 0002 (the `#83` collaborative-text work). No separate issue yet.

## State

**Last updated:** 2026-06-27 · **Status: NOT STARTED.** `CustomElement` exists
as a one-method contract (`abstract Kind : Kind`) and `Element.Custom` is a
reserved case that every bridge throws on. Next step: **Step 0** (layering
spike).

### Progress

- [ ] **Step 0** — Layering spike: decide where `Connect`/`BindContext` live
  (codec layer referencing Fable.Yjs, vs a Y-layer sub-contract). Compile-green
  skeleton only.
- [ ] **Step 1** — Add `Connect : BindContext -> IDisposable` to `CustomElement`
  and define `BindContext` / `ParentContainer` / `Slot`. Compile-green.
- [ ] **Step 2** — Skip `Element.Custom` in the structural path
  (`materialize`/`elementToY`, `Element.ofAdaptive`/`elementToY`,
  `mergeReadback`) exactly as `Text` is skipped — custom lives in its own root.
- [ ] **Step 3** — Dispatch `Element.Custom` in `Y.Doc.connect` (root + nested)
  to `binding.Connect ctx`. Prove with a trivial in-test binding.
- [ ] **Step 4** — A worked, *consumer-style* custom element (a counter) with
  `Encode`/`Decode` helpers; two peers' counters converge end-to-end.
- [ ] **Step 5** — Dogfood: route the built-in `Text` connect through the same
  attach primitive `CustomElement.Connect` uses, so there is one attach contract.
- [ ] **Step 6** — Example + docs: a `CounterElement` in/near
  `TodoCollaborative`; README "writing a custom element" section.

### Decisions & lessons

- _(none yet)_

### Blockers

- None.

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

```fsharp
// What a custom element needs in order to attach itself.
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

### Step 0 — Layering spike (compile-green skeleton)

`BindContext` references Fable.Yjs types (`Y.Doc`, `Y.Map`, `Y.Array`).
`Adaptive.Codec.fs` is in the Ylmish project, which already references Fable.Yjs,
so it *can* `open Yjs`. Decide:

- **(A, recommended)** Put `Connect`/`BindContext` in the **codec** layer; the
  codec may reference Fable.Yjs (it already exists only to map to Yjs — the
  anti-corruption layer is about app-vs-state *schema*, not about avoiding the
  Yjs dependency).
- **(B)** Keep codec `CustomElement` minimal (`Kind`); define the richer binding
  interface in `Y.fs` and resolve it by cast/registry at dispatch. More
  decoupled, more indirection.

- **Exit check:** a skeleton type compiles under the chosen option; suite green.
  Record the choice under *Decisions*.

### Step 1 — Define `Connect` / `BindContext` (compile-green)

Add the types from *Design* under the Step 0 choice. `CustomElement` gains
`Connect`. No dispatch yet; `connect`/bridges still `failwith` on `Custom`.

- **Exit check:** compiles; suite green. The exhaustiveness checker forces every
  `Element` match to keep acknowledging `Custom`.

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
- **Layering coupling (Step 0).** Option A couples the codec to Fable.Yjs. Judged
  acceptable; revisit only if a non-Yjs backend (Ycs/Yrs) is pursued.

## Out of scope

- Naming/layout of roots — that is plan 0004 (`Scheme`).
- Non-Yjs backends.
- Collapsing the built-in `Text`/`AList`/`AMap` cases into `Custom` (kept concrete).
