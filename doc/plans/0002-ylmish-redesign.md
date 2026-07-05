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
| The nested first-create race (U2a) | **Accepted as a documented limitation, solved by application design.** If two offline peers each *first-create* the same nested container and later sync, one subtree wins wholesale. The app-level rule: anything that can be *created* offline needs a consumer-supplied unique key (`Encode.map` keyed by it), so independent offline creations occupy distinct keys and never race. Editing an *existing* field offline is the normal CRDT case and merges. The library documents this rule; it does not contort the document layout to dissolve the race. **Amended at Step 5:** top-level structural containers anchor to *named Yjs root types*, which merge by name (U2b) — so the flagship scenario (two fresh offline peers populating the same top-level collection) cannot race at all. The residual U2a race is narrowed to a fixed-path record nested *inside* another container, on its very first write. |
| Packaging | **One `Ylmish` package** (plus `Fable.Yjs`); opinionated extras are separate namespaces you must explicitly `open`. |
| Per-field merge menu | **LWW registers, mergeable Text, mergeable lists (Y.Array), keyed element-wise maps** — plus **`Encode.atomic`** (deliberate wholesale-LWW subtree) and **`Encode.custom`** (the `CustomElement` escape hatch for consumer-defined merge strategies and direct Y-type access). Opinionated strategies built *on* the hatch (counters, timestamped LWW) stay out of core. The demo must exercise list and keyed-map merging, not just text. |
| List semantics | **`Encode.list` is for value sequences** (insert/delete merge, diff-reconciled). **Records with identity go in `Encode.map`** over `HashMap<key, _>` — this is now a hard rule, not a preference, because Adaptify's list deltas are positional (see L1) and would corrupt nested collaborative state under insert/reorder. The rule is **enforced by construction, not at runtime**: `Encode.list` accepts only the explicit `Value` primitive sub-language (an opaque `Value.Encoder<'a>`), so a text/custom/object item does not typecheck. Ordering of keyed items is the consumer's concern (fractional-index recipe in docs). |
| Migrations | **Defer entirely.** No Migrations module. The codec keeps migrations *expressible* (decoders compose, encoders combine, the runtime preserves unknown keys) and the docs show the dual-key recipe. (The executed branch independently built migration helpers and then cut them — treat that as confirmation.) |
| Demo form | **CLI.** A Node script whose output demonstrates the system under interesting concurrency: offline divergence, text interleaving, list merges, keyed-map merges, an honest LWW clobber, reconvergence. |

## Lessons imported from `claude/github-issues-visibility-8k12g3`

That branch executed its own plans 0002–0009 to close #83 by accretion (flatten collaborative leaves to scheme-named top-level roots, keep `materialize` for the structural rest). This plan takes the other architecture — bind nested types in place, delete `materialize` — but its evidence is real and is folded in here:

| # | Lesson (their evidence) | Consequence in this plan |
| --- | --- | --- |
| L1 | **Adaptify list deltas are positional, not keyed** (their 0006 Step 1, pinned in `Adaptive.Spike.fs`). The idiomatic rebuild `{ m with Items = recompute() }` yields O(n) positional value-rewrites, and `ChangeableModelList` **rebinds nested adaptive objects by position** — a nested `Y.Text` hung off item *i* gets hijacked by whatever lands at position *i*. | `Encode.list` is restricted to value sequences **by its type**: it accepts only `Value.Encoder<'a>` from the primitive sub-language, so text/custom/object items are unrepresentable rather than runtime-rejected. Step 1 re-pins this characterization in our suite. The plan's O(delta) claims are scoped: lists are diff-reconciled by value, maps are keyed by construction (`HashMap` → keyed `amap` reconcile, their 0008 Step 0a). |
| L2 | **Differential testing against a raw-Yjs oracle** (their 0006 harness): replay per-replica schedules under a delivery policy, compare the full bridge against hand-written Yjs, plus property-based random schedules. It caught the whole-container-LWW bug red-handed and *discriminated* (green on sequential). | Adopted as this plan's verification backbone (Steps 2a and 9). No self-authored spec to be wrong about. |
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
2. **The codec is where merge behaviour is chosen — the model's type is the merge choice** (L7). `Text` merges as text, `HashMap` merges element-wise by key, `IndexList` merges as a value sequence, a plain value is an LWW register, `atomic` is deliberate wholesale replacement, `custom` is yours. Nothing implicit — and **illegal encodings are unrepresentable, not runtime-checked**: positions with restricted vocabularies (list items, registers) take their own narrower encoder types.
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
| `IndexList<'v>` (values only) | `Encode.list` | `Y.Array` | insert/delete merge, diff-reconciled by value; items are `Value` primitives *by construction* (L1) — entities belong in `Encode.map` |
| any subtree | `Encode.atomic` | single wholesale value | deliberate whole-subtree LWW (L8) |
| anything | `Encode.custom` | consumer-bound Y type | consumer-defined — see the escape hatch |

```
Encoded  ::= object [(key, Encoded)]        → Y.Map     (per-key merge)
           | map    encodeItem amap         → Y.Map     (element-wise, keyed — the identity primitive)
           | list   Value.Encoder alist     → Y.Array   (value sequence, diff-reconciled)
           | text   adaptiveText            → Y.Text    (splice merge)
           | value  Value.Encoder aval      → primitive (LWW register)
           | atomic Encoded                 → one value (wholesale LWW)
           | custom CustomElement           → consumer-bound Y type
           | option …                       → presence/absence of any of the above
```

**The `Value` sub-language — incorrect by construction.** Positions that can only hold JSON primitives (list items, registers) do not take an arbitrary `Encoded`; they take an opaque `Value.Encoder<'a>`, constructible *only* from an explicitly enumerated set of primitives. There is no injection from `Encoded` into `Value.Encoder`, so "a list of texts" or "a list of objects" is not a runtime error — it is a type error at the call site, and the fix (`Encode.map`) is visible in the signature you reach for instead.

```fsharp
module Ylmish.Codec.Value

type Encoder<'a>        // opaque: 'a → JSON primitive
type Decoder<'a>        // opaque: JSON primitive → 'a, with path-tracked errors

module Encode =         // Value.Encode.string, Value.Encode.int, …
    val string : Encoder<string>      val int  : Encoder<int>
    val float  : Encoder<float>       val bool : Encoder<bool>
    // domain types ride a primitive by mapping, staying inside the sub-language:
    val contramap : ('b -> 'a) -> Encoder<'a> -> Encoder<'b>   // e.g. TodoId → string

module Decode =         // Value.Decode.string, …
    val string : Decoder<string>      val int  : Decoder<int>
    val float  : Decoder<float>       val bool : Decoder<bool>
    val map : ('a -> 'b) -> Decoder<'a> -> Decoder<'b>
```

(Encoders and decoders live in `Value.Encode`/`Value.Decode` submodules so both sides can use the plain primitive names — confirmed with Nick at the 2b review.)

The register combinators are then sugar over the same sub-language (`Encode.bool = Encode.value Value.Encode.bool`), the codec has exactly one notion of "primitive" shared by registers and list items, and **optionality composes by name** over any single-`aval` encoding — `None` is key-absence, never null:

```fsharp
val Encode.value  : Value.Encoder<'a> -> aval<'a>  -> Encoded
val Encode.list   : Value.Encoder<'a> -> alist<'a> -> Encoded
val Encode.option : (aval<'a> -> Encoded) -> aval<'a option> -> Encoded
// e.g. Encode.option Encode.text m.Note — an optional collaborative text.
// None→Some creates the backing type lazily (a local edit); Some→None deletes
// the key (delete beats concurrent inner edits, U9). Collections have no aval
// view; an empty map/list is their "none".
val Decode.list   : Value.Decoder<'a> -> Decoder<'model, IndexList<'a>>
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

**Layout (the wire format, fixed at Step 5):** top-level *registers* (and options/atomics/customs) live as entries in the argless root map — it exists everywhere and merges per-key, so there is no creation race (U1). Top-level structural *containers* — objects, keyed maps, lists, texts — anchor to **named Yjs root types** (`doc.getMap "todos"`, `doc.getText "body"`, …), which merge by name (U2b): the schema's own field names act as the "consumer-supplied unique keys" of the accepted-limitation rule, applied by the library. Below the top level, containers are created lazily in place; keyed-map items are protected by their app-minted keys.

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

Each step is one PR-sized unit: it starts by writing failing (or characterization) tests, ends with `npm test` green, and leaves the library releasable. Sub-steps within a step are individually committable green increments.

### Working the plan

For the engineer executing this (competent F#, new to this codebase):

- **Setup.** `npm install && npm test` must be green before Step 1 (see `AGENTS.md`; tests run via Fable→Mocha — `dotnet test` will not work). Record the passing count: it is your baseline number, and every check-in reports the new one.
- **Reference quarry.** Branch `claude/github-issues-visibility-8k12g3` executed a *different* architecture for this same issue, but much of its **test code transfers**: `tests/Ylmish.Tests/Y.Assumptions.fs` and `Adaptive.Spike.fs` (Step 1), `Harness.fs`/`Harness.Options.fs`/`Harness.Stress.fs` (Steps 2a, 9), and its `Codec.*.fs` tests as scenario inspiration. Crib freely and adapt; do **not** import its `Scheme`/flattening design, its boolean reentrancy guards, or its `materialize` hybrid — those are exactly what this plan replaces.
- **Interim-compatibility rule (Steps 4–6).** The old `materialize`/`dematerialize` path stays alive until Step 7 — `withYlmish` runs on it until then and the suite must stay green at every step boundary. When Step 4 adds `Element` cases, the old path handles them with a descriptive `failwith` ("Text/Atomic/Custom fields require the binding runtime; see plan 0002 Step 7") — never silently skipping. Deleting the old path is Step 7's job, not yours early.
- **Cadence.** One commit per green increment. Never commit red. If a step's premise turns out wrong mid-flight, stop and check in — this plan encodes decisions that are Nick's to change, and improvising around a broken premise is how the reference branch accreted three naming conventions.
- **Check-in ritual (after every step, before the next):** tick the step in *Progress* below and bump the test count; add a short *Decisions & lessons* note under the step recording anything surprising or anything you had to interpret (interpretation is a design decision in disguise — surface it); show the diff, the names of the new tests, and the runnable output where a step has one (harness calibration, demo acts).

### Progress

- [x] Step 0 — Baseline (S) — 99 passing, 0 skipped
- [x] Step 1 — Pin the assumptions (M) — 125 passing (+16 Y.Assumptions, +10 Adaptive.Assumptions)
- [x] Step 2a — Differential harness (M) — 130 passing (+5 calibration)
- [x] Step 2b — Target API skeleton + north stars (M — **design-review checkpoint**) — 130 passing, 3 pending (the north stars, skipped until Step 7) — **awaiting Nick's signature review**
- [x] Step 3 — `Ylmish.Text` (M) — 142 passing (+12), 3 pending — **note the Adaptify finding: `Text` is a sealed class, not a record**
- [x] Step 4 — Codec v2 (L; sub-steps 4a–4e) — 158 passing (+16), 3 pending
- [x] Step 5 — Binding, encode direction (L; sub-steps 5a–5g) — 174 passing (+16), 3 pending — **note the design amendment: root anchoring for top-level containers**
- [ ] Step 6 — Binding, decode direction (M)
- [ ] Step 7 — `withYlmish` v2; old path dies (M)
- [ ] Step 8 — `CustomElement` end-to-end (M)
- [ ] Step 9 — Property stress (S)
- [ ] Step 10 — Demo (M)
- [ ] Step 11 — Docs (M)

### Step 0 — Baseline

No code changes. Get the toolchain running per `AGENTS.md`, run `npm test`, record the passing/skipped counts in *Progress*, and skim `src/Ylmish/Y.fs`, `Adaptive.Codec.fs`, `Program.fs` plus this plan's *Validated assumptions* table.

*Check-in:* baseline test count; anything already broken on your machine; questions about the plan itself before work starts.

*Decisions & lessons (executed 2026-07-04):* baseline 99 passing, 0 skipped, nothing broken. (PR #123 "establish test baseline" merged with an empty diff, so the count is recorded here instead.) Toolchain note (from the parallel Step 0 runs, PRs #125/#127): fresh environments ship without a .NET SDK, so `npm install` fails at `dotnet restore` — install SDK 10.0.300 per `global.json` via `dotnet-install.sh` first. One benign pre-existing restore warning (`NU1608`: `YoloDev.Expecto.TestSdk 0.13.3` wants `Expecto < 10`, `10.2.3` resolves) — tests pass regardless.

### Step 1 — Pin the assumptions (Yjs **and** Adaptive)

Port `doc/plans/0002-assumptions/*.mjs` to `tests/Ylmish.Tests/Y.Assumptions.fs` (U2a/U2b, U3, U4, U5b, U6, U9, U11, U13, U14, U15 at minimum), and re-pin the Adaptify delta characterization (A1/L1: rebuild-regime positional rewrites; positional rebinding of nested adaptive objects; `HashMap` keyed reconcile) in `Adaptive.Assumptions.fs`. The reference branch has working versions of both files to adapt.

*Acceptance:* suite green; every design-consequence claim in this plan is enforced by CI, so a Yjs or Adaptify upgrade that changes semantics fails loudly.

*Check-in:* test count; a one-line map from each assumption id (U*/A1) to its test name; any assumption that did **not** reproduce (that's a stop-the-line finding — it invalidates part of the design).

*Decisions & lessons (executed 2026-07-04):*

- Every assumption reproduced exactly as tabled — no stop-the-line findings. Assumption → test map (all in `Y.Assumptions.fs` under the same-named `testList`): U1 root map identity, U2a nested first-create race, U2b root-level create race, U3 shared nested Y.Text, U3b plain-string map value, U4 LWW tiebreak, U5b re-parenting an integrated type, U6 transaction origins, U7 observeDeep coverage, U8 concurrent array inserts, U9 delete vs edit-inside, U10 typed primitives in Y.Map, U11 replace vs concurrent edit, U13 concurrent structural move, U14 transaction batching, U15 unknown keys. A1/L1 in `Adaptive.Assumptions.fs`: three IndexList structural-op tests (minimal deltas), rebuild + reorder positional-rewrite tests, positional nested rebinding, and four HashMap keyed-reconcile tests (O(delta) ops on add/remove; zero outer ops on value-update; identity follows the key). Ported beyond the plan's minimum: U1, U3b, U7, U8, U10 (cheap, and each backs a design claim). Not ported: U5 (subsumed by U5b), U12 (subsumed by U2a).
- The HashMap keyed-reconcile characterization needed a model with a `HashMap` field; added `MapModel` to the test common's `Example.fs` (Adaptify generates a keyed `ChangeableModelMap` for it, confirming the L1 contrast by construction).
- Interpretation: U7's path-tracked events (`["list", 0]`) are pinned as *coverage* (event counts through one deep observer) plus **target identity** (the nested text's deep event carries the `Y.Text` instance itself, by reference — grafted from #127's approach), not paths — the Fable.Yjs `YEvent` binding doesn't expose `path`. The binding gap is real and Step 6 (decode direction) will need `path` bound; noting rather than fixing here since Step 1 adds no production code.
- Fable interop gotcha: an F# `int[]` compiles to a JS `Int32Array`, which Yjs rejects with "Unexpected content type" — U10 pins plain (boxed) arrays. Worth remembering for the codec's list encoding.
- Fable interop gotcha #2 (grafted from #127): a stored JS `null` is a real Y.Map value, but the binding's `YMap.get : string -> 'T option` collapses it to `None` — **indistinguishable from a missing key**. U10 pins null presence via `has`. Design consequence for Step 4: `Encode.option` must model absence via key presence, never via null through `get`.
- U5b makes Yjs log an internal `TypeError` to the console mid-suite; expected noise, documented in the test.
- Post-review hardening (review on PR #126): U2a, U3b and U8 now pin their exact deterministic outcomes (survivor `["from-d2"]`; winner `"oh, hello"`; converged order `["from-d1"; "from-d2"; "base"]`) instead of disjunctions/sorted membership — a flipped tiebreak now fails loudly. Added the keyed **value-update** delta characterization (editing an existing key's value = 0 outer amap ops, absorbed by the nested adaptive object in place) — the case Step 5b's O(delta) claims lean on hardest.
- Inventory for Step 7 (grafted from #127's skim): `Program.fs` carries ~330 lines of commented-out prior codec sketches below the live `withYlmish` — part of Step 7's deletion.

### Step 2a — Differential harness

Build the verification backbone modeled on the reference branch's `Harness.fs` (L2): a `Bridge` abstraction (whole immutable models in, converged model out — the `withYlmish` contract), a schedule-replay driver with delivery policies (Immediate/Concurrent), and a `differential` runner comparing the system-under-test against a raw-Yjs oracle (the same schedule hand-applied to plain Y types). Keep it minimal: two replicas, one model shape, deterministic replay.

Calibrate it — this is the step's real deliverable: **green on the oracle** (ground truth holds), **red on the current materialize path** (it catches the known #83 bug and names the lost data), **green on the current path for sequential edits** (it discriminates rather than failing everything).

*Acceptance:* the three calibration results above, as committed tests (the materialize-red one marked as expected-fail/pending so the suite stays green).

*Check-in:* test count; the calibration triad's output; the harness's public surface (it will be reused in Steps 5, 7, 9).

*Decisions & lessons (executed 2026-07-04):*

- 130 passing (+5). `tests/Ylmish.Tests/Harness.fs`, adapted from the reference branch's `Harness.fs` per the quarry note — the design transfers wholesale (Bridge / schedule driver / differential vs raw-Yjs oracle / minimality meter).
- Public surface for reuse: `Bridge`/`BridgeFactory` (whole models in, converged model out), `run : BridgeFactory -> Delivery -> ReplicaOp list -> RunResult` with `Immediate`/`Concurrent` delivery, `differential` (SUT vs oracle, reports `Lost` ids), `incrementalBytes`/`measureApplies` (O(delta)-vs-O(state) meter, calibrated now, first *used* in Step 5).
- **Interpretation surfaced:** the plan said the materialize-red calibration lands "as expected-fail/pending". A pending test asserts nothing, so instead it lands as a *passing* test that asserts the mismatch and the lost id (`MatchesOracle = false`, `Lost` non-empty) — a stronger pin with the same green suite. When Step 5 fixes the bug this test fails by design and gets flipped into the fix's regression test; its message says so.
- The driver models `withYlmish`'s remote-update → read-back → `Set` loop on every delivery (refresh the receiving replica's model from its bridge before its next local op). Without this the old path would fail even sequentially and the harness would be hostile rather than discriminating.

### Step 2b — Target API skeleton + north stars (red) — **design-review checkpoint**

Transcribe this plan's API sketches into compiling F# signatures with stub bodies: `Ylmish.Text` (module + type), `Ylmish.Codec` (`Value` sub-language, `Encode.*`/`Decode.*` including `map`/`text`/`atomic`/`custom`), `CustomElement`/`BindContext`, and the `withYlmish` options record. Stubs `failwith "plan 0002: not implemented until Step N"`.

Then write the north-star tests against that surface, `ptestCase`-skipped: concurrent `Text` edits converge interleaved across two `withYlmish` programs; unknown keys survive a full session; keyed-map concurrent adds both survive.

*Acceptance:* everything compiles; north stars are skipped; **no implementation**.

*Check-in:* this is the one to slow down on — the diff *is* the public API. Nick reviews the signatures here, before any implementation exists to defend. Surface every place the sketches in this document were ambiguous and what you chose.

*Decisions & lessons (executed 2026-07-04):* 130 passing + 3 pending (the north stars). New files: `src/Ylmish/Text.fs`, `src/Ylmish/Codec.fs`, `Ylmish.Program.V2` (appended to `Program.fs`), `tests/Ylmish.Tests/NorthStar.fs`. Interpretations made where the plan's sketches were ambiguous — **each is a review point, none is defended**:

1. **`Value.Encode.*` / `Value.Decode.*` submodules**, not the sketch's flat `Value.string` — one module can't hold an encoder and a decoder both named `string`. **Resolved with Nick (2b review): the submodule shape is confirmed**; the design-section sketch was updated to match.
2. **`Ylmish.Program.V2`** nests the new options record + `withYlmish` so the running v1 stays untouched; Step 7 deletes v1 and promotes these names. Scaffolding, not the final shape.
3. **No `aval` anywhere in the v2 surface**: `Decoder<'model,'a>` is opaque and `Decode.run : 'model -> Decoder<'model,'r> -> Y.Doc -> Result<'r, Error list>` is synchronous — v1's `Decoded<'a> = Validation aval` is gone; liveness (when to re-decode) is the runtime's concern (Step 6). This is the dependency-posture decision applied literally.
4. **`OnError` is a record** `{ Handle : Error list -> unit }`, not a single-case union — a case named like its type shadows the companion module (`OnError.log` resolved to the case constructor, found empirically).
5. **`Encode.option` — resolved with Nick (2b review): the inner encoder takes the adaptive view.** `Encode.option : (aval<'a> -> Encoded) -> aval<'a option> -> Encoded`, so optionality composes by name with every single-`aval` combinator (`Encode.option Encode.text m.Note`) and the decode side is already presence-based (`Decode.object.optional "note" Decode.text : Decoder<_, Text option>` — the symmetry falls out). Semantics documented on the combinator: None = key absent (never null); None→Some creates lazily at the transition; Some→None deletes, delete-beats-inner-edits (U9); concurrent initialization of the same optional field is the accepted first-create limitation. **New named risk for Step 5:** the runtime needs a "Some-window" adaptive projection (an inner `aval<'a>` live only while the option is Some) — no FSharp.Data.Adaptive built-in does this; budget for building and testing it. The north stars now exercise a `Note : Text option` field as consumer code.
6. **Composition combinators are inert stubs; only runtime entry points throw.** Found empirically: a consumer's module-level `let decode = Decode.object { … }` evaluates the builder at module load, so throwing stubs crashed the suite before any test ran. The final implementation inherits this constraint: composing a codec must be total and cheap; effects live in `run`/attach.
7. **The `Ylmish.Codec` namespace shadows v1's `Codec.` references in `Y.fs`** (the interim-compatibility rule bit at 2b, earlier than Step 4 predicted) — fixed with a `module V1 = Ylmish.Adaptive.Codec` alias in the materialize path, which Step 7 deletes anyway.
8. **North stars use a hand-written adaptive companion**, not Adaptify — 2b must not take on codegen risk; Step 3 owns proving `[<ModelType>]` with a `Text` field.
9. `Error` is a fresh minimal union (`UnexpectedKind`/`UnexpectedValue`/`MissingProperty` over a `Path` that gains a `MapKey` segment) — independent of v1's, since v1 dies at Step 7.

### Step 3 — `Ylmish.Text`

Pure value type, no Yjs dependency. Replace the Step 2b stubs with the real implementation.

*Tests first:* content equality/hash laws; `insert`/`remove`/`replace` semantics incl. bounds; property — replaying any intent sequence over the old content equals the new content; property — `Text.edit` produces a single minimal splice (affix diff, L3) whose application equals the new string; Adaptify round-trip (`cval<Text>` works in a `[<ModelType>]`).

*Acceptance:* `Text` usable in a model today, even before sync exists.

*Check-in:* test count; the property-test generators (they get reused); confirmation the Adaptify round-trip works (this is the step most likely to surface a generator limitation — if `cval<Text>` doesn't survive `[<ModelType>]`, stop and check in, because it forces a design change).

*Decisions & lessons (executed 2026-07-04):*

- 142 passing (+12), 3 pending. `src/Ylmish/Text.fs` implemented; `tests/Ylmish.Tests/Text.fs` with reusable generators (`TextOp` with deliberately out-of-range positions; a plain-string reference implementation the content semantics are checked against, differential-style).
- **The predicted generator limitation appeared, and forced the predicted design change.** `[<ModelType>]` with a field whose type is a *cross-assembly record with hidden representation* makes Adaptify emit the member as `aval<obj>` while the backing cell is correctly `cval<Ylmish.Text>` — the generated code does not even compile. Diagnosis: Adaptify introspects record fields to build wrappers and falls back to `obj` when it cannot see them across the assembly boundary (same-assembly `MapModel`/`Submodel` were fine; building the referenced project first changed nothing; the 1.3.7 CLI behaves the same). **Fix: `Text` is a `[<Sealed>]` class** with internal state, custom content-only equality/comparison/hash, and unchanged module API — Adaptify passes an opaque class through as a plain changeable value with its type intact (`aval<Ylmish.Text>` confirmed in the regenerated code). **Rule for the rest of the plan: any public Ylmish value type destined for consumer `[<ModelType>]` models must be a class (or public-representation record), never a hidden-representation record.**
- While diagnosing: the Adaptify CLI tool (1.3.7, `dotnet-tools.json`) and the `Adaptify.Core` package (1.3.4, `Directory.Packages.props`) were mismatched — "aligned" both to 1.3.7. **Corrected in Step 4: that skew is deliberate.** `Adaptify.Core` 1.3.7's own library code does not compile under Fable 5 ("cannot get type info of generic parameter T" in its `Helpers.fs`); the package must stay 1.3.4 while the codegen tool runs 1.3.7. Reverted, and now documented here so nobody "fixes" it again. Second lesson from the same incident: Step 3's green run rode a **stale Fable cache** (the package swap didn't recompile `fable_modules`) — when dependencies change, `rm -rf build` before trusting `npm test`.
- Contract decisions pinned by tests: **edit positions clamp** (an out-of-range edit in an Elmish `update` must not crash the loop); **content-neutral edits are elided** (e.g. replacing "a" with "a") — a deliberate consequence of content-only equality, since adaptive propagation is content-driven and such an intent could never reach the doc anyway; `Text.edit`'s affix diff pinned minimal per L3 ("hello"→"hełlo" = remove 1, insert "ł") and property-checked affix-minimal (the splice neither starts nor ends with a kept character).
- Internals (`Text.pending`/`drain`/`applySplice`) are exercised via `InternalsVisibleTo("Ylmish.Tests")` (`src/Ylmish/AssemblyInfo.fs`) rather than widening the public surface.

### Step 4 — Codec v2: typed primitives, `text`, `atomic`, `custom`, keyed `map`

Sub-steps, each a green commit. Remember the **interim-compatibility rule**: the old materialize path stubs the new `Element` cases with a descriptive `failwith`; existing tests must stay green after every sub-step.

- **4a — `Value` sub-language.** Purely additive: `Value.Encoder`/`Value.Decoder`, the four primitives, `contramap`/`map`. Round-trip properties, including a domain type over `contramap`.
- **4b — `Element` v2.** Reshape the union: typed primitive payloads (U10), `Text`, `Atomic`, `Custom` cases. This is the sub-step that ripples — fix every exhaustive match, applying the interim rule in the old path. No new behaviour yet.
- **4c — registers, objects, lists over `Value`.** `Encode.value/bool/int/float/string`, `Encode.list`/`Decode.list` taking `Value.*`; the should-not-compile snippet recording that `Encode.list` cannot accept text/custom/object items (the L1 restriction is type-level, so it isn't a runtime test).
- **4d — `map`, `text`, `atomic`.** `Encode.map`/`Decode.map` (→ `HashMap`), `Encode.text`/`Decode.text` over `Ylmish.Text`, `Encode.atomic`/`Decode.atomic` (round-trips as the inner codec).
- **4e — `custom` + housekeeping.** `Encode.custom`/`Decode.custom` (element carries the binding; decode reads `Value`); error paths (`UnexpectedKind`) with path reporting; `Decode.ask`; re-enable or rewrite the two disabled roundtrip tests (#10/#12).

*Acceptance:* codec fully specified with zero Yjs runtime involvement (the `CustomElement` *type* references Fable.Yjs — that's the decided dependency posture); old suite still green via the interim rule.

*Check-in:* test count per sub-step; which exhaustive matches 4b touched and how; status of #10/#12 (fixed, rewritten, or still blocked — say which and why).

*Decisions & lessons (executed 2026-07-04):*

- 158 passing (+16), 3 pending. `src/Ylmish/Codec.fs` fully implemented; `tests/Ylmish.Tests/Codec.fs` covers 4a–4e.
- **4b's feared exhaustive-match ripple never happened.** Because 2b put v2 in its own `Ylmish.Codec` namespace, v1's `Element` union is never reshaped — v1 stays byte-identical until Step 7 deletes it. The interim-compatibility rule's only real casualty remains the `V1` alias in `Y.fs` from 2b.
- Internal architecture: `Encoded` keeps hold of the **adaptive views** (`aval`/`alist`/`amap`) so Step 5 can observe deltas, not snapshots. `Element` is the pure point-in-time tree decoding consumes; tests pipeline `Element.ofEncoded → Decode.runElement` (via `InternalsVisibleTo`), and the public `Decode.run` (against a live doc) stays unimplemented until Step 6, as designed. `Primitive` is `PString | PNumber of float | PBool` — numbers are JS numbers; `Value.Decode.int` checks integrality on the way out.
- `Encode.option`'s interim representation is a presence flag + an inner projection that must only be forced while Some (guarded in `Element.ofEncoded`); Step 5's Some-window primitive replaces it, as flagged at 2b.
- Decode error semantics pinned: the object CE is monadic (short-circuits); **traversals accumulate** (every bad list/map item reports, each with its `ArrayIndex`/`MapKey`); paths are innermost-first, pinned by a deep-composition test (`[ArrayIndex 0; ObjectKey "tags"; MapKey "id-1"; ObjectKey "todos"]`).
- `Decode.custom`'s unbox is **trust-based under Fable** (JS casts are unchecked) — a Value/decoder type drift surfaces downstream, not at decode; documented on the combinator rather than pretending a try/catch would catch it.
- The should-not-compile record for L1 lives as a comment block at the top of `tests/Ylmish.Tests/Codec.fs`.
- #10/#12: nothing to do — the two formerly-disabled v1 roundtrip tests were already re-enabled on master during the parallel Step 0/1 baseline work, and the v2 round-trip properties supersede them anyway (v1 dies at Step 7).
- **Adaptify.Core 1.3.7 reverted to 1.3.4** — see the Step 3 correction above: the package/tool version skew is deliberate (1.3.7's library code doesn't compile under Fable 5), and dependency changes require `rm -rf build` before trusting a green run.

### Step 5 — Binding layer, encode direction

Internal `Binding.attach doc encoded : IDisposable` — the eventual replacement for `materialize`, built **alongside** it (`withYlmish` keeps running on the old path until Step 7; nothing is deleted here). Sub-steps, each red-green:

- **5a** — registers + objects: proving nested records merge per-field with no flattening machinery (L8);
- **5b** — keyed maps: element-wise reconcile, nested text under items, and the identity headline from the reference harness — **a keyed item's nested text survives concurrent membership changes and stays with its item** (L1/L7);
- **5c** — value lists: diff-reconciled minimal splices;
- **5d** — text;
- **5e** — atomic;
- **5f** — custom dispatch through `BindContext`;
- **5g** — composition (a model using every kind at once).

*Tests first (behavioural contract, two-doc via the Step 2a harness where relevant):* lazy container creation in one transaction; adopt-never-replace (the U11 anti-test); untouched unknown keys; kind-drift structured error (L5); all writes transacted under the origin token (L4); O(delta) op counts via `doc.on("update")` for the keyed/list/text paths; **option transitions** — None→Some creates the backing type lazily, Some→None deletes the key, and the inner encoding stays live across the Some window (this requires the "Some-window" adaptive projection named at the 2b check-in — a new internal primitive, test it in isolation first).

*Acceptance:* the harness's differential runner passes on every sub-step's slice **where the old materialize path failed its calibration** — same schedules, new result; old path untouched and old suite green.

*Check-in:* test count per sub-step; the differential runner's before/after on the #83 calibration schedule; any place the binding had to deviate from the design section (say what and why).

*Decisions & lessons (executed 2026-07-04):*

- 174 passing (+16), 3 pending. `src/Ylmish/Binding.fs` (internal `Ylmish.Internal.Binding`, `attach : Y.Doc -> Encoded -> Attachment`) + `tests/Ylmish.Tests/Binding.fs`. Sub-steps 5a–5g were implemented as one increment rather than seven commits — a cadence deviation, surfaced per the ritual; the per-sub-step tests all exist.
- **Design amendment — root anchoring.** The flagship offline test failed as first written: the keyed-map *container* itself hit the U2a first-create race (each peer lazily created its own `Y.Map` under the root key; one peer's whole collection was discarded on sync). The app-minted-key rule protects items, not the collection's own first creation. Fix, derived from the same rule + U2b: **top-level structural containers anchor to named Yjs root types** (the schema's field names are the unique keys, applied by the library); top-level registers/options/atomics/customs stay in the argless root map. The residual U2a race narrows to fixed-path records nested inside other containers, on their first write — pinned and documented in the L8 test. Design sections updated; **Step 6's decode direction must read both namespaces** (argless root map + named root types).
- **Adaptive library quirk (empirically found):** reader-based `AddCallback` (alist/amap) fires *nothing* at registration when the collection is empty — so a naive skip-one-callback flag swallows the first real add. Fixed by pre-subscribing at-attach items and skipping one callback only when a non-empty registration delta exists. The aval-based `AddCallback` *does* always fire initially; the two need different handling.
- **The Some-window primitive** landed as: a hold-last-Some projection in `Encode.option` (an in-flight inner callback during the Some→None transition sees an unchanged value instead of a null-ish default — that crash was observed empirically) + subscribe-inner-only-while-Some in the binding. Transitions pinned: None→Some flushes the subtree at the transition; Some→None detaches then deletes the key.
- **Text intents:** consecutive model values share their pending-list tail structurally, so the binding extracts exactly the new splices (or falls back to one affix-diff splice on wholesale replacement, and to a full-content insert on fresh creation). Pinned by the delta-shape test (one retain+insert event — a splice, not a rewrite).
- Known nuance deferred to Step 6: local splice positions apply to a possibly-diverged Y.Text in the window between a remote apply and the model read-back — inherent to index-addressed text; the read-back loop closes the window. Similarly, reconciling the *model* with remote doc state is explicitly not the encode direction's job (the list test documents this).
- O(delta) pinned structurally (splice shapes, per-key map ops, index deltas, no-op re-stamp skip); the byte-size meter from 2a is reserved for Step 9's stress pass.
- The differential harness now passes on the exact Concurrent schedule the materialize path failed, and still matches under Immediate delivery — Step 2a's calibration red, flipped, with the materialize-still-fails test left in place until Step 7 deletes that path.

### Step 6 — Binding layer, decode direction

One `observeDeep` → decode → dispatch, custom elements riding the same observer.

*Tests first:* own-origin transactions ignored (no echo, no loop); one remote transaction spanning many types = exactly one `Set` (U14); remote text edits surface as intent-free `Text` values; remote custom edits surface through `Decode.custom` with no side channel (L6); decode failure invokes `OnError`, keeps the model, loop survives; the two imported crash regressions — a remote apply mid-flight never triggers a re-entrant local `Y.Text` write, and no adaptive mutation escapes `transact` (L4).

*Acceptance:* full bidirectional binding at the adaptive layer, still without Elmish.

*Check-in:* test count; a trace of one remote transaction → one `Set` (the U14 batching evidence); confirmation both imported crash regressions are covered by name.

### Step 7 — `withYlmish` v2, end-to-end

Rewire `Program.withYlmish` onto the binding layer; decode-empty-=-init on startup. **This is where the old world dies:** delete `materialize`/`dematerialize`, the interim `failwith` stubs from Step 4b, and the dead commented code in `Program.fs`.

*Tests:* un-skip Step 2b's north stars — they pass, including through the differential harness's `withYlmish` bridge; the existing `Program.fs` suite (fixing the known wrong assertions, e.g. `PropC`/`PropD`) passes rewritten against the binding path; `TodoCollaborative` gains the concurrent-edit cases; the **compatibility test** proving the dual-key migration recipe with plain combinators — a v1-schema program and a v2-schema program share a doc, edit concurrently, and neither destroys the other's representation.

*Acceptance:* issue #83 closed by tests, not by claim; zero references to `materialize` remain.

*Check-in:* test count; the north stars' names now green; what the old-path deletion removed (line count is a feature here); any `Program.fs` test whose assertion you had to change and the reasoning.

### Step 8 — `CustomElement` escape hatch, proven end-to-end

Prove the hatch under `withYlmish` (the types exist since 2b/4e; the dispatch since 5f; the readback since 6 — this step is consumer-side proof, not new runtime machinery).

*Tests first:* a consumer-authored grow-only counter **sums** concurrent increments across two `withYlmish` peers, converging in both Elmish models; the editor scenario — `GetText` hands out the live `Y.Text`, external edits to it flow into the model's decoded field; a custom binding cannot double-integrate its Y type (U5 guarded by `BindContext`).

*Acceptance:* the hatch is sufficient to build a counter *without touching Ylmish internals or Adaptive* — the escape-hatch and encapsulation claims proven together.

*Check-in:* test count; the counter's full source (its size and its `open` list are the measure of the hatch's ergonomics — bring both).

### Step 9 — Property-based stress

Random two-replica add/remove/edit schedules over the keyed-map + text + register model, under Immediate and Concurrent delivery, replayed deterministically through the Step 2a harness. Invariants, per schedule: the replicas **converge** to equal models, the converged state **matches the raw-Yjs oracle**, and nothing the oracle preserves was lost (L2).

*Acceptance:* ~100 schedules per policy, deterministic seeds, green in CI.

*Check-in:* test count; runtime cost of the stress suite (if it dominates CI, propose a split: small always-on set + larger nightly/manual set — that's a check-in decision, not yours alone).

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

*Check-in:* the transcript itself — read it as a stranger; each act should be intelligible without having read this plan.

### Step 11 — Docs rewrite

- **README:** keep the philosophy sections; replace the TODO list with: a 60-second quickstart mirroring the demo; the **taxonomy table** ("the model's type is the merge choice") doubling as the merge-semantics table, with the honest limits (LWW tiebreak deterministic-not-temporal; the offline first-create rule — *anything creatable offline needs a unique key*; delete-beats-edit; structural-move duplication); the layer map with the public/internal line and the dependency posture (Yjs/Elmish public, Adaptive shrinking); the demo transcript.
- **doc/guides/**: `codec.md` (schema decoupling, `Decode.ask`, app-only state), `text.md` (Text semantics, `edit` ambiguity), `custom-elements.md` (writing a binding; the counter; wiring an editor to `GetText`), `recipes.md` (dual-key rolling migration; fractional-index ordering over `Encode.map`; modelling offline-creatable entities with app-minted keys).
- **AGENTS.md**: update layout/testing sections (the harness becomes a documented testing tool); mark plans 0001 and 0002 statuses.

*Acceptance:* every code sample in the docs is compiled — concretely: each sample is a verbatim excerpt of code that lives in `examples/` or `tests/` (marked with a comment naming the doc that quotes it), so CI compiles them by construction; no free-floating fenced code that can rot.

*Check-in:* final test count vs the Step 0 baseline; the README read top-to-bottom; the list of plan Open questions that remain open (they carry forward, not silently expire).

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
