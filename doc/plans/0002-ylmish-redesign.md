# 0002 — Redesigning Ylmish around real CRDT merging

Fixes [#83](https://github.com/NickDarvey/Ylmish/issues/83).

## Motivation

`Program.withYlmish` today synchronizes by **materializing the whole encoded state tree** into the Y.Doc on every Elmish update, and reading it back wholesale on every Y.Doc change. Consequences (all reproduced empirically, see [Validated assumptions](#validated-assumptions)):

- String fields are atomic map values, so concurrent edits to the same field resolve last-writer-wins instead of merging. The canonical collaborative use case — two people typing in the same text body — silently loses one side's work.
- Every update replaces nested `Y.Array`/`Y.Map`/`Y.Text` instances wholesale, which discards *any* concurrent remote edits inside them, not just text.
- `materialize` deletes root keys it doesn't recognize, actively destroying data written by other clients or other schema versions — the exact opposite of what schema migration needs.
- Re-materializing is O(state) per update rather than O(delta).

Meanwhile the delta-level machinery that yields true CRDT convergence (`Text.attach`, `Array.attach`, `Map.attach` in `src/Ylmish/Y.fs`) already exists and passes its unit tests — it is simply unreachable from the codec and `withYlmish`.

This plan replaces the materialize path with a **live binding**: the codec describes *which* shared type backs each field, and the runtime keeps the adaptive model and those shared types in sync by exchanging deltas, in both directions, forever.

## Design inputs

Decisions taken with Nick:

| Question | Decision |
| --- | --- |
| How does collaborative text appear in the consumer's model? | **A dedicated `Ylmish.Text` type** that carries edit intent, not a plain diffed `string`. (Reaffirmed after reviewing the executed branch, which shipped `aval<string>` + affix diff and proved it *converges* fine — the dedicated type is an interleaving-fidelity and editor-integration choice, and `Text.edit` keeps the zero-ceremony path.) |
| Which layers are public? | **Elmish-first, with named exceptions.** `Program.withYlmish` + the codec are the primary surface. **Yjs and Elmish are acceptable public dependencies**; Fable.Yjs types appear verbatim in the `CustomElement` escape hatch so a consumer can bind, say, a real `Y.Text` to an editor. **Adaptive trends toward encapsulation**: the Adaptify-generated `Create`/`Update` can't be hidden yet, but nothing else should require the consumer to touch `aval`/`alist`/`amap`, and new public contracts (notably `CustomElement`) must be Adaptive-free. |
| Who seeds a fresh document? | **Decode-empty = init.** The library never writes structure eagerly. An empty/partial doc decodes through the consumer's decoder (which has access to the current model); containers are created lazily by the first client that *edits* them, atomically. |
| The nested first-create race (U2a) | **Accepted as a documented limitation, solved by application design.** If two offline peers each *first-create* the same nested container and later sync, one subtree wins wholesale. The app-level rule: anything that can be *created* offline needs a consumer-supplied unique key (`Encode.map` keyed by it), so independent offline creations occupy distinct keys and never race. Editing an *existing* field offline is the normal CRDT case and merges. The library documents this rule; it does not contort the document layout to dissolve the race. |
| Packaging | **One `Ylmish` package** (plus `Fable.Yjs`); opinionated extras are separate namespaces you must explicitly `open`. |
| Per-field merge menu | **LWW registers, mergeable Text, mergeable lists (Y.Array), keyed element-wise maps** — plus **`Encode.atomic`** (deliberate wholesale-LWW subtree) and **`Encode.custom`** (the `CustomElement` escape hatch for consumer-defined merge strategies and direct Y-type access). Opinionated strategies built *on* the hatch (counters, timestamped LWW) stay out of core. The demo must exercise list and keyed-map merging, not just text. |
| List semantics | **`Encode.list` is for value sequences** (insert/delete merge, diff-reconciled). **Records with identity go in `Encode.map`** over `HashMap<key, _>` — this is now a hard rule, not a preference, because Adaptify's list deltas are positional (see L1) and would corrupt nested collaborative state under insert/reorder. Ordering of keyed items is the consumer's concern (fractional-index recipe in docs). |
| Migrations | **Defer entirely.** No Migrations module. The codec keeps migrations *expressible* (decoders compose, encoders combine, the runtime preserves unknown keys) and the docs show the dual-key recipe. (The executed branch independently built migration helpers and then cut them — treat that as confirmation.) |
| Demo form | **CLI.** A Node script whose output demonstrates the system under interesting concurrency: offline divergence, text interleaving, list merges, keyed-map merges, an honest LWW clobber, reconvergence. |

## Lessons imported from `claude/github-issues-visibility-8k12g3`

That branch executed its own plans 0002–0009 to close #83 by accretion (flatten collaborative leaves to scheme-named top-level roots, keep `materialize` for the structural rest). This plan takes the other architecture — bind nested types in place, delete `materialize` — but its evidence is real and is folded in here:

| # | Lesson (their evidence) | Consequence in this plan |
| --- | --- | --- |
| L1 | **Adaptify list deltas are positional, not keyed** (their 0006 Step 1, pinned in `Adaptive.Spike.fs`). The idiomatic rebuild `{ m with Items = recompute() }` yields O(n) positional value-rewrites, and `ChangeableModelList` **rebinds nested adaptive objects by position** — a nested `Y.Text` hung off item *i* gets hijacked by whatever lands at position *i*. | `Encode.list` is restricted to value sequences; items may not contain text/custom leaves (runtime error pointing at `Encode.map`). Step 1 re-pins this characterization in our suite. The plan's O(delta) claims are scoped: lists are diff-reconciled by value, maps are keyed by construction (`HashMap` → keyed `amap` reconcile, their 0008 Step 0a). |
| L2 | **Differential testing against a raw-Yjs oracle** (their 0006 harness): replay per-replica schedules under a delivery policy, compare the full bridge against hand-written Yjs, plus property-based random schedules. It caught the whole-container-LWW bug red-handed and *discriminated* (green on sequential). | Adopted as this plan's verification backbone (Steps 2 and 9). No self-authored spec to be wrong about. |
| L3 | **Common-affix diff produces minimal text deltas** (their A5: one char change = 2 ops, not clear+reinsert; Myers unnecessary). | Validates `Text.edit`'s implementation strategy and the "plain `<input>` stays cheap" claim. |
| L4 | **Read-back fragmentation was a consequence of their layout.** Flattening leaves to separate roots meant root-map `observeDeep` never saw remote text/custom edits → per-clist observers → merged read-back trees → `afterTransaction` hooks, with real crashes en route (re-entrant `Y.Text` edit during a remote apply; `"cannot mark object without transaction"`). | Bind-in-place keeps **one** `observeDeep` (nested events reach the root, U7) and origin tokens suppress echo (U6) — structurally avoiding that bug class. Their two crashes become named regression tests in Step 6. Their lesson that connect realizes the adaptive graph (all mutations must be inside `transact`) is folded into Step 5's contract. |
| L5 | **Root kind drift throws** (their A2): re-getting a root under a different type is an error, and schema drift needs a clear message. | The binding layer detects kind mismatch (encoded kind vs existing Y type) and surfaces a structured decode/bind error rather than a Yjs internal throw. Step 5 test. |
| L6 | **The `CustomElement` contract shape is proven** (their 0003/0005/0008): `Connect : BindContext -> IDisposable`, an Adaptive-free merged-value read (`Value`), a working consumer counter that *sums* concurrent increments, and "one attach contract" (built-in text rides the same seam). Their `Subscribe` member existed only because their layout put customs outside `observeDeep`'s reach. | Step 8 adopts Connect + Value (Adaptive-free, per the encapsulation decision) but **drops `Subscribe`** — under bind-in-place, custom read-back rides the same root `observeDeep`. `BindContext` exposes real Fable.Yjs types; that's now an explicit feature (bind a `Y.Text` to an editor). |
| L7 | **"The model's type is the merge choice"** (their 0008 taxonomy) and **naming-id ≠ ordering-id** (their 0004: immutable identity for structure, mutable fractional `order` field for display order, `fractional-indexing` as consumer recipe). | Adopted verbatim: the taxonomy table below and in the README; the fractional-index recipe in `recipes.md`. |
| L8 | **Their 0009 problem (nested records clobber per-record) dissolves under bind-in-place.** They needed dotted-key flattening + escaping because nested `Y.Map`s were re-created each update. | We get per-field merge of nested records for free — a nested object is a bound `Y.Map` whose keys are bound individually. `Encode.atomic` is still worth shipping as the *deliberate* wholesale-LWW opt-out (their insight that atomic replacement is sometimes what you want). Step 5 proves per-field nested merge; Step 4 adds `atomic`. |
| L9 | **Move/reorder duplication and delete-beats-edit are livable, pinned limits** (their harness pinned concurrent same-item moves may duplicate; ours pinned the same, U13/U9). | Documented limits; the keyed-map + order-field pattern is the recommended shape for reorderable collections precisely because a reorder becomes an order-field LWW write, not a structural move. |

What we deliberately do **not** import: the `Scheme` seam and root-flattening layout (its robustness benefit is now an accepted app-design concern — unique keys for offline-creatable entities — and its costs were a fragmented wire format and read path); the `materialize` hybrid; boolean reentrancy flags (origins are sounder, and are what `Y.UndoManager` will want); stringly primitives (`Decode.bool` via `string v = "true"`).

## Validated assumptions

Runnable scripts: [`0002-assumptions/`](./0002-assumptions/) (`node experiments.mjs`, yjs 13.6.x). Step 1 pins these as characterization tests in CI — both the Yjs set and the Adaptive set (L1).

| # | Unknown | Result | Design consequence |
| --- | --- | --- | --- |
| U1 | What does argless `doc.getMap()` return? | The root map named `''`; same instance every call. | Root binding is stable; no need to force consumers to name a root map. |
| U2a | Two clients each create a nested type under the same map key, then sync. | One client's **entire subtree wins**; the other's data is silently discarded. | Never create containers eagerly at init; create lazily on first local edit. Residual first-create race is an **accepted, documented limitation**: offline-creatable entities need consumer-supplied unique keys (`Encode.map`). |
| U2b | Same race with *root-level* types (`doc.getArray "todos"`). | Merges by name; both sides' items survive. | Root types are safe to get-or-create. |
| U3 | Concurrent edits to one shared nested `Y.Text`. | Interleave and converge. | The whole point. Text must bind to an existing instance, created once. |
| U3b | Concurrent `map.set` of a plain string. | LWW clobber — issue #83's failure mode. | Plain values are honest LWW registers; the codec must let consumers choose text vs register per field. |
| U4 | What decides the LWW winner? | Deterministic, order-independent: **higher clientID wins** among concurrent sets. Not wall-clock. | Document honestly: "last writer" is an arbitrary-but-convergent tiebreak. |
| U5 | Re-set an already-integrated Y type instance under another key. | **No exception at the call site; the doc corrupts** — later syncs throw and content is lost. | The public API must make re-parenting impossible by construction. The binding layer binds each model field to exactly one shared-type instance and never reuses instances. |
| U6 | Do transaction origins propagate? | Yes: `doc.transact(fn, origin)` origins and `applyUpdate`'s origin both reach observers, with `local` flag. | Echo suppression via a per-binding origin token, replacing boolean reentrancy flags. |
| U7 | Does `observeDeep` on the root see nested edits? | Yes, typed events with paths (`["list", 0]`) and per-type deltas. | One observer covers the whole bound tree — including custom elements (L4, L6). |
| U8 | Concurrent Y.Array inserts at the same position. | Both survive, deterministic order. | Lists merge; safe default for value sequences. |
| U9 | Delete an item vs concurrent edit inside it. | Delete wins; the edit is lost. | Documented limit. |
| U10 | What primitives can Y.Map hold? | Any JSON value (number, bool, null, string, plain objects/arrays). | The codec's `Element<string>` (strings only) is an unnecessary restriction; v2 elements carry typed primitives. |
| U11 | Replace a nested Y.Text with a fresh one while a peer edits the old one. | Replacement wins; the peer's edits are lost. | Precisely why materialize-per-update can never merge. Bind, never replace. |
| U13 | Concurrent "move" (delete+insert) of the same list item. | The item is **duplicated**. | Known Yjs v13 limit; the keyed-map + order-field pattern avoids structural moves entirely (L9). |
| U14 | One `doc.transact` spanning many nested types. | One `observeDeep` batch containing all events. | Remote changes apply as a single Elmish `Set` per transaction — atomic model updates. |
| U15 | A client applies updates containing keys it doesn't understand. | Applied cleanly; unknown keys preserved and readable. | Forward compatibility is free *if we stop deleting unknown keys*. Foundation for schema migrations (the dual-key recipe). |
| A1 | *(Adaptive, from L1 — to re-pin in Step 1)* What delta does Adaptify emit when a rebuilt `IndexList` replaces a field? | Positional: O(n) value-rewrites; nested adaptive objects rebound by position; reorder is never a move. | `Encode.list` = values only; identity lives in `HashMap` keys (`Encode.map`), whose Adaptify reconcile is keyed by construction. |

## Design

### Principles

1. **Layered, opt-in, no all-or-nothing.** Core stays small; opinions (ordering policies, richer merge strategies, migration helpers) live in consumer land or future opt-in namespaces, built on public seams.
2. **The codec is where merge behaviour is chosen — the model's type is the merge choice** (L7). `Text` merges as text, `HashMap` merges element-wise by key, `IndexList` merges as a value sequence, a plain value is an LWW register, `atomic` is deliberate wholesale replacement, `custom` is yours. Nothing implicit.
3. **Bind, never replace.** After init, the runtime only ever applies deltas to existing shared types. It never re-creates a container, never re-parents an instance (U5, U11).
4. **Leave what you don't own.** The runtime never deletes keys it didn't encode (U15). Other clients and other schema versions co-exist by default.
5. **Honest semantics, documented.** LWW tiebreak is deterministic-not-temporal (U4); the offline first-create race is an app-design concern with a stated rule (U2a); delete beats edit-inside (U9); structural moves can duplicate (U13). These go in the README, not a footnote.
6. **Dependency posture:** Elmish and Yjs are public vocabulary (Yjs deliberately so, in the escape hatch). Adaptive is plumbing — required today at `Create`/`Update` because of the Adaptify generator, kept out of every new public contract, and squeezed inward over time.

### Layer map

```
public   ┌────────────────────────────────────────────────────────────┐
         │ Ylmish.Program.withYlmish     Elmish integration           │
         │ Ylmish.Codec (Encode/Decode)  schema + merge policy        │
         │ Ylmish.Text                   mergeable text value         │
         │ Ylmish.CustomElement          escape hatch (Yjs verbatim)  │
         ├────────────────────────────────────────────────────────────┤
internal │ Ylmish.Internal.Binding       encoded tree ↔ Y.Doc         │
         │ Ylmish.Internal.Y             attach/delta plumbing        │
         │ (FSharp.Data.Adaptive)        diff engine; public only at  │
         │                               Adaptify Create/Update, and  │
         │                               shrinking                    │
         │ Fable.Yjs                     bindings (own pkg; public    │
         │                               types resurface in the hatch)│
         └────────────────────────────────────────────────────────────┘
```

### The consumer experience

The target end-to-end feel, for a collaborative todo app. Note the shapes: todos are a `HashMap` keyed by an id the app mints at creation (which is also what makes offline creation safe, per the accepted-limitation rule), ordering is a plain LWW `Order` field, and no `aval` appears anywhere:

```fsharp
open Ylmish

[<ModelType>]
type Todo = {
    Title : Text            // collaborative: concurrent edits merge
    Done  : bool            // register: last-writer-wins
    Order : string          // fractional index; LWW; consumer policy
}

[<ModelType>]
type Model = {
    Todos  : HashMap<string, Todo>   // keyed by app-minted id ⇒ element-wise merge
    Filter : Filter                  // app-only: not encoded, never persisted
}

module Codec =
    open Ylmish.Codec

    let encodeTodo (t : AdaptiveTodo) = Encode.object [
        "title", Encode.text t.Title
        "done",  Encode.bool t.Done
        "order", Encode.string t.Order
    ]

    let decodeTodo = Decode.object {
        let! title = Decode.object.required "title" Decode.text
        let! done_ = Decode.object.optional "done" Decode.bool
        let! order = Decode.object.optional "order" Decode.string
        return { Title = title; Done = defaultArg done_ false; Order = defaultArg order "a0" }
    }

    let encode (m : AdaptiveModel) = Encode.object [
        "todos", Encode.map encodeTodo m.Todos
    ]

    let decode = Decode.object {
        let! model = Decode.ask                    // current model, for app-only fields
        let! todos = Decode.object.optional "todos" (Decode.map decodeTodo)
        return { model with Todos = defaultArg todos HashMap.empty }
    }

Program.mkProgram init update view
|> Ylmish.Program.withYlmish {
    Doc    = doc
    Create = AdaptiveModel.Create      // Adaptify-generated (the one Adaptive seam)
    Update = AdaptiveModel.Update
    Encode = Codec.encode
    Decode = Codec.decode
    OnError = Ylmish.Program.OnError.log
}
|> Program.run
```

Notes on ergonomics:

- The model stays a plain immutable record; the only Ylmish type in it is `Text`.
- `Decode.ask` (the Reader environment) is how app-only state survives remote updates.
- `Decode.object.optional` + defaults is how "decode-empty = init" falls out naturally: an empty doc decodes to the init state through the same decoder as any other doc state.
- Choosing merge behaviour per field is exactly one word: `Encode.text` vs `Encode.bool` vs `Encode.map` vs `Encode.atomic` vs `Encode.custom`.

### `Ylmish.Text`

An immutable value type representing collaboratively-edited text, carrying **edit intent** so the runtime can apply precise splices to the backing `Y.Text` (rather than guessing by diffing strings).

```fsharp
type Text                                   // opaque; content + pending intent
module Text =
    val empty    : Text
    val ofString : string -> Text
    val toString : Text -> string
    val length   : Text -> int
    // intent-carrying edits (what update functions use)
    val insert   : at:int -> string -> Text -> Text
    val remove   : at:int -> count:int -> Text -> Text
    val replace  : at:int -> count:int -> string -> Text -> Text
    // convenience for plain <input>/<textarea> onChange, derives one splice
    // by common prefix/suffix diff (ambiguous for repeats; documented; the
    // affix-diff strategy is validated minimal by L3)
    val edit     : newValue:string -> Text -> Text
```

Semantics:

- **Equality and comparison are by content only.** Pending intent is transport, not identity. Views and tests stay simple.
- The runtime drains intents when flushing to the backing `Y.Text` and returns intent-free `Text` values on the way back in.
- Consumers wiring a real editor (CodeMirror, Monaco) who want the actual `Y.Text` bypass `Text` entirely via the `CustomElement` hatch.

### The codec, v2

The codec (moving to `Ylmish.Codec`) gains typed primitives (U10), a `Text` element kind, `atomic`, and `custom`. The taxonomy — **pick the model type that matches the merge you want** (L7):

| Model field | Combinator | Backed by | Concurrent behaviour |
| --- | --- | --- | --- |
| `Text` | `Encode.text` | `Y.Text` | splices interleave and merge |
| `bool`/`int`/`float`/`string`/… | `Encode.bool`/… (`Encode.value` general) | map entry | LWW register (deterministic tiebreak) |
| record | `Encode.object` | bound `Y.Map` | per-key merge of whatever each field chose (L8) |
| `HashMap<string, 'T>` | `Encode.map` | `Y.Map` of bound entries | element-wise by key: different items never conflict; per-item fields merge per their own encodings; **the shape for anything creatable offline** |
| `IndexList<'v>` (values only) | `Encode.list` | `Y.Array` | insert/delete merge, diff-reconciled by value; no text/custom inside items (L1 — runtime error pointing at `Encode.map`) |
| any subtree | `Encode.atomic` | single wholesale value | deliberate whole-subtree LWW (L8) |
| anything | `Encode.custom` | consumer-bound Y type | consumer-defined — see the escape hatch |

```
Encoded  ::= object [(key, Encoded)]        → Y.Map     (per-key merge)
           | map    encodeItem amap         → Y.Map     (element-wise, keyed — the identity primitive)
           | list   encodeValue alist       → Y.Array   (value sequence, diff-reconciled)
           | text   adaptiveText            → Y.Text    (splice merge)
           | value  toPrimitive aval        → primitive (LWW register)
           | atomic Encoded                 → one value (wholesale LWW)
           | custom CustomElement           → consumer-bound Y type
           | option …                       → presence/absence of any of the above
```

Ordering of keyed items is the consumer's policy: an immutable key names the item, a mutable order field (e.g. fractional index) sorts it — never the reverse (L7). `recipes.md` shows the pattern.

### `Ylmish.CustomElement` — the escape hatch

For merge strategies core doesn't ship (counters, mergeable sets, timestamped LWW) **and** for handing a real Yjs type to something that wants one (a CodeMirror binding wants the `Y.Text` itself). The contract is deliberately Adaptive-free (design decision above; shape proven by L6):

```fsharp
type BindContext = {
    /// Get-or-create this element's slot in the doc as a given Y type.
    /// Real Fable.Yjs types, exposed on purpose. The binding layer guarantees
    /// the instance is integrated exactly once (U5) and adopted, never replaced.
    GetText  : unit -> Y.Text
    GetMap   : unit -> Y.Map<obj>
    GetArray : unit -> Y.Array<obj>
    /// The origin token local writes must be transacted under (echo suppression, undo-readiness).
    Origin   : obj
}

type CustomElement =
    /// Wire the binding: push local model changes into the Y type, keep the
    /// merged value current. Runs once at connect; dispose tears it down.
    abstract Connect : BindContext -> System.IDisposable
    /// Current merged value (read at decode time; boxed — Decode.custom unboxes
    /// under the consumer's type).
    abstract Value : obj
```

`Encode.custom : CustomElement -> Encoded` / `Decode.custom : Decoder<_,_,'a>`. No `Subscribe` member: under bind-in-place, remote changes to the custom's Y type surface through the same root `observeDeep` as everything else, so read-back needs no side channel (L4/L6 — this is the simplification their layout couldn't have). The canonical example (tests + demo + docs) is a grow-only counter whose concurrent increments **sum**; the docs also show the editor-binding scenario (`GetText` handed to a UI component, the field decoded read-only into the model).

### The runtime (internal): binding instead of materializing

`Y.Doc.materialize`/`dematerialize` are deleted. In their place, an internal binding layer walks the encoded tree once and establishes live, bidirectional, delta-level bindings:

- **Encode direction.** Adaptive deltas flow to the bound Y types inside a single `doc.transact` tagged with this instance's **origin token** (U6, U14). All mutations happen inside `transact` — connect realizes the adaptive graph, so untransacted writes are a bug class, not a style choice (L4). Containers that don't exist yet are created at the moment the first local edit touches them, within that same transaction (U2a). Existing containers are adopted, never replaced (U5, U11); a kind mismatch between the encoded field and the existing Y type surfaces as a structured schema-drift error, not a Yjs internal throw (L5). Keys the encoder doesn't mention are never touched (U15). Lists are reconciled by value diff (minimal splices), maps element-wise by key.
- **Decode direction.** One `observeDeep` on the root; transactions carrying our own origin token are ignored; every other transaction produces exactly one decode → one Elmish `Set` dispatch (U7, U14). Custom elements ride the same observer (L6). Decode failures go to `OnError` and leave the current model in place — a malformed or newer-versioned doc can't crash the loop.

The existing `Text/Array/Map.attach` delta plumbing in `Y.fs` is the substance of this layer; it gets origin-based guards instead of boolean flags, direction-split as the old TODO already called for, and moves under `Ylmish.Internal`.

### Migrations: expressible, deferred

**No Migrations module ships in this plan.** What *is* in scope is keeping migrations expressible with nothing but the public codec surface:

- Decoders compose, so "read old-or-new, prefer new" is an `oneOf`-shaped combinator over ordinary decoders.
- Encoders produce key sets that can be merged, so "write both shapes during rollout" is a fold over ordinary encoders.
- The load-bearing part is a **runtime rule**, and it is in scope: the binding layer never deletes keys it didn't encode (U15).

The dual-key rolling-upgrade recipe becomes a documentation recipe, exercised by a compatibility test in Step 7 — not an API. (The executed branch built helpers and reverted them; deferral is validated.)

## Plan of work

Each step is one PR-sized unit: it starts by writing failing (or characterization) tests, ends with `npm test` green, and leaves the library releasable.

### Step 1 — Pin the assumptions (Yjs **and** Adaptive)

Port `doc/plans/0002-assumptions/*.mjs` to `tests/Ylmish.Tests/Y.Assumptions.fs` (U2a/U2b, U3, U4, U5b, U6, U9, U11, U13, U14, U15 at minimum), and re-pin the Adaptify delta characterization (A1/L1: rebuild-regime positional rewrites; positional rebinding of nested adaptive objects; `HashMap` keyed reconcile) in `Adaptive.Assumptions.fs`.

*Acceptance:* suite green; every design-consequence claim in this plan is enforced by CI, so a Yjs or Adaptify upgrade that changes semantics fails loudly.

### Step 2 — Differential harness + north-star acceptance (red)

Build the verification backbone modeled on the executed branch's harness (L2): a `Bridge` abstraction (whole immutable models in, converged model out — the `withYlmish` contract), a schedule-replay driver with delivery policies (Immediate/Concurrent), and a `differential` runner comparing the SUT against a raw-Yjs oracle. Calibrate it: green on the oracle, red on the current materialize path (the known #83 bug), green on the current path for sequential edits (it discriminates).

Then write the north-star tests against the *target* public API, `ptestCase`-skipped: concurrent `Text` edits converge interleaved across two `withYlmish` programs; unknown keys survive a full session; keyed-map concurrent adds both survive.

*Acceptance:* harness trustworthy by construction; north stars compile against the intended API signatures, skipped. Un-skipped in Step 7.

### Step 3 — `Ylmish.Text`

Pure value type, no Yjs dependency.

*Tests first:* content equality/hash laws; `insert`/`remove`/`replace` semantics incl. bounds; property — replaying any intent sequence over the old content equals the new content; property — `Text.edit` produces a single minimal splice (affix diff, L3) whose application equals the new string; Adaptify round-trip (`cval<Text>` works in a `[<ModelType>]`).

*Acceptance:* `Text` usable in a model today, even before sync exists.

### Step 4 — Codec v2: typed primitives, `text`, `atomic`, `custom`, keyed `map`

*Tests first:* encode/decode round-trip properties for bool/int/float/string/null, `option`, nested objects, `map` (→ `HashMap`), `list` (values), `text`, `atomic` (round-trips as the inner codec), `custom` (element carries the binding; `Decode.custom` reads `Value`); error paths (`UnexpectedKind`, text/custom inside `Encode.list` items rejected per L1) with path reporting; `Decode.ask`. Re-enable or rewrite the two disabled roundtrip tests (#10/#12).

*Acceptance:* codec fully specified with zero Yjs runtime involvement (the `CustomElement` *type* references Fable.Yjs — that's the decided dependency posture).

### Step 5 — Binding layer, encode direction

Internal `Binding.attach doc encoded : IDisposable`, replacing `materialize`. Sub-steps, each red-green: (a) registers + objects — proving nested records merge per-field with no flattening machinery (L8); (b) keyed maps — element-wise reconcile, nested text under items, and the identity headline from their harness: **a keyed item's nested text survives concurrent membership changes and stays with its item** (L1/L7); (c) value lists — diff-reconciled minimal splices; (d) text; (e) atomic; (f) custom dispatch through `BindContext`; (g) composition.

*Tests first (behavioural contract, two-doc via the Step 2 harness where relevant):* lazy container creation in one transaction; adopt-never-replace (the U11 anti-test); untouched unknown keys; kind-drift structured error (L5); all writes transacted under the origin token (L4); O(delta) op counts via `doc.on("update")` for the keyed/list/text paths.

*Acceptance:* `materialize` deleted; the harness's differential runner passes on every sub-step's slice where the old path failed.

### Step 6 — Binding layer, decode direction

One `observeDeep` → decode → dispatch, custom elements riding the same observer.

*Tests first:* own-origin transactions ignored (no echo, no loop); one remote transaction spanning many types = exactly one `Set` (U14); remote text edits surface as intent-free `Text` values; remote custom edits surface through `Decode.custom` with no side channel (L6); decode failure invokes `OnError`, keeps the model, loop survives; the two imported crash regressions — a remote apply mid-flight never triggers a re-entrant local `Y.Text` write, and no adaptive mutation escapes `transact` (L4).

*Acceptance:* full bidirectional binding at the adaptive layer, still without Elmish.

### Step 7 — `withYlmish` v2, end-to-end

Rewire `Program.withYlmish` onto the binding layer; decode-empty-=-init on startup; delete the dead commented code in `Program.fs`.

*Tests:* un-skip Step 2's north stars — they pass, including through the differential harness's `withYlmish` bridge; the existing `Program.fs` suite (fixing the known wrong assertions) passes; `TodoCollaborative` gains the concurrent-edit cases; the **compatibility test** proving the dual-key migration recipe with plain combinators — a v1-schema program and a v2-schema program share a doc, edit concurrently, and neither destroys the other's representation.

*Acceptance:* issue #83 closed by tests, not by claim.

### Step 8 — `CustomElement` escape hatch, proven end-to-end

Public `Encode.custom`/`Decode.custom` + `BindContext` (Step 4 declared the types; this step proves them under `withYlmish`).

*Tests first:* a consumer-authored grow-only counter **sums** concurrent increments across two `withYlmish` peers, converging in both Elmish models; the editor scenario — `GetText` hands out the live `Y.Text`, external edits to it flow into the model's decoded field; a custom binding cannot double-integrate its Y type (U5 guarded by `BindContext`).

*Acceptance:* the hatch is sufficient to build a counter *without touching Ylmish internals or Adaptive* — the escape-hatch and encapsulation claims proven together.

### Step 9 — Property-based stress

Random two-replica add/remove/edit schedules over the keyed-map + text + register model, under Immediate and Concurrent delivery, replayed deterministically through the Step 2 harness: converged (P1), matches the raw-Yjs oracle (M1), no-loss where the oracle guarantees it (L2).

*Acceptance:* ~100 schedules per policy, deterministic seeds, green in CI.

### Step 10 — The demo

`examples/TodoCollaborative` stays a **CLI (Node) program**, a scripted narrative of the system under interesting concurrency. Two `withYlmish` programs on independent `Y.Doc`s, sync performed explicitly between acts, output printed per act:

1. Two peers start; neither seeds; both show init state (decode-empty = init, visibly).
2. Offline: both edit the same note concurrently → sync → the interleaved merged text.
3. Offline: both *create* todos concurrently (app-minted ids, `Encode.map`) → sync → both items survive — the accepted-limitation rule demonstrated positively.
4. Concurrent edits to *different fields of the same todo* → sync → both stick (per-field merge, L8).
5. Both flip the same LWW register concurrently → sync → one deterministic winner, shown honestly as a clobber.
6. One peer deletes a todo while the other edits inside it → sync → delete wins.
7. Concurrent reorders via the fractional `Order` field → sync → converged order, no duplication (contrast with the documented structural-move limit).
8. A consumer counter (the escape hatch) summing concurrent increments.
9. An app-only field changes throughout and never appears in either doc.

*Acceptance:* the demo exercises only the public API; `npm run demo` output reads as documentation, and the transcript is embedded in the README.

### Step 11 — Docs rewrite

- **README:** keep the philosophy sections; replace the TODO list with: a 60-second quickstart mirroring the demo; the **taxonomy table** ("the model's type is the merge choice") doubling as the merge-semantics table, with the honest limits (LWW tiebreak deterministic-not-temporal; the offline first-create rule — *anything creatable offline needs a unique key*; delete-beats-edit; structural-move duplication); the layer map with the public/internal line and the dependency posture (Yjs/Elmish public, Adaptive shrinking); the demo transcript.
- **doc/guides/**: `codec.md` (schema decoupling, `Decode.ask`, app-only state), `text.md` (Text semantics, `edit` ambiguity), `custom-elements.md` (writing a binding; the counter; wiring an editor to `GetText`), `recipes.md` (dual-key rolling migration; fractional-index ordering over `Encode.map`; modelling offline-creatable entities with app-minted keys).
- **AGENTS.md**: update layout/testing sections (the harness becomes a documented testing tool); mark plans 0001 and 0002 statuses.

*Acceptance:* every code sample in the docs is compiled (samples live in the demo or a doc-tests project).

## Open questions

None blocking Steps 1–4:

1. **`OnError` shape:** is "log and keep current model" the right decode-failure policy, or should consumers get the raw errors + the offending doc state to decide (e.g. surface "this doc was written by a newer client" UX)?
2. **`Text` equality:** equality by content only (intent excluded) — acceptable? It makes `=`/memoization intuitive but means "has pending edits" is not observable via equality.
3. **`Text.edit` ambiguity:** for repeated characters the single-splice diff can pick a different position than the user's actual edit (convergence unaffected; interleaving fidelity slightly coarser). Fine for the plain-`<input>` path given precise `insert`/`remove` exist?
4. **Presence/awareness** (cursors, who's-online): explicitly out of scope for this plan, or wanted as a future optional module worth leaving API room for?
5. **Undo:** `Y.UndoManager` integrates naturally with origin tokens (undo only your own origin's ops). Out of scope here, but the `BindContext.Origin` design deliberately reserves room for it.
6. **`Encode.map` key types:** `string` keys only for v1 (Yjs map keys are strings); non-string keys would need a key codec. Acceptable to defer?

## Out of scope

- Ycs/Yrs (.NET-native) backends — the old README TODO stands, unaffected.
- Rich-text attributes/embeds in `Text` (Y.Text formatting) — plain text only; the escape hatch covers editors that need more.
- Async resolution of decoded references (README TODO item on author IDs) — remains an app-layer concern via `Cmd`.
- Provider integrations (y-websocket, y-indexeddb persistence) — the demo uses raw `applyUpdate` wiring; providers are the consumer's choice.
- Ordering policies (fractional indexing) — consumer recipe, not library code (L7).
