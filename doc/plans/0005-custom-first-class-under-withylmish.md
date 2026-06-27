# 0005 — Custom fields first-class under `withYlmish` (readback + value exposure)

Close the one gap left by [0003](./0003-custom-element-merge-strategies.md):
`Encode.custom` / `Decode.custom` work at the **connect layer** but a *remote*
edit to a custom field doesn't yet update the Elmish model under
`Program.withYlmish`. Give `CustomElement` an observable merged **value** and
teach `withYlmish`'s readback to watch it — making custom fields as first-class
as collaborative text, and removing the value-cell threading wart along the way.

Parent: plan 0003 (the `CustomElement` seam). No separate issue yet.

## State

**Last updated:** 2026-06-27 · **Status: NOT STARTED.** Plan 0003 shipped the
`CustomElement` seam (`Connect`), `Encode.custom` / `Decode.custom`, a unified
`connect` dispatch, and a counter example — all proven at the **connect** level
(124 tests green; `npm run demo` prints the summed counter). The gap:
`withYlmish`'s `subs` readback observes the structural root map (`"ylmish-ydoc"`)
and **text** `clist`s (`"ylmish-text"` via `gather`), but `gather` skips
`Element.Custom`, so an inbound custom edit merges into its Y root yet never
dispatches `Set`. Design is **decided** — an Adaptive-free contract
(`Value : obj` + `Subscribe`) and a **single unified readback path** for text +
custom; see *Decisions*. Next step: **Step 0**.

### Progress

- [ ] **Step 0** — Add `abstract Value : obj` and
  `abstract Subscribe : (unit -> unit) -> System.IDisposable` to `CustomElement`
  (no `aval` in the contract). Implement both on the built-in `Text.binding` (its
  string + a `clist` subscription) and on the example/test counter (its merged
  count + change notifications, owned internally by the binding). Compile-green;
  existing connect tests still pass (`Decode.custom` unchanged for now).
- [ ] **Step 1** — Rework `Decode.custom` to read `binding.Value` *from the
  element* as a snapshot (drop the external value-cell parameter), unboxing under
  the consumer's type. Update the counter test/example to the no-cell surface.
  Connect tests green.
- [ ] **Step 2** — **Unify the readback collector onto `Subscribe`.** Replace the
  text-specific `clist`-observing `"ylmish-text"` sub with a single collector that
  walks the encoded tree and `Subscribe`s to *every* connect-managed leaf — text
  and custom alike — through the one `CustomElement` contract, re-decoding/`Set`
  on change, reusing `isWritingToYDoc` + `readbackModel`. Text rides the same
  path (its `Subscribe` observes its `clist`); all text tests stay green.
- [ ] **Step 3** — Headline e2e test: two `withYlmish` peers with a counter
  field; concurrent increments; **both Elmish models** converge on the sum (the
  custom analogue of the existing text e2e). Text e2e stays green.
- [ ] **Step 4** — Promote the counter into the `withYlmish` `TodoModel` (a
  `Hits` field + `Bump`/increment msg) so the demo shows *model-level*
  convergence; update README to state customs are first-class under `withYlmish`.

### Decisions & lessons

- **Keep Adaptive out of the contract: `Value : obj` + `Subscribe`, not
  `aval<obj>` (decided).** The `CustomElement` contract is what *consumers
  implement*, so it should stay as plain as the rest of Ylmish's promise ("your
  model field stays plain immutable F#"). Two shapes were weighed:
  1. `abstract Value : aval<obj>` — exposes the merged value as an FSharp.Data.
     Adaptive cell. Lets `Decode.custom` be a *live* aval (symmetric with
     `Decode.text` reading the `clist`), but **leaks `aval` into every consumer
     binding**.
  2. `abstract Value : obj` (current snapshot) **+**
     `abstract Subscribe : (unit -> unit) -> IDisposable` (notify on change).
     **Chosen.** Adaptive-free contract: a binding author writes plain .NET. It
     *still* removes the 0003 threaded-cell wart (decode reads `b.Value` off the
     element) and `Subscribe` is exactly what push-based readback wants.
  - **The one cost:** `Decode.custom` reads a **snapshot**, not a live aval. That
    liveness is *unused under `withYlmish`* — readback is push-driven (`Subscribe`
    fires → `readbackModel` re-runs → re-reads `b.Value` → `Set`). It would only
    matter to code that forces a `Decode.custom` aval *outside* `withYlmish` and
    holds it expecting updates; no test/example does. Documented as the single
    asymmetry vs `Decode.text`.
  - The value is still **boxed** (`obj`): `CustomElement` is non-generic
    (`Element.Custom of CustomElement`; `Element<'Value>`'s `'Value` is the *LWW
    value* type, not the custom's decoded type), so typing it would force a type
    parameter onto `Element` — too invasive. `Decode.custom` unboxes under the
    consumer-supplied generic, where the consumer already states the type.
- **Remaining Adaptive leak (acknowledged, out of scope).** Even with the
  Adaptive-free *contract*, a binding's `Connect` still uses Adaptive in its
  *implementation* on the **encode** side — it observes the model field
  (`local.AddCallback`) to push edits into Yjs. Fully hiding that needs a
  higher-level "binding builder" that takes plain push/merge functions; noted as
  a future direction, not undertaken here.
- **No module-global cell needed for `withYlmish` integration (consequence).**
  Because `mergeReadback` keeps the live `Element.Custom binding` and
  `Decode.custom` reads `binding.Value` off it, a consumer's `encode`/`decode`
  can stay separate top-level functions (encode builds the binding from the model
  field; decode reads its value) with no shared mutable cell — the awkward part
  of 0003 Step 4 disappears.
- **Unify the readback collector onto `Subscribe` (decided — Step 2).** Once both
  text and custom expose `Subscribe`, there should be **one** readback path, not a
  text-specific `clist` observer plus a custom one. The collector walks the
  encoded tree and subscribes to every connect-managed leaf through the single
  `CustomElement` contract; text rides it too (`Text.binding.Subscribe` observes
  the `clist`). This is the whole point of giving text a binding in 0003 Step 5 —
  one connect contract — extended to readback: one *readback* contract. The cost
  is that the text readback trigger moves from a direct `clist` observation to
  `Text.binding.Subscribe` (same underlying `clist`, so behaviourally identical) —
  Step 2 must re-verify every text test green. Mechanism is the implementer's
  choice (reconstruct each leaf's binding from the tree to read its `Subscribe`,
  or have `connect` hand its managed bindings back for reuse), but the *outcome*
  is fixed: a single Subscribe-based collector covering text + custom.

### Blockers

- None.

### Agent pickup prompt

> You are continuing plan 0005. Work **one step at a time**, in order, keeping the
> suite green. Each iteration: pick the first unchecked **Progress** item; read
> its *Work breakdown* entry and the decisions it depends on; implement the
> smallest change + its test(s); ensure the SDK is on `PATH`
> (`export PATH="$HOME/.dotnet:$PATH"`) and run `npm test` (and `npm run demo`
> for Step 4); update **State** (tick, bump *Last updated*, record any
> decision/lesson); commit one focused commit to the working branch (no PR unless
> asked); compact context; continue. Stop when every step is checked or a blocker
> needs a human decision.

## Problem

`Program.withYlmish` (`subs`) wires two readback triggers:

```fsharp
[ ["ylmish-ydoc"], fun dispatch -> rootMap.observeDeep (re-decode on structural change) ]
[ ["ylmish-text"], fun dispatch ->
    // gather walks the encoded tree collecting Element.Text leaves' clists,
    // observes each, and re-decodes on a remote text edit.
    let rec gather acc el =
        match el with
        | Element.Text chars -> chars :: acc
        | Element.AMap m -> ...recurse
        | Element.AList l -> ...recurse
        | _ -> acc          // <-- Element.Custom falls here and is dropped
    ... ]
```

A custom field lives in its **own** Y root (like text), so the structural
observer never sees it; and `gather` drops it. So an inbound remote edit to a
custom field updates the binding's internal value but **no `Set` is dispatched**
— the Elmish model is stale until something else triggers a readback. Customs
thus work at the connect layer but are not first-class under `withYlmish`.

The root cause is that a `CustomElement` is opaque (`Kind` + `Connect`): there is
nothing for readback to observe, and `Decode.custom` consequently has to read an
externally threaded value cell rather than the element itself.

## Goal

A consumer adds a custom field (e.g. a counter) to their model, wires
`withYlmish` exactly as they do for text, and a remote peer's edit shows up in
their Elmish model — no extra wiring, no global cell. The headline test: two
`withYlmish` peers, a counter field, concurrent increments, **both models**
converge on the sum.

## Design

### `CustomElement` exposes a plain value + a change subscription

```fsharp
type CustomElement =
    abstract Kind      : Kind
    abstract Connect   : BindContext -> System.IDisposable
    /// The element's current merged value, boxed and plain (no `aval`). `Connect`
    /// keeps it updated from the Yjs side; `Decode.custom` reads this snapshot.
    abstract Value     : obj
    /// Subscribe to "the merged value changed". `withYlmish` readback uses this to
    /// re-decode and `Set`. Returns a disposable that unsubscribes. The binding
    /// fires it (inside its reentrancy guard) whenever a Yjs merge moves `Value`.
    abstract Subscribe : (unit -> unit) -> System.IDisposable
```

A binding holds its merged value (a plain `mutable`, or a `cval` it doesn't have
to expose) and a subscriber list; its reentrancy-guarded decode direction updates
the value and notifies subscribers. The built-in `Text.binding` implements
`Value` as its current string and `Subscribe` by observing its `clist` — so text
can ride the same readback path as custom.

### `Decode.custom` reads the element snapshot (no external cell)

```fsharp
// before (0003): let custom (value : aval<'a>) : Decoder<_,_,'a>
let custom : Decoder<_,_,'a> = fun _ (path, el) ->
    match el with
    | Element.Custom b -> Decoded.ok (unbox<'a> b.Value)   // snapshot; re-read on readback
    | el -> error (UnexpectedKind { Expected = [ Kind.Custom ]; ... })
```

Same *shape* as `Decode.text` (reads the value off the element), but a snapshot
rather than a live aval — see *Decisions* for why that's fine under `withYlmish`.

### One unified readback collector (text + custom)

Today readback has a text-only collector: `gather` walks the tree for
`Element.Text` leaves and observes each `clist`. Step 2 replaces it with a single
collector that subscribes to **every** connect-managed leaf through the one
`CustomElement` contract:

```fsharp
// One walk → a Subscribe per connect-managed leaf (text OR custom).
let rec gather acc el =
    match el with
    | Element.Text chars -> (Text.binding chars).Subscribe :: acc  // text rides the same contract
    | Element.Custom b    -> b.Subscribe :: acc
    | Element.AMap m      -> // recurse
    | Element.AList l     -> // recurse
    | _                   -> acc

// Then, identically for both:
let subscriptions =
    gather [] tree
    |> List.map (fun subscribe ->
        subscribe (fun () -> if not isWritingToYDoc then readback ()))
```

where `readback` is the existing `readbackModel () |> Option.iter (dispatch << Set)`.
`mergeReadback` already takes the live side for `Custom`, so `readbackModel`
decodes the merged value correctly once it re-runs. (`Text.binding chars` here is
reconstructed purely to obtain its `Subscribe`, which observes the same shared
`clist`; the implementer may instead have `connect` hand its managed bindings
back so the same instances are reused — see *Decisions*.)

## Work breakdown — incremental, verify after every step

### Step 0 — Add `Value` + `Subscribe` to `CustomElement`; implement on text + counter

Add the two members; implement on `Text.binding` (current string + a `clist`
subscription) and the counter binding (it owns its merged value + a subscriber
list; `Connect` updates the value and notifies where it currently writes the
external `merged` cell). Leave `Decode.custom` on its external-cell signature for
this step so nothing else moves yet.

- **Exit check:** compiles; 124 tests green (members wired, no behaviour change).

### Step 1 — `Decode.custom` reads `binding.Value` (snapshot)

Drop the `value` parameter; read/unbox the snapshot from the element. Update the
counter binding to own its value and the test/example decoders to the no-cell
surface.

- **Test:** the existing "consumer counter sums concurrent increments" test,
  rewritten to the no-cell surface, stays green.
- **Exit check:** no external value cell remains; connect tests green.

### Step 2 — Unify the readback collector onto `Subscribe` (text + custom)

Replace the text-specific `clist` observer with **one** collector that walks the
encoded tree and `Subscribe`s to every connect-managed leaf — text via
`Text.binding.Subscribe`, custom via `b.Subscribe` — re-decoding/`Set` on change.
There is then one readback contract, mirroring the one *connect* contract from
0003 Step 5.

- **Test:** a `withYlmish` program with a counter field; a *remote-style* Y edit
  to the counter root dispatches `Set` and the model reflects it (the custom
  analogue of "withYlmish reflects a remote text edit in the model").
- **Exit check:** inbound custom edits update the model; **every existing text
  test stays green** (text now rides the unified path); suite green.

### Step 3 — End-to-end convergence test

Two `withYlmish` peers, each with a counter field; concurrent increments; sync;
**both Elmish models** read the summed value.

- **Exit check:** the custom e2e passes; the text e2e is unaffected.

### Step 4 — Promote the counter into the demo + docs

Add a `Hits : int` field and increment message to `TodoModel`; encode with
`Encode.custom`, decode with `Decode.custom`; a demo scenario increments on both
peers and shows the model-level sum. README: customs are first-class under
`withYlmish` (drop/curb any "connect-layer only" caveat).

- **Exit check:** `npm run demo` shows model-level counter convergence; docs
  match; suite green.

## Assumptions / risks

- **Snapshot decode (accepted).** `Decode.custom` reads `Value` once, not a live
  aval; `withYlmish` re-reads it on every `Subscribe` push, so the model stays
  correct. The only loss is for code holding a forced `Decode.custom` aval
  outside `withYlmish` — documented, and nothing relies on it. See *Decisions*.
- **Boxing.** `Value : obj`; `Decode.custom` unboxes under the consumer's type. A
  wrong consumer type is a runtime cast error — same failure class as any decoder
  type mismatch.
- **Contract addition is source-breaking for binding authors.** Adding `Value` +
  `Subscribe` means every `CustomElement` implementation must provide them; the
  only implementors today are in-repo (text + counter), so the blast radius is
  ours.
- **Reentrancy.** The binding must update `Value`/fire `Subscribe` inside the
  shared `Active` guard, exactly as it does its Yjs writes, so readback↔encode
  don't loop. Same discipline as text.
- **Readback churn (Step 2).** Unifying text+custom onto `Subscribe` moves the
  text readback trigger from a direct `clist` observation to
  `Text.binding.Subscribe` (same underlying `clist`). Behaviourally identical, but
  every text test must be re-verified green — this is a committed check, not a
  conditional one.

## Out of scope

- New custom *strategies* (sets, registers) — this plan only makes the existing
  seam first-class under `withYlmish`; bindings remain consumer-defined (0003).
- Nested (non-root) placement of customs — still `Parent = Root` per 0003's
  A1/A3 discipline.
- The layout `Scheme` work — that is plan 0004.
- Non-Yjs backends.
