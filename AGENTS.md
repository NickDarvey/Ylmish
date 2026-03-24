# Agent Guide — Ylmish

## What this project is

Ylmish bridges **Elmish** (F# MVU) ↔ **FSharp.Data.Adaptive** (incremental) ↔ **Yjs** (CRDT sync), compiled to JavaScript via **Fable**. See `README.md` for design and `doc/plans/0001-making-ylmish-functional.md` for the roadmap.

## Build & test

```bash
npm install          # restore .NET + npm dependencies
npm test             # adaptify codegen → Fable compile → Mocha tests
npm run test+watch   # watch mode (Fable + Adaptify)
```

Tests **must** run through Fable/Mocha (JavaScript). `dotnet test` will not work because the tests depend on the Yjs runtime.

## Repository layout

```
src/Fable.Yjs/       F# bindings for Yjs (generated + hand-tuned)
src/Ylmish/          Core library: Adaptive.Codec, Y (Delta/Text/Array/Map), Program
tests/Ylmish.Tests/  Fable.Mocha tests (compiled to JS and run with Mocha)
  common/            Shared test helpers: Example model, Elmish test harness
doc/plans/           Design plans and roadmap
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

Use `Property.check` with `property { let! ... }` when a test can be expressed as "for all valid inputs, this invariant holds." Hedgehog is already a dependency and works under Fable.

**Codec round-trips** — the canonical use case. Any model encoded and then decoded should equal the original. Write generators for your model types and test `decode(encode(x)) = x`.

```fsharp
testCase "Thing codec round-trips" <| fun _ -> Property.check <| property {
    let! thing = Example.Thing.gen
    let actual =
        thing
        |> Example.AdaptiveThing
        |> Example.Codec.Things.encode
        |> Decode.force Example.Codec.Things.decode
    Expect.equal actual thing ""
}
```

**Delta operations** — applying a random sequence of Yjs deltas to a `clist` and then reading it back should produce the expected content. Symmetric: applying random Adaptive deltas to a Yjs type and reading it back.

**Bi-directional sync invariants** — after any interleaved sequence of edits from both the Adaptive side and the Yjs side, the two representations should agree. Generate random edit sequences and assert convergence.

**Adaptive model update consistency** — after any sequence of model updates, `AVal.force model.Current` should equal the last update. The existing `basic updates work` test is an example.

**Element tree conversions** — `toAdaptive(ofAdaptive(x))` and `ofAdaptive(toAdaptive(x))` should be identity (or equivalent) for valid element trees.

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
