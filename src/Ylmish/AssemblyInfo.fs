module internal Ylmish.AssemblyInfo

open System.Runtime.CompilerServices

// The test suite characterizes internal semantics (e.g. Text's pending-intent
// replay property) without widening the public surface.
[<assembly: InternalsVisibleTo("Ylmish.Tests")>]
do ()
