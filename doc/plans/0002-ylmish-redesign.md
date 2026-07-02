# 0002 — Redesigning Ylmish around real CRDT merging

Fixes [#83](https://github.com/NickDarvey/Ylmish/issues/83).

## Motivation

`Program.withYlmish` today synchronizes by **materializing the whole encoded state tree** into the Y.Doc on every Elmish update, and reading it back wholesale on every Y.Doc change. Consequences (all reproduced empirically, see [Validated assumptions](#validated-assumptions-about-yjs)):

- String fields are atomic map values, so concurrent edits to the same field resolve last-writer-wins instead of merging. The canonical collaborative use case — two people typing in the same text body — silently loses one side's work.
- Every update replaces nested `Y.Array`/`Y.Map`/`Y.Text` instances wholesale, which discards *any* concurrent remote edits inside them, not just text.
- `materialize` deletes root keys it doesn't recognize, actively destroying data written by other clients or other schema versions — the exact opposite of what schema migration needs.
- Re-materializing is O(state) per update rather than O(delta).

Meanwhile the delta-level machinery that yields true CRDT convergence (`Text.attach`, `Array.attach`, `Map.attach` in `src/Ylmish/Y.fs`) already exists and passes its unit tests — it is simply unreachable from the codec and `withYlmish`.

This plan replaces the materialize path with a **live binding**: the codec describes *which* shared type backs each field, and the runtime keeps the adaptive model and those shared types in sync by exchanging deltas, in both directions, forever.

## Design inputs

Decisions taken with Nick (2026-07-02):

| Question | Decision |
| --- | --- |
| How does collaborative text appear in the consumer's model? | **A dedicated `Ylmish.Text` type** that carries edit intent, not a plain diffed `string`. |
| Which layers are public? | **Elmish-first.** `Program.withYlmish` + the codec are the supported surface. The adaptive↔Y attach primitives stay internal so they can change freely. |
| Who seeds a fresh document? | **Decode-empty = init.** The library never writes structure eagerly. An empty/partial doc decodes through the consumer's decoder (which has access to the current model); containers are created lazily by the first client that *edits* them, atomically. No seeding race by construction. |
| Packaging | **One `Ylmish` package** (plus `Fable.Yjs`); opinionated extras are separate namespaces you must explicitly `open`. |
| Per-field merge menu | **LWW registers, mergeable Text, and mergeable lists (Y.Array insert/delete merge)** — the three merge behaviours Yjs gives us natively. Richer strategies (counters, timestamped LWW, custom reduces) deferred until a consumer needs them. The demo must exercise list merging, not just text. |
| List semantics | **`Encode.list` → Y.Array with documented limits** (concurrent reorder duplicates; delete beats concurrent edit-inside). Additionally ship **`Encode.map` as the keyed primitive** (items in a Y.Map by consumer-supplied stable key) so consumers who have keys can build ordering themselves — e.g. fractional indexing as an order field. Ylmish provides the primitive, not the opinionated ordering machinery. |
| Migrations | **Defer entirely.** No Migrations module yet. The codec must keep migrations *expressible* (decoders compose, encoders combine, the runtime preserves unknown keys) and the docs show the dual-key recipe, but nothing ships until a real consumer validates the need. |
| Demo form | **CLI, as today.** A Node script whose output demonstrates the system under interesting concurrency: offline divergence, concurrent text edits interleaving, concurrent list inserts merging, LWW clobber shown honestly, reconvergence after sync. |

## Validated assumptions about Yjs

Runnable scripts: [`0002-assumptions/`](./0002-assumptions/) (`node experiments.mjs`, yjs 13.6.x). Step 1 of the plan pins these as characterization tests in CI.

| # | Unknown | Result | Design consequence |
| --- | --- | --- | --- |
| U1 | What does argless `doc.getMap()` return? | The root map named `''`; same instance every call. | Root binding is stable; no need to force consumers to name a root map. |
| U2a | Two clients each create a nested type under the same map key, then sync. | One client's **entire subtree wins**; the other's data is silently discarded. | Never create containers eagerly at init. Containers are created only on first local edit; decode of an empty doc yields the consumer's init state. |
| U2b | Same race with *root-level* types (`doc.getArray "todos"`). | Merges by name; both sides' items survive. | Root types are safe to get-or-create. |
| U3 | Concurrent edits to one shared nested `Y.Text`. | Interleave and converge. | The whole point. Text must bind to an existing instance, created once. |
| U3b | Concurrent `map.set` of a plain string. | LWW clobber — issue #83's failure mode. | Plain values are honest LWW registers; the codec must let consumers choose text vs register per field. |
| U4 | What decides the LWW winner? | Deterministic, order-independent: **higher clientID wins** among concurrent sets. Not wall-clock. | Document honestly: "last writer" is an arbitrary-but-convergent tiebreak. Timestamped LWW would be an opt-in module. |
| U5 | Re-set an already-integrated Y type instance under another key. | **No exception at the call site; the doc corrupts** — later syncs throw and content is lost. | The public API must make re-parenting impossible by construction. The binding layer binds each model field to exactly one shared-type instance and never reuses instances. |
| U6 | Do transaction origins propagate? | Yes: `doc.transact(fn, origin)` origins and `applyUpdate`'s origin both reach observers, with `local` flag. | Echo suppression via a per-binding origin token, replacing the current boolean-flag reentrancy guards. |
| U7 | Does `observeDeep` on the root see nested edits? | Yes, typed events with paths (`["list", 0]`) and per-type deltas. | The decode direction can route remote deltas precisely. |
| U8 | Concurrent Y.Array inserts at the same position. | Both survive, deterministic order. | Lists merge; safe default. |
| U9 | Delete an item vs concurrent edit inside it. | Delete wins; the edit is lost. | Documented list semantics. |
| U10 | What primitives can Y.Map hold? | Any JSON value (number, bool, null, string, plain objects/arrays). | The codec's `Element<string>` (strings only) is an unnecessary restriction; v2 elements carry typed primitives. |
| U11 | Replace a nested Y.Text with a fresh one while a peer edits the old one. | Replacement wins; the peer's edits are lost. | Precisely why materialize-per-update can never merge. Bind, never replace. |
| U13 | Concurrent "move" (delete+insert) of the same list item. | The item is **duplicated**. | Known Yjs v13 limitation; documented list semantics (keyed encoding as a possible later module). |
| U14 | One `doc.transact` spanning many nested types. | One `observeDeep` batch containing all events. | Remote changes apply as a single Elmish `Set` per transaction — atomic model updates. |
| U15 | A client applies updates containing keys it doesn't understand. | Applied cleanly; unknown keys preserved and readable. | Forward compatibility is free *if we stop deleting unknown keys*. Foundation for schema migrations (the dual-key recipe). |

## Design

### Principles

1. **Layered, opt-in, no all-or-nothing.** Core stays small; opinions (future merge strategies, migration helpers) live in separate namespaces the consumer must `open`.
2. **The codec is where merge behaviour is chosen.** A field's encoding *is* its merge policy: `Encode.text` means "concurrent edits interleave", `Encode.value` means "LWW register", `Encode.list` means "insert/delete merge". Nothing implicit.
3. **Bind, never replace.** After init, the runtime only ever applies deltas to existing shared types. It never re-creates a container, never re-parents an instance (U5, U11).
4. **Leave what you don't own.** The runtime never deletes keys it didn't encode (U15). Other clients and other schema versions co-exist by default.
5. **Honest semantics, documented.** LWW tiebreak is deterministic-not-temporal (U4); reorders can duplicate (U13); delete beats edit-inside (U9). These go in the README, not in a footnote.

### Layer map

```
public   ┌───────────────────────────────────────────────────────┐
         │ Ylmish.Program.withYlmish        Elmish integration   │
         │ Ylmish.Codec (Encode/Decode)     schema + merge policy│
         │ Ylmish.Text                      mergeable text value │
         ├───────────────────────────────────────────────────────┤
internal │ Ylmish.Internal.Binding          encoded tree ↔ Y.Doc │
         │ Ylmish.Internal.Y                attach/delta plumbing│
         │ Fable.Yjs                        bindings (own pkg)   │
         └───────────────────────────────────────────────────────┘
```

`Fable.Yjs` remains its own package (it is useful standalone), but Ylmish's contract is the top box only. Everything under `Ylmish.Internal` may change without notice.

### The consumer experience

The target end-to-end feel, for a collaborative todo app:

```fsharp
open Ylmish

[<ModelType>]
type Todo = {
    Title : Text          // collaborative: concurrent edits merge
    Done  : bool          // register: last-writer-wins
}

[<ModelType>]
type Model = {
    Todos  : Todo IndexList
    Filter : Filter        // app-only: not encoded, never persisted
}

module Codec =
    open Ylmish.Codec

    let encodeTodo (t : AdaptiveTodo) = Encode.object [
        "title", Encode.text t.Title
        "done",  Encode.bool t.Done
    ]

    let decodeTodo = Decode.object {
        let! title = Decode.object.required "title" Decode.text
        let! done_ = Decode.object.optional "done" Decode.bool
        return { Title = title; Done = defaultArg done_ false }
    }

    let encode (m : AdaptiveModel) = Encode.object [
        "todos", Encode.list encodeTodo m.Todos
    ]

    let decode = Decode.object {
        let! model = Decode.ask                    // current model, for app-only fields
        let! todos = Decode.object.optional "todos" (Decode.list.required decodeTodo)
        return { model with Todos = defaultArg todos IndexList.empty }
    }

Program.mkProgram init update view
|> Ylmish.Program.withYlmish {
    Doc    = doc
    Create = AdaptiveModel.Create      // Adaptify-generated
    Update = AdaptiveModel.Update
    Encode = Codec.encode
    Decode = Codec.decode
    OnError = Ylmish.Program.OnError.log
}
|> Program.run
```

Notes on ergonomics:

- The model stays a plain immutable record; the only Ylmish type that appears in it is `Text`.
- `Decode.ask` (the Reader environment) is how app-only state survives remote updates: the decoder merges persisted fields into the *current* model.
- `Decode.object.optional` + defaults is how "decode-empty = init" falls out naturally: an empty doc decodes to the init state through the same decoder as any other doc state. No separate seeding path exists.
- Choosing merge behaviour per field is exactly one word: `Encode.text` vs `Encode.value`/`Encode.bool`/….

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
    // by common prefix/suffix diff (ambiguous for repeats; documented)
    val edit     : newValue:string -> Text -> Text
```

Semantics:

- **Equality and comparison are by content only.** Pending intent is transport, not identity; `Text.ofString "hi" = (Text.empty |> Text.insert 0 "hi")`. Views and tests stay simple.
- The runtime drains intents when flushing to the backing `Y.Text` and returns intent-free `Text` values on the way back in.
- `Text.edit` exists so a bog-standard Elmish `OnChange (fun s -> dispatch (SetTitle s))` still produces splice-level merges; consumers wiring a real editor (CodeMirror, Monaco) can bypass ambiguity with `insert`/`remove`/`replace`, or (later, out of scope here) bind the editor straight to the underlying Y.Text.

### The codec, v2

Two changes to `Ylmish.Adaptive.Codec` (which moves to `Ylmish.Codec`):

1. **Typed primitives.** `Element<'v>`'s value case becomes a proper primitive union (string/number/bool/null) mirroring what Y.Map actually stores (U10), so `Encode.bool`, `Encode.int`, `Encode.float` exist without stringly round-trips. `Encode.value : ('a -> Primitive) -> aval<'a> -> Encoded` remains the general register combinator.
2. **A `Text` element kind.** `Element` gains `Text`, routed by the binding layer to a `Y.Text`. `Encode.text : AdaptiveText -> Encoded` and `Decode.text : Decoder<_, _, Text>` are the only new user-facing combinators.

Everything else — `Encode.object/list/map/option`, the `Decode.object` builder, `required`/`optional`, `Decode.ask`, path-tracked validation errors — keeps its current shape. The codec grammar in full:

```
Encoded  ::= object [(key, Encoded)]        → Y.Map     (per-key LWW of subtrees)
           | list  encodeItem alist         → Y.Array   (insert/delete merge)
           | map   encodeValue amap         → Y.Map     (per-key merge; the keyed-collection primitive)
           | text  adaptiveText             → Y.Text    (splice merge)
           | value toPrimitive aval         → primitive (LWW register)
           | option …                       → presence/absence of any of the above
```

`Encode.map` is deliberately first-class, not an afterthought: when a consumer's
items have a stable key, a keyed Y.Map is the merge-friendly shape — concurrent
edits to *different* items never conflict, and ordering can be modelled by the
consumer as an explicit order field on each item (e.g. fractional indexing).
Ylmish provides the primitive; the ordering policy stays in consumer land (a
docs recipe shows the fractional-index pattern).

### The runtime (internal): binding instead of materializing

`Y.Doc.materialize`/`dematerialize` are deleted. In their place, an internal binding layer walks the encoded tree once and establishes live, bidirectional, delta-level bindings:

- **Encode direction.** Adaptive deltas (from Adaptify's `Update` after each Elmish update) flow to the bound Y types inside a single `doc.transact` tagged with this instance's **origin token** (U6, U14). Containers that don't exist yet in the doc are created at the moment the first local edit touches them, within that same transaction (U2a). Existing containers are always adopted, never replaced (U5, U11). Keys the encoder doesn't mention are never touched (U15).
- **Decode direction.** One `observeDeep` on the root; transactions carrying our own origin token are ignored (echo suppression); every other transaction produces exactly one decode → one Elmish `Set` dispatch (U7, U14). Decode failures go to `OnError` and leave the current model in place — a malformed or newer-versioned doc can't crash the loop.

The existing `Text/Array/Map.attach` delta plumbing in `Y.fs` is the substance of this layer; it gets an origin-based guard instead of boolean flags, direction-split as the old TODO already called for, and moves under `Ylmish.Internal`.

### Migrations: expressible, deferred

**No Migrations module ships in this plan.** What *is* in scope is keeping migrations expressible with nothing but the public codec surface, so the module (if a real consumer ever justifies it) is ~20 lines of sugar rather than a runtime privilege:

- Decoders compose, so "read old-or-new, prefer new" is an `oneOf`-shaped combinator over ordinary decoders.
- Encoders produce key sets that can be merged, so "write both shapes during rollout" is a fold over ordinary encoders.
- The load-bearing part is a **runtime rule**, and it is in scope: the binding layer never deletes keys it didn't encode (U15), so a v1 client and a v2 client can share a doc without destroying each other's representation.

The dual-key rolling-upgrade recipe (v2 renames `"todos"` to `"items"`; write both, read either, drop the old leg when v1 clients are gone) becomes a documentation recipe, exercised by a compatibility test in Step 7 — not an API.

## Plan of work

Each step is one PR-sized unit: it starts by writing failing (or characterization) tests, ends with `npm test` green, and leaves the library releasable. Later steps depend on earlier ones; nothing lands half-wired.

### Step 1 — Pin the assumptions

Port `doc/plans/0002-assumptions/*.mjs` to `tests/Ylmish.Tests/Y.Assumptions.fs` as characterization tests of Yjs itself (U2a/U2b, U3, U4, U5b, U6, U9, U11, U13, U14, U15 at minimum), via the existing Fable.Yjs bindings.

*Tests:* the table above, asserted. *Acceptance:* suite green; every design-consequence claim in this plan is enforced by CI, so a Yjs upgrade that changes semantics fails loudly.

### Step 2 — North-star acceptance test (red)

The issue #83 test, written against the *target* public API and marked `ptestCase` (skipped): two `withYlmish` programs on separate docs; both edit the same `Text` field concurrently; updates exchanged via `encodeStateAsUpdate`/`applyUpdate`; both models converge to the same interleaved string. Plus a sibling test for "unknown keys survive a full session".

*Acceptance:* compiles against the intended API surface (which forces the API sketch above to be written down as signatures), skipped in CI. Un-skipped in Step 7.

### Step 3 — `Ylmish.Text`

Pure value type, no Yjs dependency.

*Tests first:* content equality/hash laws; `insert`/`remove`/`replace` semantics incl. bounds; property — replaying any intent sequence over the old content equals the new content; property — `Text.edit` produces a single splice whose application equals the new string; Adaptify round-trip (`cval<Text>` works in a `[<ModelType>]`).

*Acceptance:* Text usable in a model today, even before sync exists (degrades to an ordinary value).

### Step 4 — Codec v2: typed primitives + Text element

*Tests first:* encode/decode round-trip properties for bool/int/float/string/null, `option`, nested objects/lists/maps, and `text`; error paths (`UnexpectedKind` when a register field meets a text decoder, etc.) with path reporting; `Decode.ask` merging app-only fields. Re-enable the two disabled roundtrip tests (#10/#12) or re-write them free of the Fable #3328 shape.

*Acceptance:* codec fully specified with zero Yjs involvement — the schema layer is independently testable, per the layering principle.

### Step 5 — Binding layer, encode direction

Internal `Binding.attach doc encoded : IDisposable`, replacing `materialize`. Sub-steps, each red-green: (a) registers + objects, (b) keyed maps, (c) lists, (d) text, (e) nested composition.

*Tests first (behavioural contract, two-doc where relevant):*
- first local edit creates the container lazily, in one transaction (U2a discipline);
- an existing container/Y.Text is adopted, never replaced — concurrent remote edits to it survive a local update (the U11 anti-test);
- keys not mentioned by the encoder are untouched (U15);
- update cost is O(delta): a 1-item change to a 1000-item list produces one Y op batch, asserted by counting `doc.on("update")` payload ops;
- all writes carry the instance's origin token.

*Acceptance:* `materialize` deleted; `Y.Doc` tests rewritten against `Binding`.

### Step 6 — Binding layer, decode direction

One `observeDeep` → route by event path → decode → dispatch.

*Tests first:* own-origin transactions are ignored (no echo, no infinite loop — the current sentinel TODO closed properly); one remote transaction spanning many types yields exactly one `Set` (U14); remote text edits surface as intent-free `Text` values; a decode failure invokes `OnError`, keeps the current model, and the loop stays alive.

*Acceptance:* full bidirectional binding at the adaptive layer, still without Elmish.

### Step 7 — `withYlmish` v2, end-to-end

Rewire `Program.withYlmish` onto the binding layer; decode-empty-=-init on startup; delete the dead commented code in `Program.fs`.

*Tests:* un-skip Step 2's north-star tests — they pass; the existing `Program.fs` test suite (fixing the known wrong assertions, e.g. `PropC`/`PropD`) passes; `TodoCollaborative` tests extended with the concurrent-edit case from issue #83's acceptance criteria; a **compatibility test** proving the dual-key migration recipe with plain combinators — a v1-schema program and a v2-schema program share a doc, edit concurrently, and neither destroys the other's representation.

*Acceptance:* issue #83 closed by tests, not by claim.

### Step 8 — The demo

`examples/TodoCollaborative` stays a **CLI (Node) program**, rebuilt as a scripted narrative of the system under interesting concurrency. Two `withYlmish` programs on independent `Y.Doc`s, sync performed explicitly between acts, output printed per act:

1. Two peers start; neither seeds; both show init state (decode-empty = init, visibly).
2. Offline: both edit the same note concurrently → sync → the interleaved merged text, side by side with what each peer typed.
3. Offline: both insert todos concurrently → sync → both items survive in a deterministic order (list merge — required by the merge-menu decision).
4. Both flip the same LWW register concurrently → sync → one deterministic winner, shown honestly as a clobber.
5. One peer deletes an item while the other edits inside it → sync → delete wins.
6. An app-only field changes throughout and never appears in either doc.

*Acceptance:* the demo exercises only the public API; `npm run demo` output reads as documentation, and the transcript is embedded in the README so a reader can falsify the library's claims in under a minute.

### Step 9 — Docs rewrite

- **README:** keep the philosophy sections (they're good), replace the TODO list with: a 60-second quickstart mirroring the demo; a **merge semantics table** (field encoding → what happens on concurrent edits, including the honest limits: LWW tiebreak is deterministic-not-temporal, reorder duplication, delete-beats-edit-inside); the layer map with the public/internal line drawn explicitly; the demo transcript.
- **doc/guides/**: `codec.md` (schema decoupling, `Decode.ask`, app-only state), `text.md` (Text semantics, `edit` ambiguity, wiring editors), `recipes.md` (dual-key rolling migration with plain combinators; fractional-index ordering over `Encode.map`).
- **AGENTS.md**: update layout/testing sections; mark plans 0001 (superseded where applicable) and 0002 statuses.

*Acceptance:* every code sample in the docs is compiled (samples live in the demo or a doc-tests project, not in fenced blocks that rot).

## Open questions

Interface/behaviour details surfaced by the design — none blocking Steps 1–4:

1. **`OnError` shape:** is "log and keep current model" the right decode-failure policy, or should consumers get the raw errors + the offending doc state to decide (e.g. surface "this doc was written by a newer client" UX)?
2. **`Text` equality:** equality by content only (intent excluded) — acceptable? It makes `=`/memoization intuitive but means "has pending edits" is not observable via equality.
3. **`Text.edit` ambiguity:** for repeated characters the single-splice diff can pick a different position than the user's actual edit (convergence unaffected; interleaving fidelity slightly coarser). Fine for the plain-`<input>` path given precise `insert`/`remove` exist?
4. **Presence/awareness** (cursors, who's-online): explicitly out of scope for this plan, or wanted as a future optional module worth leaving API room for?
5. **Undo:** `Y.UndoManager` integrates naturally with origin tokens (undo only your own origin's ops). Out of scope here, but should the binding layer's origin design reserve room for it (I propose: yes, it costs nothing)?

## Out of scope

- Ycs/Yrs (.NET-native) backends — the old README TODO stands, unaffected.
- Rich-text attributes/embeds in `Text` (Y.Text formatting) — plain text only for now.
- Async resolution of decoded references (README TODO item on author IDs) — remains an app-layer concern via `Cmd`.
- Provider integrations (y-websocket, y-indexeddb persistence) — the demo may use raw `applyUpdate` wiring; providers are the consumer's choice.
