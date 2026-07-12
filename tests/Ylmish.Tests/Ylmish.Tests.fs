module Ylmish.Tests

open Ylmish

let tests = [
   Tests.Text.tests
   Tests.Codec.tests
   Adaptive.Assumptions.tests
   Tests.Delta.tests
   Y.Assumptions.tests
   Harness.tests
   Tests.Binding.tests
   Tests.BindingDecode.tests
   NorthStar.tests
   Tests.CustomElements.tests
   Tests.Stress.tests
   Tests.Program.tests
   TodoCollaborative.tests
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
