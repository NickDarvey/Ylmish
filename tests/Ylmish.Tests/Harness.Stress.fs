module Ylmish.HarnessStress

// =============================================================================
// Plan 0006 — Step 6: adversarial / property-based stress (method M2).
//
// Beyond the hand-picked schedules of Steps 0–5, generate *random* concurrent
// add/remove schedules over two replicas and assert, on every one, that the
// keyed element-wise bridge (Option E's mechanism) both CONVERGES (P1) and
// MATCHES the raw-Yjs oracle membership (M1 / P2) — under maximal-concurrency
// (Concurrent) and eager (Immediate) delivery. The deterministic fixed-seed PRNG
// (Hedgehog.fs) runs 100 schedules per property, reproducibly.
// =============================================================================

open Hedgehog

open Ylmish
open Ylmish.Harness

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// Ids drawn from a small pool so Removes frequently hit a present id and
// concurrent edits actually collide.
let private idGen = Gen.int32 (Range.linear 0 4) |> Gen.map (fun i -> "id" + string i)

let private opGen =
    gen {
        let! r = Gen.int32 (Range.linear 0 1)
        let! isAdd = Gen.int32 (Range.linear 0 1)
        let! id = idGen
        return { Replica = r; Op = (if isAdd = 0 then Add (id, "") else Remove id) }
    }

let private scheduleGen = Gen.list (Range.linear 1 12) opGen

let tests = testList "Stress (0006 Step 6)" [

    testCase "random add/remove schedules converge and match the oracle (concurrent)" <| fun _ ->
        Property.check <| property {
            let! ops = scheduleGen
            let d = differential HarnessOptions.keyed Concurrent ops
            Expect.isTrue d.Converged "P1: replicas converge on every schedule"
            Expect.isTrue d.MatchesOracle
                (sprintf "M1/P2: keyed bridge must match the element-wise oracle membership (lost=%A)"
                    (Set.toList d.Lost))
        }

    testCase "random add/remove schedules converge and match the oracle (immediate)" <| fun _ ->
        Property.check <| property {
            let! ops = scheduleGen
            let d = differential HarnessOptions.keyed Immediate ops
            Expect.isTrue d.Converged "P1: replicas converge on every schedule"
            Expect.isTrue d.MatchesOracle "M1/P2: matches the oracle under eager delivery too"
        }
]
