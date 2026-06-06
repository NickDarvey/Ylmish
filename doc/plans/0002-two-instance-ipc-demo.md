# 0002 — Two-instance collaboration demo over process IPC

## Goal

Demonstrate the `TodoCollaborative` example running as **two independent
processes** that stay in sync through Ylmish + Yjs, with edits made in one
process appearing in the other. The two processes are wired together with
Node.js **`child_process` IPC** (`process.send` / `process.on('message')`),
which is the closest "real" transport to the in-process `Main.sync` helper the
tests use today.

This turns the existing headless example (which only syncs two `Y.Doc`s inside
a single test process) into a runnable artifact that exercises the full
Elmish → Adaptive → Yjs → Adaptive → Elmish loop across a process boundary.

## Current state

- [`examples/TodoCollaborative`](../../examples/TodoCollaborative) wires a
  `TodoModel` through `Program.withYlmish` ([Main.fs](../../examples/TodoCollaborative/Main.fs))
  but is **headless** (`TodoModel.view _ _ = ()`) and has **no entry point**.
- `Main.sync` copies state between two `Y.Doc`s in one process using
  `Y.encodeStateAsUpdate` / `Y.applyUpdate`.
- `Program.withYlmish` ([src/Ylmish/Program.fs](../../src/Ylmish/Program.fs))
  already: materializes the initial model into the `Y.Doc`, materializes on each
  `User` update, and observes the `Y.Doc` (`observeDeep`) to decode remote
  changes back into the Elmish model via a `Set` message. It guards against
  echo loops with an `isWritingToYDoc` flag.
- Build tooling is Fable → JS → Node (`npm test` runs Fable then Mocha). There
  is no HTML/bundler setup, so a **console / Node** demo is the natural fit.

## Design

```
                ┌──────────────── launcher (parent) ────────────────┐
                │  forks two peers, relays Yjs updates between them  │
                │  runs a scripted scenario, prints a summary        │
                └───────┬───────────────────────────────────┬───────┘
              IPC (process.send / 'message')          IPC
                        │                                   │
              ┌─────────▼─────────┐               ┌─────────▼─────────┐
              │  peer A (process) │               │  peer B (process) │
              │  Elmish+Ylmish    │               │  Elmish+Ylmish    │
              │  own Y.Doc        │               │  own Y.Doc        │
              └───────────────────┘               └───────────────────┘
```

Single compiled module (`Demo.fs`) behaves as **launcher** or **peer** based on
`process.argv`:

- **launcher** (no `--peer` flag): `child_process.fork`s the same script twice
  with `--peer A` / `--peer B`, relays every `update` message from one child to
  the other, waits for both to report `ready`, then drives a scripted scenario
  (add an item on A, then add one on B), and finally prints both peers' state
  and exits.
- **peer `<name>`**: builds the Ylmish-wired Elmish program over a fresh
  `Y.Doc`, then:
  1. `doc.on('update', …)` — forwards **local** updates (origin ≠ `"remote"`) to
     the launcher as a byte array.
  2. `process.on('message', …)` — applies inbound `update` messages with
     `Y.applyUpdate(doc, bytes, "remote")` (the `"remote"` origin both prevents
     re-forwarding and, because `Set` does not re-materialize, prevents echo
     loops). It also handles scripted `op` messages by dispatching
     `Program.Message.User (AddItem text)`.
  3. logs the model on every change via `Program.withSetState`.

Echo-loop safety relies on existing behaviour: a `User` edit materializes to the
`Y.Doc` (→ local `update` event → forwarded); an applied remote update fires
`observeDeep` → decodes → dispatches `Set`, and the `Set` branch only updates the
adaptive model (no re-materialize), so nothing is forwarded back.

Yjs `Uint8Array` updates are converted to/from plain number arrays at the IPC
boundary so the default JSON IPC serialization round-trips them safely.

## Objectives

### Objective 1: Add `Demo.fs` to the example

A single F# module compiled by Fable that contains: a minimal Node interop shim
(`process.argv/send/on`, `child_process.fork`, `setTimeout`, `Uint8Array`↔number
array, a typed `doc.on('update')`), the `peer` runner, the `launcher` runner,
and a top-level dispatch on `process.argv`.

### Objective 2: Wire build & run

- Add `Demo.fs` to `TodoCollaborative.fsproj`.
- Add npm scripts: `demo:build` (Fable-compile the example to
  `build/examples/TodoCollaborative`) and `demo` (build then
  `node build/examples/TodoCollaborative/Demo.js`).

### Objective 3: Verify

- `npm run demo` launches two processes, shows an edit on A propagating to B and
  an edit on B propagating to A, and both peers converge to the same item list.
- Existing `npm test` still passes (no library changes).

## Scope boundaries

- **In scope**: a headless, scripted two-process demo over `child_process` IPC;
  console logging of model state; npm wiring.
- **Out of scope / defer**: an interactive UI, browser/BroadcastChannel or
  WebSocket transports, Electron, network transports, incremental conflict-merge
  scenarios beyond simple adds, and any change to the Ylmish library itself.

## Files likely to change

- `examples/TodoCollaborative/Demo.fs` (new)
- `examples/TodoCollaborative/TodoCollaborative.fsproj`
- `package.json`
