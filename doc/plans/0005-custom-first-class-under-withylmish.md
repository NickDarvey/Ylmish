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
dispatches `Set`. Design is **decided** — see *Decisions*. Next step: **Step 0**.

### Progress

- [ ] **Step 0** — Add `abstract Value : aval<obj>` to `CustomElement`. Implement
  it on the built-in `Text.binding` (its string, boxed) and on the
  example/test counter (its merged count, boxed, owned internally by the
  binding). Compile-green; existing connect tests still pass (`Decode.custom`
  unchanged for now).
- [ ] **Step 1** — Rework `Decode.custom` to read `binding.Value` *from the
  element* (drop the external value-cell parameter), unboxing under the
  consumer's type — symmetric with `Decode.text`. Update the counter
  test/example to the no-cell surface. Connect tests green.
- [ ] **Step 2** — Wire `withYlmish` readback to custom values: extend the
  readback collector (`gather` + the `"ylmish-text"` sub, likely renamed) to
  also observe each `Element.Custom`'s `Value` aval and re-decode/`Set` on
  change, reusing `isWritingToYDoc` + `readbackModel`.
- [ ] **Step 3** — Headline e2e test: two `withYlmish` peers with a counter
  field; concurrent increments; **both Elmish models** converge on the sum (the
  custom analogue of the existing text e2e). Text e2e stays green.
- [ ] **Step 4** — Promote the counter into the `withYlmish` `TodoModel` (a
  `Hits` field + `Bump`/increment msg) so the demo shows *model-level*
  convergence; update README to state customs are first-class under `withYlmish`.

### Decisions & lessons

- **Expose the value (`Value : aval<obj>`), not just a change signal (decided).**
  Two shapes were considered for letting readback notice a custom change:
  1. `abstract Subscribe : (unit -> unit) -> IDisposable` — a bare change signal.
     Fixes the readback gap but leaves `Decode.custom` reading an externally
     threaded value cell (the 0003 wart).
  2. `abstract Value : aval<obj>` — the binding exposes its merged value as an
     adaptive cell. **Chosen.** It fixes the readback gap *and* lets
     `Decode.custom` read the value straight from the element (like `Decode.text`
     reads the `clist` inside `Element.Text`), deleting the value-cell threading.
     One member, two problems solved; custom becomes symmetric with text.
  - Cost: the value is **boxed** (`aval<obj>`). `CustomElement` is non-generic
    (`Element.Custom of CustomElement`; `Element<'Value>`'s `'Value` is the
    *LWW value* type, not the custom's decoded type), so typing the value would
    force `Element` to carry an extra type parameter — rejected as too invasive.
    `Decode.custom` unboxes under the consumer-supplied generic, exactly where the
    consumer already states the type.
- **No module-global cell needed for `withYlmish` integration (consequence).**
  Because `mergeReadback` keeps the live `Element.Custom binding` and
  `Decode.custom` reads `binding.Value` off it, a consumer's `encode`/`decode`
  can stay separate top-level functions (encode builds the binding from the model
  field; decode reads its value) with no shared mutable cell — the awkward part
  of 0003 Step 4 disappears.
- **Readback unification is a follow-on, not a goal here.** Once both text and
  custom expose a `Value`, the two readback collectors could merge into one
  (observe every connect-managed leaf's `Value`). Tempting, but it changes the
  *text* readback trigger (clist→value aval); do it only if Step 2 lands it
  green without churn, otherwise leave text's path intact and just *add* custom.

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

### `CustomElement` exposes its merged value

```fsharp
type CustomElement =
    abstract Kind    : Kind
    abstract Connect : BindContext -> System.IDisposable
    /// The element's merged value as an adaptive cell, boxed. `Connect` keeps it
    /// updated from the Yjs side; `Decode.custom` reads it; `withYlmish` readback
    /// observes it. Mirrors how `Element.Text`'s `clist` is both read and observed.
    abstract Value   : aval<obj>
```

A binding owns a `cval<obj>` initialised to its empty/default value and updates
it (inside its reentrancy-guarded decode direction) as the Y type merges. The
built-in `Text.binding` implements `Value` as its string boxed
(`chars |> AList.toAVal |> AVal.map (System.String.Concat >> box)`), for contract
completeness even though text readback can keep using the `clist`.

### `Decode.custom` reads the element (no external cell)

```fsharp
// before (0003): let custom (value : aval<'a>) : Decoder<_,_,'a>
let custom : Decoder<_,_,'a> = fun _ (path, el) ->
    match el with
    | Element.Custom b -> b.Value |> AVal.map (fun v -> Validation.ok (unbox<'a> v))
    | el -> error (UnexpectedKind { Expected = [ Kind.Custom ]; ... })
```

Now identical in shape to `Decode.text`: it reads the live value off the
element, recomputing as the binding updates it.

### `withYlmish` readback observes custom values

Extend the readback collector so `gather` also yields each `Element.Custom`'s
`Value` aval, and the subscription observes it (via `AddCallback`, with the same
first-echo skip and `isWritingToYDoc` guard the text path uses), calling the same
`readbackModel () |> Option.iter (dispatch << Set)`. `mergeReadback` already
takes the live side for `Custom`, so `readbackModel` decodes the merged value
correctly once it re-runs.

## Work breakdown — incremental, verify after every step

### Step 0 — Add `Value` to `CustomElement`; implement on text + counter

Add the member; implement on `Text.binding` and the counter binding (counter owns
its `cval<obj>` internally, exposing it; `Connect` writes it where it currently
writes the external `merged` cell). Leave `Decode.custom` on its external-cell
signature for this step so nothing else moves yet.

- **Exit check:** compiles; 124 tests green (exhaustiveness/`Value` wired, no
  behaviour change).

### Step 1 — `Decode.custom` reads `binding.Value`

Drop the `value` parameter; read/unbox from the element. Update the counter
binding to own its value and the test/example decoders to the no-cell surface.

- **Test:** the existing "consumer counter sums concurrent increments" test,
  rewritten to the no-cell surface, stays green.
- **Exit check:** no external value cell remains; connect tests green.

### Step 2 — Readback observes custom values under `withYlmish`

Extend `gather`/the readback sub to collect and observe custom `Value` avals.
Keep text's path working (add custom alongside, or unify per *Decisions*).

- **Test:** a `withYlmish` program with a counter field; a *remote-style* Y edit
  to the counter root dispatches `Set` and the model reflects it (the custom
  analogue of "withYlmish reflects a remote text edit in the model").
- **Exit check:** inbound custom edits update the model; suite green.

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

- **Boxing.** `Value : aval<obj>` boxes the merged value; `Decode.custom`
  unboxes under the consumer's type. A wrong consumer type is a runtime cast
  error — same failure class as any decoder type mismatch.
- **Contract addition is source-breaking for binding authors.** Adding `Value`
  means every `CustomElement` implementation must provide it; the only
  implementors today are in-repo (text + counter), so the blast radius is ours.
- **Reentrancy.** The binding must update its `Value` cell inside the shared
  `Active` guard, exactly as it does its Yjs writes, so readback↔encode don't
  loop. Same discipline as text.
- **Readback churn (Step 2).** If unifying text+custom onto `Value`, re-verify
  every text test — the text readback trigger changes from clist to value aval.

## Out of scope

- New custom *strategies* (sets, registers) — this plan only makes the existing
  seam first-class under `withYlmish`; bindings remain consumer-defined (0003).
- Nested (non-root) placement of customs — still `Parent = Root` per 0003's
  A1/A3 discipline.
- The layout `Scheme` work — that is plan 0004.
- Non-Yjs backends.
