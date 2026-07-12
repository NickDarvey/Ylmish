# TODO

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
