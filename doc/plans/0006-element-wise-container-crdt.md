# 0006 — Element-wise container CRDT: research, options, and a correctness harness

The gap left open by [0004](./0004-scheme-document-layout.md): structural
containers (`Encode.list`/`map` of values) are **whole-container last-writer-wins**
— `materialize` re-projects a fresh `Y.Array`/`Y.Map` each update, so concurrent
structural edits drop one side. We want **element-wise** merge (concurrent
add/remove/reorder compose) *while consciously choosing* what happens to the
"plain immutable model" promise.

This is a **research plan**. Its deliverable is **(1) a reusable, trustworthy
correctness harness** and **(2) an evidence-backed architectural decision** — and
only then **(3) a thin vertical slice** of the chosen approach. It is *not* a
commitment to a particular design, and explicitly does **not** anchor on any one
option (including "cut Adaptive out of the middle").

Parent: plans 0002/0004. No separate issue yet.

## State

**Last updated:** 2026-06-28 · **Status: AWAITING HUMAN DECISION (Step 4).**
Steps 0–3 complete and committed (146 tests passing). Evidence points to **Option
E** (keyed reconcile vs live Yjs, on a keyed-map + id-named nested state +
fractional order layout) as the engineering front-runner — see the scored rubric
in *Decisions*. **Stopped at the Step 4 product question** (relax plain-immutable
purity for collections? — see Blockers) before fixing the option and starting the
Step 5 thin slice.

### Progress

- [x] **Step 0** — Built the **validation harness + oracle** (option-independent)
  in `tests/Ylmish.Tests/Harness.fs`. A `Bridge` takes whole immutable models in
  and reads a converged model out (the `withYlmish` contract); the `run` driver
  replays a per-replica schedule with a delivery policy and models `withYlmish`'s
  remote-update→read-back→`Set` loop; `differential` compares any SUT against the
  raw-Yjs oracle (M1). Acceptance met (5 tests): oracle keeps both concurrent adds
  (ground truth no-loss); the hybrid **disagrees** with the oracle and the harness
  pinpoints the lost id (red on the known bug); the hybrid *matches* sequentially
  (harness discriminates — not blindly hostile); hybrid converges yet violates
  no-loss (P1≠P2); minimality meter shows raw is `O(Δ)` vs hybrid `O(|state|)`.
- [x] **Step 1** — Characterized Adaptive's reassignment deltas
  (`tests/Ylmish.Tests/Adaptive.Spike.fs`, 6 tests, observed counts pinned). The
  decisive answer is **"it depends on the app, and even then it's positional, not
  keyed"** — see *Decisions* below. This both **conditionally kills Option A** and
  **prices Option B** (Adaptive only gives a positional diff we'd have to replace
  anyway).
- [x] **Step 2** — **Per-option falsifiers.** Built the positive experiment — a
  **keyed element-wise bridge** (`tests/Ylmish.Tests/Harness.Options.fs`, 5 tests)
  reconciling a stable `Y.Array` of ids by item id — and validated it on the
  ordered representative datatype (add/remove/**move**): sequential order matches
  the model fold, concurrent adds preserve both (matches the oracle), add/remove
  are O(Δ), a sequential move reorders losslessly, and concurrent same-item moves
  converge (P1) but may duplicate (the no-native-move limit, pinned). Per-option
  evidence/verdicts in *Decisions*.
- [x] **Step 3** — **Identity decided + move-preserves-nested-state proven**
  (`tests/Ylmish.Tests/Harness.Identity.fs`, 2 tests). Identity = an explicit
  stable per-item id the model carries; nested collaborative state is named by it
  (reuse 0004 `Scheme.byKey`/`KeyById`). Proof: id-keyed nested text survives a
  concurrent reorder+edit and merges onto the right item; the *same* schedule with
  position-keyed nested text corrupts (the edit sticks to the slot, mislabelling
  the moved row). See *Decisions*.
- [~] **Step 4** — **Scored** against the rubric (table in *Decisions*);
  front-runner = **Option E**. **STOPPED at the human product question** (immutable
  purity — Blockers). The chosen option is fixed only once the human answers.
- [ ] **Step 5** — **Thin vertical slice** of the winner, end-to-end under
  `withYlmish` for one datatype, passing the full harness (incl. minimality +
  liveness). Proves it composes with `materialize`/`connect` and the 0005 readback.
- [ ] **Step 6** — **Adversarial/stress + write-up**: large random schedules,
  partitions, nesting; document the guarantees *and* the limits; update README.

### Decisions & lessons

- **Step 0 — the harness is trustworthy.** It is green on the raw-Yjs oracle and
  red on the known hybrid bug, *and* it discriminates (passes the hybrid when
  edits are sequential). Differential testing against raw Yjs means there is no
  self-authored spec to be wrong about. This is the foundation every option is
  now judged on.

- **Step 1 — what adaptify actually emits on whole-collection reassignment
  (measured, pinned):**
  - adaptify turns an `IndexList<'T>` field into a plain `clist`, and `Update`
    sets the **whole new `IndexList`** (`_PropD_.Value <- value.PropD`). The delta
    is whatever `ChangeableIndexList`/`IndexList.computeDelta` produces, and that
    aligns the two lists **positionally by `Index`**, not by value/item identity.
  - **Identity-preserving regime** — when the next model is derived by *structural
    ops on the previous `IndexList`* (`.Add`/`.Prepend`/`.RemoveAt`): deltas are
    **minimal and O(1)** (append = 1 Set, prepend = 1 Set, remove = 1 Remove).
    Adaptive genuinely earns its keep here.
  - **Rebuild regime** — when the next model is a *fresh* list
    (`IndexList.ofList […]`, i.e. the idiomatic `{ model with Items = recompute () }`):
    a logically-O(1) prepend becomes **4 Sets / 0 Removes** (positional value-
    rewrite of all 3 survivors + 1 append). It does **not** recognise that a/b/c
    survived — it rebinds positions. O(n), not identity-stable.
  - **Reorder** — has **no** identity-preserving form in `IndexList`; reversing
    `[a;b;c]→[c;b;a]` is 2 Sets / 0 Removes. A move is never a move — always
    positional churn.
  - **Nested state** — `ChangeableModelList` reuses the inner `AdaptiveSubmodel`
    objects **positionally**: a rebuilt prepend *hijacks* position 0's adaptive
    object (it was item "a", now holds "x"). So a `Y.Text`/custom hung off an item
    by position is orphaned/rebound on any insert or reorder.
  - **Consequences for the options:**
    - **Option A is conditionally dead.** Feeding Adaptive's delta to Yjs gives
      correct element-wise ops *only if* the app preserves `Index` identity by
      mutating the prior `IndexList` — fragile app discipline — and **never** for
      reorder/move, and the identity it tracks is *positional Index*, not a stable
      item key, so moves orphan nested state regardless. It cannot deliver
      identity-stable moves at all.
    - **Option B is fairly priced.** Adaptive is only buying a *positional*
      `computeDelta` — not the *keyed* diff correct merge needs. We'd have to
      replace it with a keyed diff anyway, so cutting Adaptive from the sync path
      throws away little. (B still needs stable item identity — Step 3.)
    - **Every diff-based option (A/B/E) needs an explicit stable item id**;
      Adaptive's `Index` is not it. Identity is now the pivotal cross-cutting
      question (Step 3), not an Adaptive detail.

- **Step 2 — per-option evidence (run against the Step 0 harness):**
  - **The keyed element-wise mechanism works.** A stable `Y.Array` of ids,
    reconciled by item id against each new model, passes the harness on the
    ordered datatype: convergent, no-loss on concurrent adds (matches the oracle),
    O(Δ) on add/remove, lossless sequential moves. This is the *application layer*
    every viable option (B and E) produces, so the family is validated.
  - **The wall is moves, and it is a *representation* problem, not an op-source
    one.** `Y.Array` has no native move, so a move is delete+insert: it duplicates
    under concurrent moves and would orphan any per-item nested state (Step 3).
    This wall is hit identically by A/B/E because they all target a *value array*.
    The escape is a different **layout** — a keyed **map of items + a fractional
    order key** (0004): then a move is a key *update* (nested-state-safe, merges),
    not delete+insert. Elevate this for Step 5; it matters more than A-vs-B-vs-E.
  - **Option E (reconcile vs live Yjs)** = the validated `keyed` bridge itself:
    stateless, robust to remote updates landing between local ops (it always
    diffs against live Yjs). Strongest "just works", least state. Needs a stable
    id (Step 3); shares the move limit.
  - **Option B (diff vs previous immutable model)** = the same application layer
    plus a *previous model* to recover intent. Two findings: (1) it must still
    apply against *live* Yjs indices, so a `prev` that has diverged from Yjs (a
    remote update arrived) mis-diffs — B only stays correct if the read-back loop
    feeds it the merged model, the *same* coupling the hybrid has; (2) the one
    thing `prev` buys over E is **move-intent**, which a value array can't honour
    natively anyway — so B's edge over E only pays off under the keyed-map +
    fractional-order layout above. Verdict: viable, but no free lunch over E.
  - **Option C (capture intent from `Msg`)** — falsifier run by inspection of the
    example message sets (`Example.Message`, TodoCollaborative `Msg`): Add/Remove/
    Set map cleanly, but there is **no reorder/move message** — the apps don't
    model it, so move-intent can't be captured without inventing synthetic
    messages. Deeper: C couples `Msg` to sync (a *second* sync surface beside the
    codec) and only covers state changed *through* messages. Verdict: viable only
    for apps whose every collaborative mutation is a message *and* who add explicit
    move messages; inverts Ylmish's "the codec is the only sync concern." Also a
    product question.
  - **Option D (handle in the model, `Ref<>`)** — falsifier is ergonomic/values,
    not empirical: a live Y-backed handle gives exact native ops with zero diffing
    (mechanically the simplest, and it sidesteps the move/identity problems by
    using Yjs types directly), but it **breaks referential transparency** for
    collection fields — `update` is no longer a pure prev→next, and model equality
    / time-travel / test-replay no longer hold for those fields. Verdict:
    correctness-strong, but it abandons the plain-immutable-model promise that
    differentiates Ylmish. Pure **product** decision (see Blockers).

- **Step 3 — identity + nested-state-on-move (decided + proven):**
  - **Identity source:** an explicit, stable per-item id the model carries (a
    guid). Nested collaborative state (per-item text/custom) is named by that id,
    reusing 0004's `Scheme.byKey`/`PathSegment.KeyById`. Explicitly *not*
    positional `Index` (mutable, Step 1) and *not* the fractional order key (that
    is for ordering and changes on reorder — 0004's immutable-id-vs-mutable-order
    split). The model already needs this id for the codec; no new concept.
  - **Move preserves nested state — proven green:** because a nested root's *name*
    is the item id, a reorder never touches it. Test: an id-keyed nested text
    survives a concurrent reorder+edit and the edit merges onto the right item; the
    counter-example (position-keyed) corrupts under the identical schedule. This
    confirms the front-runner representation (keyed items + id-named nested state)
    clears the one wall Step 2 flagged.

- **Step 4 — scored rubric (evidence from Steps 0–3).** Gate = must pass M1–M4.

  | Option | Correctness (gate) | Minimality | Immutable model | Move / nested | Complexity / blast | Adaptive footprint | Verdict |
  |---|---|---|---|---|---|---|---|
  | **A** lean into Adaptive | **fails** (positional, no move; conditional on app discipline) | cond. | preserved | poor (orphans) | low | keeps | **eliminated** (Step 1) |
  | **B** cut Adaptive, diff vs prev model | pass* | O(Δ) | preserved | good w/ keyed-map | med–high (reimpl diff + prev/read-back coupling) | drops | viable, heavier than E for no gain |
  | **C** capture `Msg` intent | partial (no move msg; msg-only) | O(Δ) | preserved | needs synthetic msgs | med (2nd sync surface) | n/a | niche + product |
  | **D** handle in model (`Ref<>`) | pass (native Yjs) | O(Δ) | **breaks** | native | low | drops | simplest+correct, **abandons immutability** (product) |
  | **E** reconcile vs live Yjs (keyed) | **pass** (the validated bridge) | O(Δ) | preserved | good w/ keyed-map | med (stateless; composes w/ materialize/connect) | drops from sync path | **front-runner** |

  \*B stays correct only if the read-back feeds it the merged model (same coupling
  the hybrid has); its one edge over E (move-intent) only pays off under the
  keyed-map layout, which E gets anyway.

  **Recommendation (engineering):** **Option E** — a keyed reconcile against live
  Yjs — on a **keyed-map of items + id-named nested state + fractional order key**
  representation (0004). It is the only option that is *simultaneously* correct
  (M1–M4 ✓), minimal (O(Δ) ✓), nested-safe on moves (Step 3 ✓), and **preserves
  the plain-immutable model**, with the least moving parts (stateless; no prev
  plumbing; composes with today's `materialize`/`connect`). It also lets us drop
  Adaptive from the *sync path* (Step 1 showed it only contributes a positional
  diff there) without removing it elsewhere.

  **The decision is gated on the human product question below** (immutable purity):
  E is recommended *because* it preserves immutability — but if that constraint is
  not actually required, D (a Y-backed handle) is materially simpler. So we stop
  here for that call before fixing the option and starting Step 5.

### Blockers / human decisions needed

- **Product question (decide after Step 2 evidence):** is it acceptable to relax
  the "plain immutable F# model" promise for *collection* fields (Options C/D,
  which hold a handle / capture intent), or is preserving it a hard constraint
  (forcing a diff-based option A/B/E even if costlier)? This is a values call, not
  an empirical one — the agent must **stop and ask** once the spikes have priced it.

### Agent pickup prompt

> You are running research plan 0006. The goal is a *correct* decision, not a
> shipped feature. Work the steps in order. **Build the harness first (Step 0) and
> prove it catches today's bug** before evaluating anything. For each option,
> prefer the **cheapest experiment that could falsify it** over building it out
> (Steps 1–2). Treat raw-Yjs as ground truth (differential oracle). Record every
> spike's *observed* result, not what you expected. Do not anchor on any option,
> including "cut Adaptive". Stop at Step 4 for the human product decision, and
> whenever evidence contradicts an assumption. Keep `npm test` green; the harness
> lives in the test project.

## Problem — the one hard question

Every approach reduces to: **how do you derive minimal, correct, identity-stable
operations to feed a CRDT from a stream of whole, immutable Elmish models?**

`materialize` answers "don't — just overwrite," which is why it's LWW. Text
escapes via `connect` (a stable `Y.Text` fed deltas recovered by affix-diff).
Containers are harder because diffing two immutable collections into
insert/remove/**move** ops needs **stable per-item identity** ("did this item
move, or was it deleted and a new one added?"), and a **move** must not be a
delete+reinsert (that orphans an item's nested CRDT state — interacts with 0004).

There are only four places the operations can come from:

1. **Adaptive deltas** — let FSharp.Data.Adaptive compute them (Option A).
2. **Explicit structural diff** — compute them yourself, vs the previous model
   (Option B) or vs live Yjs (Option E).
3. **Message intent** — capture them from Elmish `Msg` (Option C).
4. **Direct mutation** — the model holds a Y handle; no diff (Option D).

## What "correct" means — properties (the spec the harness enforces)

Precise, testable properties. The harness asserts these; an option "passes" only
if it satisfies P1–P3, P5–P6 (P4 is a perf gate).

- **P1 Convergence (strong eventual consistency).** For *every* schedule of
  concurrent ops + deliveries, once all updates reach all replicas, every
  replica's **decoded model** is identical.
- **P2 No-loss / intention preservation.** Every locally-applied user op is
  observable in the converged state under a stated per-datatype semantics: an
  added item (unique id) is present unless concurrently deleted; an edit to a
  surviving item is reflected; a move changes order without losing the item or its
  nested state. (The exact semantics = raw-Yjs behaviour; see M1.)
- **P3 Faithfulness at rest.** With no concurrency, `decode (encode model) =
  model`, and after a replica applies its own update the decoded Y state equals
  its model. (Encode/decode are inverse at quiescence.)
- **P4 Minimality (perf gate, *not* correctness).** Update size and op count are
  `O(Δ)`, not `O(|state|)`. Whole-container LWW already "converges"; what makes
  element-wise *worth it* is that it's incremental. Measured, gated.
- **P5 Reentrancy / echo-freedom.** A local change emits exactly the intended
  remote ops and does not re-trigger itself; applying a remote update emits none.
- **P6 Liveness under `withYlmish`.** A remote op actually produces a model `Set`
  (the readback fires) — the 0005 concern, generalized to containers.

## How we reach "correct" — the validation harness (the rigor)

The harness is the heart of this plan and is **option-independent**: it is built
once (Step 0) and every option is judged by it. Methods, strongest first:

- **M1 — Differential testing vs raw Yjs (ground-truth oracle).** For a scenario,
  hand-write the *equivalent* program directly on `Y.Array`/`Y.Map`/`Y.Text` and
  take its converged state as ground truth. Assert the Ylmish bridge produces the
  **same** converged state. Raw Yjs *defines* the intended CRDT semantics, so this
  doubles as the P2 intention oracle — no need to invent a separate spec.
- **M2 — Property-based concurrent schedules (Hedgehog, already in repo).**
  Generate `ops × replicas × interleaving`; **shrink** failures to a minimal
  counterexample. Include **partitions** (defer delivery), **duplication**
  (deliver an update twice), and **reordering** (Yjs updates are
  idempotent/commutative, so any delivery order must still converge — a cheap,
  strong P1 probe).
- **M3 — Exhaustive small-model checking.** For small `K` ops over `N = 2..3`
  replicas, enumerate **all** interleavings and assert P1/P2. Catches the corner
  cases random sampling misses; tractable at small `K`.
- **M4 — Metamorphic relations.** `mergeUpdates` in any order ≡ same state;
  add/delete commute under the set semantics; applying ops in different orders
  converges. No oracle needed — relations must simply hold.
- **M5 — Minimality instrumentation.** Measure `|encodeStateAsUpdate|` deltas /
  op counts per change; assert `O(Δ)`. The P4 gate.
- **M6 — Faithfulness unit + property.** P3 as both example tests and a generated
  round-trip property.

**Harness acceptance test (Step 0 exit):** the harness must (a) report **green**
when driven by raw Yjs, and (b) report **red** when driven by today's
`materialize` hybrid — concretely reproducing the "concurrent add is lost"
counterexample and shrinking it to the two-op minimal case. Only a harness that
*fails on the known bug* is trustworthy enough to certify a fix.

## Research options (even-handed; each with its cheap falsifier)

For each: the hypothesis, sketch, and the **single cheapest experiment that could
kill it** (run in Step 2). None is favoured; the harness + rubric pick the winner.

### Option A — Lean into Adaptive: `connect` every container

**Hypothesis:** the existing `Array.attach`/`Map.attach` machinery already turns
adaptive deltas (`IndexListDelta`/`HashMapDelta`) into element-wise Yjs ops on
stable `Y.Array`/`Y.Map`; wire it into `withYlmish` for *all* containers and drop
`materialize` from steady state. The immutable model is untouched; adaptify's
`AdaptiveModel` supplies the incremental diff.

**Falsifier (Step 1, decisive):** when adaptify's `Update` assigns a *whole fresh*
`IndexList`/`HashMap`, does FSharp.Data.Adaptive emit a **minimal, identity-stable**
delta — or a clear-all + add-all? If the latter, Option A silently degrades to
`materialize` and is dead. **This one spike also tells us what Adaptive is worth**,
so it informs Option B too. *(Cheap, do it first.)*

### Option B — Cut Adaptive: bridge Elmish → Yjs directly *(user-proposed; not anchored)*

**Hypothesis:** drop FSharp.Data.Adaptive/adaptify from the sync path; on each
update, run an explicit **keyed structural diff** of the previous immutable model
vs the new one and emit Yjs ops to stable Y types. The diff is explicit code we
fully control and test.

**Appeal:** removes a heavy dependency and semantics we don't fully control; the
op-derivation becomes legible and directly testable. **Cost:** we reimplement a
keyed list/map diff (which Adaptive arguably already does — Option A's spike tells
us if it does it *well*); still needs the same item identity (Step 3); loses any
adaptive *views* the app relies on elsewhere.

**Falsifier (Step 2):** prototype the keyed diff for one datatype (ordered list of
entities with a text field) and run M1–M4. Keyed list diff with stable ids is
known-feasible (Myers/keyed reconcilers), so the risk is less "does it work" and
more "is it less code/complexity than A for equal correctness" — score it, don't
just pass/fail. Explicitly compare LOC + dependency footprint vs A.

### Option C — Capture intent from `Msg`

**Hypothesis:** the Elmish `Msg` already names the operation (`AddItem`,
`Move`…). Map messages → Yjs ops directly; no diff, no identity inference, exact
minimal ops.

**Cost:** couples `Msg` to sync (a second sync surface besides the codec); only
covers state changed *through* messages; inverts Ylmish's "codec is the only sync
concern."

**Falsifier (Step 2):** take a real example's message set and check every
collaborative mutation maps to a clean CRDT op *without contorting the app*; if it
needs synthetic messages or leaks ordering, that's the kill. Also a **product**
question (below).

### Option D — Handle in the model (the reserved `Ref<>`)

**Hypothesis:** collection fields hold a live Y-backed handle; mutations go
straight to Yjs. Zero diffing, exact ops, native Yjs.

**Cost:** breaks the plain-immutable promise for those fields — the very thing
that differentiates Ylmish.

**Falsifier (Step 2):** an ergonomics spike — does a `Ref<>`-style field stay
usable in `update`/`view` without poisoning the rest of the MVU model? Plus the
**product** decision (below).

### Option E — `materialize` as a keyed reconciler (diff vs live Yjs)

**Hypothesis:** keep immutable-model → `materialize`, but change `materialize`
from "replace the container" to "**reconcile**": diff the new Element tree against
the *current Y state*, keyed by id, and apply minimal ops in place (React-style).
No new layer.

**Cost:** reconciling against live Yjs each update needs to read current Y state
and handle moves correctly; it's CRDT-op-derivation by tree-diff, with the same
identity needs.

**Falsifier (Step 2):** spike a keyed reconciler for one datatype; measure
correctness (M1–M4) and minimality (M5). The risk is move-handling and read-cost;
if moves degrade to delete+insert (orphaning nested state), that's the kill.

## Decision procedure (Step 4)

Run A–E through the **same** harness on the **same** representative datatype (an
ordered list of entities, each with a collaborative text field + membership +
reorder — it exercises identity, move, nesting, and the 0004 interaction at once).
Score each on a rubric:

| Criterion | Weight |
|---|---|
| Correctness — passes M1–M4 (P1/P2/P3/P5) | gate (must pass) |
| Minimality — `O(Δ)` (M5) | high |
| Preserves plain-immutable model | high (unless human relaxes it) |
| Move / nested-state correctness | high |
| Complexity / LOC / blast radius on existing code | medium |
| Dependency footprint (e.g. keeps vs drops Adaptive) | medium |
| Maintenance / legibility of op-derivation | medium |

Then **stop and present** the scored options + the product question to the human;
record the chosen option and the evidence in *Decisions* before any Step 5 code.

## Work breakdown — verify after every step

### Step 0 — Harness + oracle (option-independent)
Build M1–M6 in the test project: a raw-Yjs differential oracle; a Hedgehog
schedule generator with partitions/duplication/reordering; a small-`K` exhaustive
enumerator; a minimality meter. **Exit:** harness green on raw Yjs, **red on the
current hybrid** (reproduces + shrinks the lost-add counterexample).

### Step 1 — Characterize Adaptive's reassignment deltas (decisive)
F# spike: drive an `AdaptiveModel` via `Update` with successive whole `IndexList`s
/`HashMap`s; log the emitted `IndexListDelta`/`HashMapDelta`. **Exit:** a recorded,
unambiguous answer to "minimal+identity-stable, or clear+rebuild?" — settling
Option A's viability and pricing Adaptive (Option B input).

### Step 2 — Per-option falsifiers
Run each option's cheapest kill-experiment against the Step 0 harness on the
representative datatype. **Exit:** a pass/fail + rubric score per option, with
observed evidence (not predictions).

### Step 3 — Identity across versions
Decide the source of per-item identity used for *diffing* (reserved guid field vs
reuse 0004 `KeyById`); confirm a **move** preserves an item's nested CRDT state in
the chosen scheme (test it). **Exit:** identity decision recorded; move-preserves-
nested-state test green for at least the front-runner option(s).

### Step 4 — Score, decide, ask the human
Apply the rubric; **stop for the product decision** (immutable purity). **Exit:**
decision + evidence in *Decisions*; chosen option fixed.

### Step 5 — Thin vertical slice of the winner
Implement the chosen option for the one representative datatype, end-to-end under
`withYlmish`, passing the **full** harness incl. P4 minimality and P6 liveness;
compose cleanly with the text/custom `connect` path and the 0005 readback. **Exit:**
the representative datatype is genuinely element-wise mergeable under `withYlmish`;
suite + harness green.

### Step 6 — Adversarial/stress + write-up
Large random schedules, deep partitions, nested containers (list of maps of
lists), move+edit races. Document the guarantees **and** the residual limits;
update README's merge-semantics section. **Exit:** stress green; docs match.

## Assumptions / risks

- **Harness false confidence.** The single biggest risk is a harness that passes
  everything. Mitigated by the Step 0 acceptance test (must go *red* on today's
  known bug) and by differential testing against raw Yjs rather than a
  self-authored spec.
- **Adaptive surprise (Step 1).** If Adaptive collapses reassignment to
  clear+rebuild, Option A is out and the result reframes the whole plan — which is
  exactly why Step 1 is cheap and first.
- **Moves orphan nested state.** Yjs arrays historically lack a native move;
  delete+reinsert loses an item's nested `Y.Text`/custom. Any option must show a
  move preserves nested state (Step 3) or document the limit.
- **Identity assignment/migration.** Stable ids must exist on items; retrofitting
  ids to existing documents is a migration concern (out of scope, but flag it).
- **Scope creep.** This is research: timebox each option's spike; do **not** build
  more than the cheapest falsifier before Step 4. The slice (Step 5) is one
  datatype, not the whole surface.

## Out of scope

- Shipping element-wise merge for *every* datatype (Step 5 proves one; general
  rollout is a follow-on plan).
- Non-Yjs backends; server persistence/auth; presence/awareness.
- Replacing the text/custom `connect` path (it already works) — this plan is about
  *containers*, and must compose with it.
