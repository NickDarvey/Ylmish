# Objective 0 — Update Dependencies and Toolchain

Parent: `doc/plans/0001-initial.md`, Objective 0

This objective brings the project onto supported, stable tooling before any feature work begins. It is broken into individual issues ordered by dependency—each issue should take ~15 minutes and can be verified independently.

## Current → Target versions

| Package | Current | Target | Risk |
|---|---|---|---|
| .NET SDK | 6.0.100 | 8.0.x LTS | Medium — TFM change, FSharp.Core bump |
| FSharp.Core | 6.0.5 | 8.0.x | Low — follows SDK |
| Fable.Core | 4.0.0-theta-007 | 4.5.0 | Medium — theta pre-release → stable, possible API changes |
| FSharp.Data.Adaptive | 1.2.13 | 1.2.26 | Low — patch/minor updates only |
| Adaptify.Core | 1.1.9 | 1.3.7 | Low–Medium — minor version bump, check codegen compat |
| Fable.Elmish | 3.1.0 | 4.2.0 | Medium — major version, API changes in `Program` module |
| Hedgehog | 0.13.0 | 2.0.0 | High — major version, merged Experimental, tagged journal, Fable support improved |
| Fable.Mocha | 2.15.0 | 2.17.0 | Low — minor updates |
| Expecto | 9.0.4 | 10.2.3 | Low–Medium — major version bump |
| Microsoft.NET.Test.Sdk | 17.4.1 | 17.12.x | Low — follows .NET version |
| YoloDev.Expecto.TestSdk | 0.13.3 | 0.14.3 | Low |
| Yjs (npm) | 13.5.35 | 13.6.30 | Low — patch/minor, backwards compatible |
| Mocha (npm) | 10.2.0 | 11.7.x | Medium — major version, requires Node ≥ 18.18.0 |
| concurrently (npm) | 7.6.0 | 9.2.x | Low–Medium — major version bump |
| source-map-support (npm) | 0.5.21 | 0.5.21 | None — already latest 0.x |

---

## Issue 0-1: Upgrade .NET SDK to 8.0 LTS

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- .NET 6 reached end of support November 2024. .NET 8 is the current LTS (supported until November 2026). Hedgehog ≥ 1.0 requires .NET 8 as a baseline.

### Task
1. Update `global.json` to set SDK version to `8.0.100` with `"rollForward": "latestFeature"`.
2. Update `TargetFramework` from `net6.0` to `net8.0` in:
   - `src/Fable.Yjs/Fable.Yjs.fsproj`
   - `src/Ylmish/Ylmish.fsproj`
   - `tests/Ylmish.Tests/Ylmish.Tests.fsproj`
3. Update `FSharp.Core` version in `Directory.Packages.props` from `6.0.5` to `8.0.100` (or the latest 8.0.x).
4. Run `dotnet restore` and fix any restore errors.
5. Run `npm test` to verify all existing tests still pass.

### Acceptance criteria
- [ ] `global.json` specifies .NET 8.0.x SDK
- [ ] All `.fsproj` files target `net8.0`
- [ ] `dotnet restore` succeeds
- [ ] `npm test` passes — all existing tests pass

### Scope boundaries
- **In scope**: SDK, TFM, and FSharp.Core only.
- **Out of scope**: Upgrading any other NuGet or npm packages. Those are separate issues.

### Files likely to change
- `global.json`
- `Directory.Packages.props`
- `src/Fable.Yjs/Fable.Yjs.fsproj`
- `src/Ylmish/Ylmish.fsproj`
- `tests/Ylmish.Tests/Ylmish.Tests.fsproj`
- `packages.lock.json` files (regenerated)

---

## Issue 0-2: Upgrade Fable.Core from pre-release to stable

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- Depends on: Issue 0-1 (.NET 8 SDK)
- `Fable.Core` is at `4.0.0-theta-007`, a pre-release from the Fable 4 development cycle. The stable `4.5.0` has been out since mid-2024. The theta→stable transition may include renamed attributes or changed emit semantics.

### Task
1. Update `Fable.Core` version in `Directory.Packages.props` from `4.0.0-theta-007` to `4.5.0`.
2. Run `dotnet restore`.
3. Run `dotnet fable tests/Ylmish.Tests/Ylmish.Tests.fsproj -o build/tests/Ylmish.Tests --sourceMaps` to check for Fable compilation errors.
4. If there are breaking changes (renamed attributes, changed `[<Emit>]` syntax, etc.), fix them in:
   - `src/Fable.Yjs/Yjs.fs` and `src/Fable.Yjs/Lib0.fs` (Yjs bindings with `[<Import>]`, `[<Emit>]`)
   - `src/Ylmish/` source files
5. Run `npm test` to verify all existing tests still pass.

### Acceptance criteria
- [ ] `Fable.Core` is `4.5.0` in `Directory.Packages.props`
- [ ] Fable compilation succeeds without errors
- [ ] `npm test` passes

### Scope boundaries
- **In scope**: `Fable.Core` only. Fix any breaking changes from theta→stable.
- **Out of scope**: Upgrading Fable CLI tool version, other Fable packages, or npm packages.

### Files likely to change
- `Directory.Packages.props`
- `src/Fable.Yjs/Yjs.fs` (if Fable attributes changed)
- `src/Fable.Yjs/Lib0.fs` (if Fable attributes changed)
- `packages.lock.json` files

---

## Issue 0-3: Upgrade FSharp.Data.Adaptive and Adaptify.Core

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- Depends on: Issue 0-1 (.NET 8 SDK)
- `FSharp.Data.Adaptive` 1.2.13→1.2.26 is a patch/minor update within the same major version. `Adaptify.Core` 1.1.9→1.3.7 includes codegen improvements and bug fixes.

### Task
1. Update `Directory.Packages.props`:
   - `FSharp.Data.Adaptive` from `1.2.13` to `1.2.26`
   - `Adaptify.Core` from `1.1.9` to `1.3.7`
2. Run `dotnet restore`.
3. Run `npm run adaptify` to regenerate `Example.g.fs` with the new Adaptify version.
4. Check if `tests/Ylmish.Tests/common/Example.g.fs` changed. If so, verify the generated code compiles.
5. Run `npm test` to verify all existing tests still pass.

### Acceptance criteria
- [ ] `FSharp.Data.Adaptive` is `1.2.26` in `Directory.Packages.props`
- [ ] `Adaptify.Core` is `1.3.7` in `Directory.Packages.props`
- [ ] Adaptify codegen succeeds (`npm run adaptify`)
- [ ] `npm test` passes

### Scope boundaries
- **In scope**: FSharp.Data.Adaptive and Adaptify.Core only.
- **Out of scope**: Any API changes in source code. If the Adaptive API changed, file a separate issue.

### Files likely to change
- `Directory.Packages.props`
- `tests/Ylmish.Tests/common/Example.g.fs` (regenerated)
- `packages.lock.json` files

---

## Issue 0-4: Upgrade Fable.Mocha, Expecto, and test SDK packages

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- Depends on: Issue 0-1 (.NET 8 SDK)
- `Fable.Mocha` 2.15.0→2.17.0 is a minor update. `Expecto` 9.0.4→10.2.3 is a major version bump with potential API changes. `Microsoft.NET.Test.Sdk` and `YoloDev.Expecto.TestSdk` are test infrastructure.

### Task
1. Update `Directory.Packages.props`:
   - `Fable.Mocha` from `2.15.0` to `2.17.0`
   - `Expecto` from `9.0.4` to `10.2.3`
   - `Microsoft.NET.Test.Sdk` from `17.4.1` to `17.12.0` (or latest 17.x)
   - `YoloDev.Expecto.TestSdk` from `0.13.3` to `0.14.3`
2. Run `dotnet restore`.
3. Check `tests/Ylmish.Tests/Ylmish.Tests.fs` for any Expecto API changes (e.g. `testList`, `test`, `Expect.equal` signatures). The Fable code path uses Fable.Mocha which mirrors the Expecto API.
4. Run `npm test` to verify all existing tests still pass.

### Acceptance criteria
- [ ] All test framework package versions updated in `Directory.Packages.props`
- [ ] `npm test` passes
- [ ] No Expecto API breakage in the `#else` (non-Fable) path

### Scope boundaries
- **In scope**: Test framework packages only.
- **Out of scope**: Changing test structure, adding new tests, or upgrading Hedgehog (separate issue).

### Files likely to change
- `Directory.Packages.props`
- `packages.lock.json` files

---

## Issue 0-5: Upgrade Fable.Elmish to 4.x

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- Depends on: Issue 0-2 (Fable.Core stable)
- `Fable.Elmish` 3.1.0→4.x is a major version bump. The Elmish 4.x line is designed for Fable 4 compatibility. Key changes include the `Program` module API and subscription model. Since `Program.withYlmish` in `src/Ylmish/Program.fs` wraps the Elmish `Program` type, API changes here will need adaptation.

### Task
1. Review the [Elmish 4.0 migration guide / changelog](https://github.com/elmish/elmish/releases) for breaking changes.
2. Update `Directory.Packages.props`: `Fable.Elmish` from `3.1.0` to `4.2.0`.
3. Run `dotnet restore`.
4. Fix compilation errors in:
   - `src/Ylmish/Program.fs` — the main `withYlmish` function wraps Elmish's `Program` type
   - `tests/Ylmish.Tests/common/Elmish.fs` — the test harness
   - `tests/Ylmish.Tests/Program.fs` — the integration tests
5. Run `npm test` to verify all existing tests still pass.

### Acceptance criteria
- [ ] `Fable.Elmish` is `4.2.0` in `Directory.Packages.props`
- [ ] `src/Ylmish/Program.fs` compiles with Elmish 4.x API
- [ ] `npm test` passes

### Scope boundaries
- **In scope**: Fable.Elmish upgrade and fixing any resulting compilation errors.
- **Out of scope**: Implementing the commented-out `Program.withYlmish` logic (that's Objective 3). Also out of scope: adding `Fable.Browser.Dom` upgrade (evaluate separately if needed).

### Files likely to change
- `Directory.Packages.props`
- `src/Ylmish/Program.fs`
- `tests/Ylmish.Tests/common/Elmish.fs`
- `tests/Ylmish.Tests/Program.fs`
- `packages.lock.json` files

---

## Issue 0-6: Evaluate and upgrade Hedgehog to 2.0

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- Depends on: Issue 0-1 (.NET 8 SDK), Issue 0-2 (Fable.Core stable)
- `Hedgehog` 0.13.0→2.0.0 is a large jump. Key changes since 0.13.0:
  - **1.0.0** (Nov 2025): .NET 8 baseline, `Hedgehog.Experimental` merged (adds `Gen.auto`/`Gen.autoWith`), perf improvements, Fable support improved (conditional compilation for Autogen/Linq), targeted `netstandard2.0` only.
  - **1.1.0** (Dec 2025): Async property support.
  - **2.0.0** (Dec 2025): Different seed per property, `Property.ignoreResult`, tagged journal (breaking: journal entries are now tagged, which changes the `Report` type).
- Hedgehog explicitly supports Fable—the `.fsproj` includes `FABLE_COMPILER` conditionals and packages Fable sources. The Autogen and Linq modules are excluded under Fable.
- The existing tests use: `Gen.string`, `Gen.int32`, `Gen.alphaNum`, `Gen.list`, `Gen.map`, `Range.linear`, `Range.linearBounded`, `Property.check`, `property { let! ... }`, `gen { let! ... }`. All of these exist in 2.0.0.

### Task
1. Read the [Hedgehog changelog](https://github.com/hedgehogqa/fsharp-hedgehog/releases) for 1.0.0, 1.1.0, and 2.0.0.
2. Update `Directory.Packages.props`: `Hedgehog` from `0.13.0` to `2.0.0`.
3. Run `dotnet restore`.
4. Check `tests/Ylmish.Tests/Adaptive.Codec.fs` for any API changes:
   - Verify `Gen.string`, `Gen.int32`, `Gen.list`, `Gen.map`, `Range.linear`, `Range.linearBounded`, `Property.check`, `property { ... }`, and `gen { ... }` still compile.
   - If `Property.check` signature changed (tagged journal), adapt call sites.
5. Run `npm test` to verify all existing tests still pass.
6. If 2.0.0 causes Fable compilation issues, fall back to 1.1.0 and note the issue.

### Acceptance criteria
- [ ] `Hedgehog` is upgraded to 2.0.0 (or 1.1.0 with noted rationale) in `Directory.Packages.props`
- [ ] Fable compiles successfully with the new Hedgehog version
- [ ] `npm test` passes
- [ ] Property-based tests in `Adaptive.Codec.fs` still work

### Scope boundaries
- **In scope**: Hedgehog package upgrade and fixing any compilation issues.
- **Out of scope**: Adding new Hedgehog-based tests (that's ongoing work as per AGENTS.md guidance). Also out of scope: using `Gen.auto` or other new features—that can come later.

### Files likely to change
- `Directory.Packages.props`
- `tests/Ylmish.Tests/Adaptive.Codec.fs` (if API changed)
- `packages.lock.json` files

---

## Issue 0-7: Upgrade npm dependencies (Yjs, Mocha, concurrently)

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- Depends on: Issue 0-1 (.NET 8 SDK) — Mocha 11.x requires Node ≥ 18.18.0, which ships with .NET 8 CI images.
- `Yjs` 13.5.35→13.6.30: many bug fixes, performance improvements, no breaking API changes within 13.x.
- `Mocha` 10.2.0→11.7.x: major version bump, dropped Node < 18.18.0 support, some config changes.
- `concurrently` 7.6.0→9.2.x: major version bump, requires Node ≥ 18.

### Task
1. Update `package.json`:
   - `yjs` from `13.5.35` to `13.6.30`
   - `mocha` from `10.2.0` to `11.7.5`
   - `concurrently` from `7.6.0` to `9.2.1`
   - Evaluate `source-map-support` — check if Mocha 11.x still needs it or has built-in source map support.
2. Delete `node_modules/` and `package-lock.json`, then run `npm install` to regenerate.
3. Check if the Mocha config in `package.json` (`"mocha"` key) needs updates for Mocha 11.x (e.g. `--require` syntax, `--spec` handling).
4. Run `npm test` to verify all existing tests still pass.

### Acceptance criteria
- [ ] `package.json` has updated npm dependency versions
- [ ] `package-lock.json` regenerated
- [ ] `npm test` passes
- [ ] No Mocha configuration warnings or deprecations

### Scope boundaries
- **In scope**: npm dependencies only.
- **Out of scope**: NuGet packages, Fable configuration, or adding new npm packages.

### Files likely to change
- `package.json`
- `package-lock.json`

---

## Issue 0-8: Final verification and CI green

### Context
- Plan: `doc/plans/0001-initial.md`, Objective 0
- Depends on: All previous issues (0-1 through 0-7)
- This is the final sweep to ensure everything works together after all individual upgrades.

### Task
1. Run `dotnet restore --force-evaluate` to ensure all NuGet packages resolve.
2. Regenerate all `packages.lock.json` files: `dotnet restore --force-evaluate`.
3. Run `npm install` to ensure `package-lock.json` is up to date.
4. Run `npm test` — full test suite.
5. Review CI workflow (`.github/workflows/build.yml`) — check if the GitHub Actions runner image needs updating (e.g. `ubuntu-latest` image and .NET SDK setup).
6. Push and verify CI passes.

### Acceptance criteria
- [ ] `dotnet restore` succeeds
- [ ] `npm install` succeeds
- [ ] `npm test` passes locally
- [ ] CI workflow (`.github/workflows/build.yml`) passes
- [ ] All lock files committed and up to date

### Scope boundaries
- **In scope**: Integration verification only. Fix any remaining incompatibilities.
- **Out of scope**: New features, new tests, refactoring.

### Files likely to change
- `.github/workflows/build.yml` (if .NET SDK setup needs updating)
- `packages.lock.json` files
- `package-lock.json`
