module Ylmish.Tests

open Ylmish

let tests = [
   Adaptive.Codec.tests
   Codec.Text.tests
   Y.Assumptions.tests
   Y.Delta.tests
   Y.Text.tests
   Y.Array.tests
   Y.Map.tests
   Y.Element.tests
   Y.Doc.tests
   Program.tests
   TodoCollaborative.tests
   Adaptive.Spike.tests
   Harness.tests
]

#if FABLE_COMPILER
open Fable.Mocha

let all = testList "" tests

[<EntryPoint>]
let main args =
    Mocha.runTests all

#else
open Expecto

[<Tests>]
let all = testList "" tests

[<EntryPoint>]
let main args =
    raise <| System.NotImplementedException "Ylmish tests depend on Yjs and can only be run with JS, not dotnet."
    runTestsWithArgs defaultConfig args all

#endif
