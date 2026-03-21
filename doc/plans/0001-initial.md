# 0001 — Initial Plan: Making Ylmish Functional

## Current State

Ylmish is an F# library for building real-time, collaborative applications by bridging [Elmish](https://github.com/elmish/elmish) (functional MVU framework) with [Yjs](https://github.com/yjs/yjs) (CRDT-based state synchronization), using [FSharp.Data.Adaptive](https://github.com/fsprojects/FSharp.Data.Adaptive) as an incremental computing middle layer and [Fable](https://github.com/fable-compiler/fable) as the F#-to-JavaScript compiler.

The core idea: successive Elmish models update an intermediate Adaptive model, whose deltas are observed and applied to a Yjs document—and vice versa—so that app state and shared state stay in sync.

```
┌─Elmish─┐         ┌─Adaptive─────────┐         ┌─Yjs───┐
│        │ --[1]-> │                  │ --[2]-> │       │
│ Model  │         │ IncrementalModel │         │ Y.Doc │
│        │ <-[4]-- │                  | <-[3]-- │       │
└────────┘         └──────────────────┘         └───────┘
```

### What exists and works

#### Fable.Yjs bindings (`src/Fable.Yjs/`)

Complete F# type bindings for the Yjs JavaScript library, generated via ts2fable and hand-tuned. These cover the full Yjs API surface: `Y.Doc`, `Y.Text`, `Y.Map`, `Y.Array`, `Y.XmlElement`, transactions, snapshots, encoding/decoding, undo management, and more.

- `Yjs.fs` (2,232 lines) — All Yjs types and a convenience `Y` module with F# helpers for `Doc.Create`, `Text.Create`, `Array.Create`, `Map.Create`, delta types, etc.
- `Lib0.fs` (3,356 lines) — Bindings for the lib0 utility library (encoding/decoding). Mostly commented-out; only the types actually used by Yjs remain active.

Evidence: The Yjs bindings are actively used by all passing tests (Y.Delta, Y.Text) which create `Y.Doc` instances, call `getText`, `getMap`, `insert`, `delete`, `observe`, `transact`, etc.

#### Adaptive Codec (`src/Ylmish/Adaptive.Codec.fs`)

A complete encoder/decoder system for translating between Adaptive models and a generic `Element<'Value>` representation (which maps onto Yjs types). This includes:

- **Element types**: `Element.Value`, `Element.AList`, `Element.AMap` — an intermediate representation bridging Adaptive and Yjs worlds.
- **Encoders**: `Encode.object`, `Encode.value`, `Encode.option`, `Encode.list`, `Encode.map` — for transforming Adaptive model fields into the Element tree.
- **Decoders**: A monadic `Decode.object` builder with `required`, `optional`, `key`, `value`, `tryParse`, and `list` combinators for transforming Element trees back into Elmish models.
- **Error handling**: Structured errors with path tracking (`UnexpectedKind`, `UnexpectedType`, `MissingProperty`).
- **Validation**: Applicative validation for accumulating multiple decode errors.

Evidence: `tests/Ylmish.Tests/Adaptive.Codec.fs` tests encode/decode with `Example.Codec.Things.encode` and `Example.Codec.Things.decode`. The `basic updates work` and `Decoded_traversei` tests pass.

#### Y.Text bi-directional sync (`src/Ylmish/Y.fs`, `Text` module)

Working bi-directional synchronization between `char clist` (Adaptive) and `Y.Text` (Yjs):

- `Text.ofAdaptive` — Creates a `Y.Text` from an Adaptive `clist<char>` and sets up bi-directional sync via `attach`.
- `Text.toAdaptive` — Creates a `clist<char>` from an existing `Y.Text` and sets up bi-directional sync.
- `Text.attach` — Private function that observes Y.Text events (applying deltas to the clist) and Adaptive callbacks (applying deltas to Y.Text), with a reentrancy guard flag.

Evidence: All 7 tests in `tests/Ylmish.Tests/Y.Text.fs` exercise this and pass — initialisation, insertion from Adaptive side, insertion from Yjs side, and multi-step edits.

#### Delta operations (`src/Ylmish/Y.fs`, `Delta` module)

Generic delta application functions:

- `Delta.applyYDelta` — Applies Yjs deltas (insert/retain/delete) to an Adaptive `clist`.
- `Delta.applyAdaptiveDelta` — Applies `IndexListDelta` operations to a Yjs type, batching contiguous inserts and deletes into single Yjs operations.

Evidence: All 19 tests in `tests/Ylmish.Tests/Y.Delta.fs` pass — covering insert, retain, delete, and combined operations in both directions.

#### Y.Array sync (`src/Ylmish/Y.fs`, `Array` module)

Bi-directional synchronization for arrays of `Element option`, similar to Text but for nested elements:

- `Array.toAdaptive` and `Array.ofAdaptive` with an `attach` function.
- `Element.toAdaptive` and `Element.ofAdaptive` for recursive conversion between `Y.Element` and `A.Element`.

Note: Currently only handles `Element.Array` / `Y.Element.Array` — no `Map`, `String`, or `Value` cases yet.

#### Elmish integration shell (`src/Ylmish/Program.fs`)

A `Program.withYlmish` function that wraps an Elmish program with Ylmish message routing:

- Defines a `YlmishOptions` record with `Create`, `Update`, `Encode`, `Decode`, and `Doc` fields.
- Wraps the Elmish `update` to route `User` messages (from the app) and `Set` messages (from Yjs).
- Maps `init`, `setState`, `view`, and `subs` to inject the Ylmish layer.

**However**, the actual connection between the Adaptive model and the Y.Doc is not implemented. The lines that would create the Adaptive model, encode it, and attach it to the Y.Doc are commented out (`//do amodel <- options.Create m`, `//let asdf = options.Encode amodel`, etc.).

Evidence: `src/Ylmish/Program.fs` lines 57–91 — the `withYlmish` function's `init`, `update` blocks have commented-out Adaptive/Yjs bridging logic. The `tests/Ylmish.Tests/Program.fs` tests (13 tests covering initial persistence, restoration, updates, optional values, nested objects, lists) are **not included** in the test runner (`Ylmish.Tests.fs` only lists `Adaptive.Codec.tests`, `Y.Delta.tests`, `Y.Text.tests`—not `Program.tests`).

#### Test infrastructure

- Tests compile via Fable to JavaScript and run with Mocha (for the browser/Node.js Yjs runtime).
- Adaptify generates adaptive model code (`Example.g.fs`) as a build step.
- A GitHub Actions CI workflow runs on push/PR to master.
- Property-based testing via Hedgehog is used for codec tests.

### What is broken or incomplete

#### 1. `Program.withYlmish` does not connect Adaptive to Yjs

The central feature—the Elmish–Adaptive–Yjs bridge—is stubbed out. The `withYlmish` function only does message routing but never:
- Creates the Adaptive model from the Elmish model
- Encodes the Adaptive model to the Element tree
- Attaches/syncs the Element tree with the Y.Doc
- Observes Y.Doc changes and decodes them back to set the Elmish model

Evidence: `src/Ylmish/Program.fs` lines 63–64, 77–79 are commented out. The README TODO item 3 states: "Implement the actual attaching of the adaptive model to the Y.Doc in Program.withYlmish so that the tests in Program.withYlmish pass."

#### 2. Program tests are excluded from the test suite

`tests/Ylmish.Tests/Ylmish.Tests.fs` only registers three test modules:
```fsharp
let tests = [
   Adaptive.Codec.tests
   Y.Delta.tests
   Y.Text.tests
]
```
The `Ylmish.Program.tests` module exists but is not listed. These tests describe the desired end-to-end behavior and would serve as acceptance tests for the complete integration.

#### 3. No Element↔Yjs bridge for maps and values

`src/Ylmish/Y.fs` has `Element.toAdaptive` and `Element.ofAdaptive` that only handle `Element.Array` / `Y.Element.Array`. The `Map` module has placeholder `failwith "not implemented"` / `failwith "not impl"` for both directions. There is no handling of `Element.Value` / `Y.Element.String` in the conversion.

Evidence: `src/Ylmish/Y.fs` lines 327–331 (`Map.toAdaptive`, `Map.ofAdaptive`) and lines 334–340 (`Element.toAdaptive`, `Element.ofAdaptive` match only one case each).

#### 4. Codec roundtrip tests are disabled

Two test cases in `tests/Ylmish.Tests/Adaptive.Codec.fs` are commented out due to a Fable compiler issue ([fable-compiler/Fable#3328](https://github.com/fable-compiler/Fable/issues/3328), tracked as [Ylmish#10](https://github.com/primacydotco/Ylmish/issues/10)). These tests verify that encoding an Adaptive model and decoding it back produces the original model—a fundamental correctness property.

#### 5. No connection from encoded Element tree to Y.Doc

Even though the Adaptive Codec can produce `Encoded<Element<string>>` from an Adaptive model, there is no function that takes this Element tree and materializes it in a `Y.Doc` (writing `Element.Value` as Y.Map entries, `Element.AList` as Y.Array, `Element.AMap` as Y.Map). And there is no function reading a Y.Doc's root map into an `Element` tree for decoding.

This is the missing "last mile"—the Codec speaks the Element language, the Yjs bindings speak the Yjs type language, and nothing translates between them at the top level.

#### 6. Schema evolution is not addressed

The README describes the need to decouple app schema from state schema and support schema versioning. Currently, `Text.attach` sets up bi-directional sync as a single unit, meaning encoding and decoding cannot use different schemas. The README TODO item 2 calls for separating sync directions.

#### 7. Decoder lacks access to the current Elmish model

When decoding state data back into app data, non-persisted fields need to be preserved from the current model. The `Decoder` type is `Path * 'Element -> Decoded<'Result>`, and the README TODO item 4 suggests the decoder should have access to the current Elmish model via a Reader monad (`ask` function in `Decode.object` builder).

#### 8. Dependency versions are old

- .NET SDK 6.0.100 (.NET 6 reached end of support in November 2024)
- Fable.Core 4.0.0-theta-007 (pre-release)
- Yjs 13.5.35 (current is 13.6.x)
- Mocha 10.2.0, various other packages at older versions

#### 9. Some Program.fs test assertions look incorrect

In `tests/Ylmish.Tests/Program.fs`, the "withYlmish persists initial list of values" test (line 382–383) asserts against `dispatcher.Model.PropC[0].Prop0` and `root.get("propC")` but the model was set up with `PropD`, not `PropC`. The "withYlmish persists initial object" test (line 426) checks `root.get("propA")` but the encoded key is `"propE"`. These may be copy-paste errors or reflect an evolving design.

---

## High-Level Objectives

The goal is to make Ylmish functional enough that a developer can build a real collaborative application using the Elmish programming model with automatic Yjs synchronization. The following objectives are ordered by dependency (each builds on the previous).

### Objective 1: Complete the Element↔Yjs bridge

Implement the missing conversions in `src/Ylmish/Y.fs`:

- **`Map.toAdaptive`**: Convert a `Y.Map<Y.Element>` to an `amap<string, A.Element>` with bi-directional observation/sync.
- **`Map.ofAdaptive`**: Convert an `amap<string, A.Element>` to a `Y.Map<Y.Element>`.
- **`Element.toAdaptive`** / **`Element.ofAdaptive`**: Handle all cases (`Value`/`String`, `Map`, `Array`) instead of only `Array`.

This is the foundation for connecting the Codec's Element trees to live Yjs documents.

### Objective 2: Build the Encoded→Y.Doc and Y.Doc→Encoded materialization

Create functions that:

- Take an `Encoded<Element<string>>` (the output of encoding an Adaptive model) and write it into a `Y.Doc`'s root map, creating `Y.Map`, `Y.Array`, and primitive entries as needed.
- Read a `Y.Doc`'s root map into an `Element<string>` tree suitable for decoding.

This bridges the Adaptive Codec layer with the Yjs document layer.

### Objective 3: Implement `Program.withYlmish` end-to-end

Wire up the full Elmish→Adaptive→Yjs→Adaptive→Elmish loop in `Program.withYlmish`:

1. **On init**: Create the Adaptive model from the initial Elmish model. Encode it. Check if the Y.Doc already has state data. If so, decode it and dispatch a `Set` message. If not, materialize the encoded data in the Y.Doc.
2. **On Elmish update**: Update the Adaptive model with the new Elmish model. Observe the incremental deltas and apply them to the Y.Doc.
3. **On Y.Doc change**: Observe Y.Doc events, decode the updated state data, and dispatch a `Set` message to update the Elmish model.
4. **Reentrancy prevention**: Ensure that changes originating from Y.Doc→Elmish don't loop back to Y.Doc, and vice versa.

Enable the Program tests in `Ylmish.Tests.fs` and fix the incorrect assertions to serve as acceptance criteria.

### Objective 4: Fix known test issues

- Investigate and fix (or properly skip) the codec roundtrip tests blocked by Fable#3328.
- Fix the test assertions in `Program.fs` that reference wrong property keys.
- Address the IndexList 1-based indexing issue noted in the README (TODO item 7) if it causes delta failures.

### Objective 5: Give decoders access to the current model

Extend the `Decoder` type so that the `Decode.object` builder can access the current Elmish model (via an `ask` combinator or by threading the model as part of the Reader environment). This lets developers write decoders that merge persisted state data with non-persisted app data.

### Objective 6: Separate sync directions for schema evolution

Split `Text.attach` (and the future `Map.attach`, `Array.attach`) into one-way sync functions so that the encode path and decode path can use different schemas. This is needed for handling schema migrations when the app's model shape changes.

### Objective 7: Update dependencies and toolchain

- Upgrade .NET SDK to a currently supported LTS version.
- Upgrade Fable.Core from pre-release theta to a stable release (or the latest pre-release compatible with the current Fable compiler).
- Upgrade Yjs to the latest 13.x.
- Upgrade Mocha, concurrently, and other dev dependencies.
- Evaluate Fable 4 compatibility and resolve any breaking changes.

### Objective 8: Provide a sample application

Create a minimal but complete example application demonstrating:

- An Elmish model with a few properties (string, list, nested object).
- Encode/Decode codec definitions.
- `Program.withYlmish` integration.
- Two Y.Doc instances syncing via `applyUpdate`/`encodeStateAsUpdate` to demonstrate collaboration.

This would live in an `examples/` directory and serve as both documentation and a smoke test.
