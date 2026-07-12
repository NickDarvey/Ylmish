# Custom elements — the escape hatch

`Encode.custom` binds a field of your model to a **consumer-defined merge
strategy over a real Yjs type**. It exists for two situations the built-in
combinators don't cover:

1. **A merge no built-in provides** — e.g. a counter whose concurrent
   increments should *sum* rather than last-writer-win.
2. **A component that wants the actual Yjs type** — e.g. handing a live
   `Y.Text` to CodeMirror or Monaco, which speak Yjs natively.

Writing one requires `open Yjs` and `open Ylmish.Codec` — nothing else. No
Ylmish internals, no FSharp.Data.Adaptive. That is the encapsulation line, and
it is enforced by the examples below, which compile with exactly those opens.

Every code block in this guide is a verbatim excerpt of compiled code; the
source file is named above each block.

## The contract

A `CustomElement` has two members:

- **`Connect : BindContext -> IDisposable`** — called once when the runtime
  attaches your program to the doc. The `BindContext` gives you
  `GetText ()` / `GetMap ()` / `GetArray ()` — get-or-adopt accessors that
  always return the **one** integrated instance (calling twice returns the
  same object, so you cannot corrupt the doc by re-integrating) — and
  `Origin`, the token your writes must be transacted under.
- **`Value : obj`** — the current merged value, read whenever the model is
  decoded. `Decode.custom` unboxes it under your field's type.

**The first rule of writing a binding: tag every write with `ctx.Origin`.**
A write transacted under the attachment origin is recognised as your own and
never echoes back into your program as a phantom remote update. Remote
changes need no subscription on your part — they surface through the ordinary
decode path, and your `Value` is re-read.

## A counter that sums

The demo's grow-only counter, entire
([`examples/TodoCollaborative/Counter.fs`](../../examples/TodoCollaborative/Counter.fs)):

<!-- sample: counter -->
```fsharp
/// A grow-only counter over a Y.Array of ticks. Concurrent increments from
/// different peers BOTH survive (array inserts merge), so the merged value is
/// their SUM — a merge no built-in encoding provides.
type GrowOnlyCounter () =
    let mutable ticks : Y.Array<obj> option = None
    let mutable origin : obj = null

    /// Push one tick. Call from a Cmd effect after an optimistic increment;
    /// the authoritative count comes back through Decode.custom. The write is
    /// tagged with the attachment origin so it never echoes back as a remote.
    member _.Bump () =
        match ticks with
        | Some arr ->
            match arr.doc with
            | Some doc -> Y.transact (doc, (fun _ -> arr.push [| box 1 |]), origin)
            | None -> ()
        | None -> ()

    interface CustomElement with
        member _.Connect ctx =
            ticks <- Some (ctx.GetArray ())
            origin <- ctx.Origin
            { new IDisposable with member _.Dispose () = () }
        member _.Value =
            match ticks with
            | Some arr -> box ((arr.toArray ()).Count)
            | None -> box 0
```

Wire it like any other field — `"hits", Encode.custom counter` on the encode
side, `Decode.object.required "hits" Decode.custom` on the decode side (see
the full codec in [codec.md](codec.md)) — and drive it from `update` with an
optimistic increment plus an effect
([`examples/TodoCollaborative/Model.fs`](../../examples/TodoCollaborative/Model.fs)):

<!-- sample: counter-bump -->
```fsharp
| Bump ->
    // Optimistic local increment; the effect pushes a tick through the
    // counter binding, and the authoritative (summed) count returns
    // through Decode.custom on remote transactions.
    { model with Hits = model.Hits + 1 }, Cmd.ofEffect (fun _ -> counter.Bump ())
```

The optimistic increment keeps the UI immediate; because `Bump ()` transacts
under the captured origin, the local write does not bounce back, and the
summed count arrives whenever a *remote* transaction does. The demo shows two
peers bumping 2 + 1 offline and both converging on 3.

## Handing a live `Y.Text` to an editor

The other archetype: capture the integrated Yjs instance and give it to a
component that binds to Yjs directly. The model still receives the merged
content through the ordinary decode path — the editor and the Elmish program
stay consistent without talking to each other
([`tests/Ylmish.Tests/CustomElements.fs`](../../tests/Ylmish.Tests/CustomElements.fs)):

<!-- sample: editor-surface -->
```fsharp
type EditorSurface () =
    let mutable text : Y.Text option = None
    /// The live Y.Text — what you would hand to the editor component.
    member _.Text = Option.get text
    interface CustomElement with
        member _.Connect ctx =
            text <- Some (ctx.GetText ())
            { new IDisposable with member _.Dispose () = () }
        member _.Value =
            match text with
            | Some t -> box (t.toString ())
            | None -> box ""
```

An editor writing to `surface.Text` transacts under its own origins — those
*do* flow into your model (they are remote as far as your program is
concerned), which is exactly what you want: external edits appear in the
decoded field like any peer's.

## Rules of the road

- **Transact your own writes under `ctx.Origin`.** Untagged writes echo back
  into your program as spurious remote updates.
- **Never cache Yjs objects across docs, and never integrate your own.** The
  `Get*` accessors adopt what is already in the doc or create it exactly once;
  re-integrating an already-integrated Y type corrupts the doc silently
  (repeated `Get*` calls return the same instance, so the safe thing is also
  the easy thing).
- **`Value` should be cheap and current.** It is read on every decode of your
  program's model.
- **`Connect`'s disposable** is your teardown seam (detach editor bindings,
  drop references); the runtime disposes it when the program terminates.
