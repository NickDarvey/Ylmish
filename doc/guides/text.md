# `Ylmish.Text`

`Text` is the collaborative text value: an immutable value type your model
holds wherever concurrent edits should **merge** instead of overwrite. Encode
it with `Encode.text` and it is backed by a `Y.Text`; concurrent edits from
different peers interleave, and nobody's keystrokes are lost.

Every code block in this guide is a verbatim excerpt of compiled code; the
source file is named above each block.

## The shape

- `Text.empty`, `Text.ofString`, `Text.toString`, `Text.length` — construction
  and reading.
- `Text.insert at value`, `Text.remove at count`,
  `Text.replace at count value` — the intent-carrying edits your `update`
  function uses.
- `Text.edit newValue` — convenience for a plain `<input>`/`<textarea>`
  onChange (see the ambiguity note below).

`Text` carries **edit intent**: an insert is remembered as "insert at 5", not
recovered later by diffing strings. The runtime drains that intent into
precise splices on the backing `Y.Text` — which is exactly what makes the
merge behaviour right under concurrency.

## In an Elmish update

Model edits are ordinary immutable updates. This is the north-star test for
issue #83 — two full `withYlmish` programs, offline concurrent edits to the
same field, converging in both **Elmish models**
([`tests/Ylmish.Tests/NorthStar.fs`](../../tests/Ylmish.Tests/NorthStar.fs)):

```fsharp
testCase "concurrent Text edits converge interleaved across two withYlmish programs" (fun () ->
    let d1 = Y.Doc.Create ()
    let d2 = Y.Doc.Create ()
    use p1 = Elmish.Program.test (makeProgram d1)
    use p2 = Elmish.Program.test (makeProgram d2)

    // Shared starting text, created by one peer and synced.
    p1.Dispatch (user (EditBody (Text.edit "hello")))
    syncBoth d1 d2

    // Offline: both edit the same field concurrently...
    p1.Dispatch (user (EditBody (Text.insert 5 " world")))
    p2.Dispatch (user (EditBody (Text.insert 0 "oh, ")))
    // ...then the network heals.
    syncBoth d1 d2

    Expect.equal (Text.toString p1.Model.Body) "oh, hello world"
        "both peers' edits survive, interleaved — the issue #83 headline"
    Expect.equal (Text.toString p2.Model.Body) (Text.toString p1.Model.Body)
        "both models converge")
```

## Equality is by content only

Two `Text` values with the same content are equal (and hash and compare
equal), regardless of the edits that produced them. Pending intent is
transport, not identity: views, tests, and `HashMap` keys stay simple, and
folding a decoded remote model into your program never looks like a change
when the content already matches.

## Positions clamp, they don't throw

An out-of-range edit inside an Elmish `update` must not crash the loop, so
positions clamp into range — a contract pinned by test, not an accident
([`tests/Ylmish.Tests/Text.fs`](../../tests/Ylmish.Tests/Text.fs)):

```fsharp
test "bounds are clamped, not thrown" {
    let t = Text.ofString "abc"
    Expect.equal (Text.insert -5 "x" t |> Text.toString) "xabc" "negative insert clamps to 0"
    Expect.equal (Text.insert 99 "x" t |> Text.toString) "abcx" "past-end insert appends"
    Expect.equal (Text.remove 1 99 t |> Text.toString) "a" "over-long remove clamps to end"
    Expect.equal (Text.remove -1 1 t |> Text.toString) "bc" "negative remove clamps to 0"
    Expect.equal (Text.replace 2 99 "Z" t |> Text.toString) "abZ" "replace clamps count"
}
```

Content-neutral edits (a replace that produces the same string) are elided
entirely — they emit no CRDT operation, consistent with the codec-wide rule
that only content changes write.

## `Text.edit` and its ambiguity

`Text.edit newValue` derives a single splice from the old and new strings by
common prefix/suffix diff — the right tool when all you have is a textbox's
new value. For a single contiguous edit the derived splice is minimal. But
repeated characters make the position ambiguous: inserting an `a` into
`"aaa"` produces the same string wherever it landed, so the derived splice may
sit at a different offset than the actual caret. Convergence is unaffected;
interleaving fidelity under concurrency is slightly coarser. When you know the
edit position — a controlled editor component, a keyboard handler — prefer
`insert`/`remove`/`replace`.

## When you need the real `Y.Text`

Editor components like CodeMirror or Monaco want to bind to an actual
`Y.Text`, not a value snapshot. That is the escape hatch's job: a
`CustomElement` whose `Connect` captures `ctx.GetText ()` and hands the live,
integrated instance to the editor. See
[custom-elements.md](custom-elements.md).
