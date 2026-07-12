# Agent Guide — Ylmish

## What this project is

Ylmish bridges **Elmish** (F# MVU) ↔ **FSharp.Data.Adaptive** (incremental) ↔ **Yjs** (CRDT sync), compiled to JavaScript via **Fable**. See `README.md` for design (quickstart, merge-semantics taxonomy, layer map) and `doc/guides/` for consumer guides.

Plan statuses: `doc/plans/0002-ylmish-redesign.md` is the **executed** redesign the current library implements (Steps 0–11 all landed; its *Validated assumptions* table pins Yjs semantics and its per-step *Decisions & lessons* record why things are the way they are). `doc/plans/0001-making-ylmish-functional.md` is **superseded** by 0002.

## Build & test

```bash
npm install          # restore .NET + npm dependencies
npm test             # adaptify codegen → Fable compile → Mocha tests
npm run test+watch   # watch mode (Fable + Adaptify)
```

Tests **must** run through Fable/Mocha (JavaScript). `dotnet test` will not work because the tests depend on the Yjs runtime.

Gotchas:

- Fresh environments ship without a .NET SDK; install the version `global.json` pins (via `dotnet-install.sh`) before `npm install`.
- After changing **dependencies** (package versions, project references), `rm -rf build` — Fable's incremental cache can go stale and produce greens/reds that lie.
- The Adaptify **CLI** (`dotnet tool`, 1.3.7) and the **Adaptify.Core package** (1.3.4, `Directory.Packages.props`) are deliberately skewed: 1.3.7's library does not compile under Fable 5. Don't "align" them.
- `npm run demo` builds and runs `examples/TodoCollaborative/Demo.fs` — a deterministic nine-step narrative whose transcript is embedded in the README.
- Doc code samples are **single-sourced**: regions of compiled code marked `// sample:begin <name>` … `// sample:end <name>` are injected into the fenced blocks tagged `<!-- sample: <name> -->` in `README.md`/`doc/guides/` (the demo transcript likewise, via `<!-- output: demo -->`). After changing a marked region or demo output, run `npm run docs`; CI fails on drift (`npm run docs:check`). Never edit the tagged blocks by hand.

## Repository layout

```
src/Fable.Yjs/       F# bindings for Yjs (generated + hand-tuned)
src/Ylmish/          Core library:
  Text.fs              Ylmish.Text — mergeable text value (public)
  Codec.fs             Ylmish.Codec — Encode/Decode, Value sub-language,
                       CustomElement escape hatch (public)
  Delta.fs             Ylmish.Internal.Delta — list-delta application (internal)
  Binding.fs           Ylmish.Internal.Binding — encoded tree ↔ Y.Doc (internal)
  Program.fs           Ylmish.Program.withYlmish — Elmish integration (public)
examples/TodoCollaborative/  The demo app; marked sample regions of it are
                             injected into the README and doc/guides by
                             doc/sync-samples.mjs (npm run docs)
tests/Ylmish.Tests/  Fable.Mocha tests (compiled to JS and run with Mocha)
  common/            Shared test helpers: Example model, Elmish test dispatcher
  Hedgehog.fs        In-repo, Fable-compatible property-testing shim (NOT the
                     NuGet package — same API surface, fixed-seed PRNG)
  Harness.fs         The differential harness (see Testing philosophy)
  Stress.fs          Property stress: random schedules through the harness
doc/guides/          Consumer guides: codec, text, custom-elements, recipes
doc/plans/           Design plans (0002 = the executed redesign)
.skills/             Agent skill definitions
```

## Key conventions

- **F# with Fable**: all source is `.fs`. Fable attributes (`[<Import>]`, `[<Emit>]`) are used for JS interop.
- **Adaptive types**: `cval`, `clist`, `cmap` are mutable; `aval`, `alist`, `amap` are observable. Use `transact` to batch mutations.
- **Adaptify codegen**: models decorated with `[<ModelType>]` get `Adaptive*` wrappers auto-generated into `*.g.fs` files. Don't edit `*.g.fs` by hand.
- **Central package management**: NuGet versions live in `Directory.Packages.props`. Don't put `Version=` in `.fsproj` files.
- **Lock files**: both `packages.lock.json` (NuGet) and `package-lock.json` (npm) are committed. Update them when changing dependencies.
- **CI**: GitHub Actions (`.github/workflows/build.yml`). Runs `npm install && npm test` on ubuntu-latest.

## Testing philosophy

Write **high-signal, robust tests** that catch real bugs without breaking on irrelevant changes.

### Principles

- **Test behaviour, not implementation.** Assert on observable outcomes (final state, output, returned values), not on internal call sequences or intermediate states.
- **Prefer property-based tests over example-based tests** where the domain has clear invariants. A single `Property.check` with a well-chosen invariant replaces dozens of hand-written examples and finds edge cases humans miss.
- **Keep tests deterministic.** Hedgehog uses seeds for reproducibility—never introduce `System.Random` or time-dependent assertions.
- **One assertion per concern.** If a test checks two unrelated things, split it. A failure message should tell you exactly what broke.
- **Avoid coupling tests to encodings or string formats** that may change. Test the round-trip property (`decode(encode(x)) = x`) rather than asserting on a specific encoded form.

### When to use Hedgehog (property-based testing)

Use `Property.check` with `property { let! ... }` when a test can be expressed as "for all valid inputs, this invariant holds." The `Hedgehog` namespace here is the in-repo shim (`tests/Ylmish.Tests/Hedgehog.fs`) — same API as the real library, fully deterministic (fixed-seed PRNG, no shrinking), Fable 5 compatible. Existing exemplars: the codec round-trip and error-path properties in `Codec.fs`, the reference-implementation and replay properties in `Text.fs`, and the schedule properties in `Stress.fs`.

### The differential harness (`Harness.fs`)

The standing tool for CRDT-semantics claims. Three pieces:

- A **`Bridge`** pushes changes into a `Y.Doc` (`Apply`, receiving both the op and the pure post-op model) and reads a converged model back (`Read`). The system under test is a full `withYlmish` program behind a bridge; the **oracle** (`RawYjs.factory`) interprets each op as one primitive Yjs operation — the intended semantics by construction, no self-authored spec to be wrong about.
- **`run`** replays a schedule (per-replica ops) under a delivery policy: `Immediate` (each change ships before the next op — no concurrency) or `Concurrent` (everything held, one exchange at the end — maximal concurrency). It pins the replicas' clientIDs, so every Yjs tiebreak is deterministic and a failing schedule replays identically.
- **`differential`** runs SUT and oracle through the same schedule and compares full converged models, reporting anything the oracle kept that the SUT lost.

`Stress.fs` drives ~100 random schedules per delivery policy through this machinery; extend the harness's op alphabet (and the oracle, op-for-op) rather than building a parallel rig. One subtlety the oracle must honour: Ylmish elides content-neutral writes (a register set to its current value emits nothing), so the oracle must too, or it enters LWW races the SUT never enters.

### When to prefer example-based tests

Use plain `test "..." { ... }` when:
- The behaviour has a small number of meaningful cases (e.g. an empty list, a singleton, a known edge case).
- The test exercises JavaScript interop (Yjs API calls) where generators would add complexity without value.
- You are testing error paths or specific failure modes.

## How to make changes

1. Read the relevant plan objective in `doc/plans/`.
2. Make the smallest change that satisfies the acceptance criteria.
3. Run `npm test` to verify. All existing tests must continue to pass.
4. Don't change unrelated code. Don't refactor beyond what's needed.

## Creating issues from plans

See `.skills/write-plan-issue.md` for how to decompose plan objectives into agent-sized GitHub issues.
