# TODO

1. **Pin (and if needed fix) `Encode.option` Some→None inside a replaced keyed-map item.**

   When a keyed-map item's value is replaced wholesale (any one-field record edit
   replaces the item's whole `Encoded`), the binding disposes the old item
   attachment, re-flushes the new encoding into the adopted containers, and
   re-attaches. The flush path never *deletes* keys — an option field that
   transitioned Some→None as part of that same replacement relies on the fresh
   option attachment's transition callback to delete the backing key, and that
   callback's initial-skip may swallow the transition. Nothing exercises
   option-inside-map-item today (noted at plan 0002 Step 10). Write the test
   first: add an optional field to a keyed item's record, flip it `Some x` →
   `None` via a normal item edit, and assert the key disappears from the item's
   `Y.Map` on both peers.

1. **Rewrite the README's Quickstart with less faff.**

   The Background sections are plain and direct; the Quickstart narrates —
   bold signposting, "That's the whole integration…" asides, selling each
   snippet. Trim it to the same register: show the code, say what it does,
   move on.

1. **Cut the running commentary of history from README.md and doc/guides/.**

   References to demo "acts", assumption ids (`U3`, `U15`, `L8`), plan-step
   numbers and "the materialize path" are development-diary vocabulary. The
   docs should describe the library as it is; history belongs in
   `doc/plans/0002-ylmish-redesign.md`.

1. **Replace the copy-pasted doc examples with literate/executable ones.**

   Samples currently exist twice: compiled in `examples/`/`tests/` and quoted
   verbatim in the docs, kept in sync by markers and a checker script. Adopt
   the modern F# approach instead — literate `.fsx` compiled/evaluated by
   [fsdocs (FSharp.Formatting)](https://fsprojects.github.io/FSharp.Formatting/),
   or docs generated from the example sources — so each sample exists once.
   Constraint to solve: Ylmish samples execute against the Yjs runtime, so they
   run under Fable/Node, not dotnet fsi.

1. **Re-enable NuGet publishing.**

   `Ylmish.fsproj` carries the package metadata already; wire the pack/publish
   step back into CI (`.github/workflows/build.yml`) and decide versioning.
