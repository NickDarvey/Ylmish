# Agent Guide — Ylmish

## What this project is

Ylmish bridges **Elmish** (F# MVU) ↔ **FSharp.Data.Adaptive** (incremental) ↔ **Yjs** (CRDT sync), compiled to JavaScript via **Fable**. See `README.md` for design and `doc/plans/0001-initial.md` for the roadmap.

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

## How to make changes

1. Read the relevant plan objective in `doc/plans/`.
2. Make the smallest change that satisfies the acceptance criteria.
3. Run `npm test` to verify. All existing tests must continue to pass.
4. Don't change unrelated code. Don't refactor beyond what's needed.

## Creating issues from plans

See `.skills/write-plan-issue.md` for how to decompose plan objectives into agent-sized GitHub issues.
