module Ylmish.Tests

open Ylmish

let tests = [
   Adaptive.Codec.tests
   Y.Delta.tests
   Y.Text.tests
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
