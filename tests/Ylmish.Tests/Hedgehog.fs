namespace Hedgehog

// Minimal, self-contained, Fable-compatible reimplementation of the small
// Hedgehog API surface this test suite uses: the `gen` and `property`
// computation expressions, a handful of `Gen` combinators, `Range.linear` /
// `Range.linearBounded`, and `Property.check`.
//
// Why this exists: Hedgehog 0.13.0 was the last version without a Fable
// packaging bug, but its Report.fs calls the
// System.ArgumentException(message, innerException) constructor overload, which
// Fable 5's fable-library does not implement. That makes the Hedgehog package
// fail to load under Fable 5 ("does not provide an export named
// ArgumentException_$ctor_68CE3CA2"). Newer Hedgehog (2.0.x) reintroduces the
// original Fable packaging bug, so neither path works. This module keeps the
// same public API and `Hedgehog` namespace so the test files need no changes,
// while being fully Fable 5 compatible because we control all of the code.
//
// Determinism: a fixed-seed Park-Miller PRNG is used (no System.Random, no
// time), matching the suite's reproducibility requirement. No shrinking is
// performed; shrinking only minimises failure reports, it does not affect
// pass/fail outcomes.

/// Deterministic Park-Miller minimal-standard PRNG. All arithmetic stays within
/// the IEEE-754 safe-integer range so the sequence is identical under .NET and
/// Fable (JavaScript).
type internal Rng(seed : int) =
    let mutable state =
        let s = float (abs (seed % 2147483647))
        if s = 0.0 then 1.0 else s
    member _.NextFloat () : float =
        state <- (state * 16807.0) % 2147483647.0
        state / 2147483647.0
    /// Uniform int in [lo, hi] for ranges whose span fits in the safe-integer
    /// range (every range used by the suite except the full Int32 range).
    member this.NextIntSmall (lo : int, hi : int) : int =
        if hi <= lo then lo
        else lo + int (this.NextFloat () * (float hi - float lo + 1.0))
    /// Uniform value across the entire Int32 range, built from two 16-bit draws
    /// so it never overflows during generation.
    member this.NextInt32Full () : int =
        let high = this.NextIntSmall (0, 65535)
        let low = this.NextIntSmall (0, 65535)
        let u = float high * 65536.0 + float low
        int (if u >= 2147483648.0 then u - 4294967296.0 else u)

/// A generation range. Only integer ranges are needed by the suite.
type Range<'a> = internal { Lo : int; Hi : int }

module Range =
    let linear (lo : int) (hi : int) : Range<int> = { Lo = lo; Hi = hi }
    let linearBounded () : Range<int> =
        { Lo = System.Int32.MinValue; Hi = System.Int32.MaxValue }

/// A generator: draws a value from a PRNG.
type Gen<'T> = internal Gen of (Rng -> 'T)

module Gen =
    let internal run (Gen g) rng = g rng

    let constant (x : 'a) : Gen<'a> = Gen (fun _ -> x)

    let map (f : 'a -> 'b) (Gen g : Gen<'a>) : Gen<'b> =
        Gen (fun rng -> f (g rng))

    let bind (Gen g : Gen<'a>) (f : 'a -> Gen<'b>) : Gen<'b> =
        Gen (fun rng -> f (g rng) |> fun next -> run next rng)

    let private alphaNumChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"

    let alphaNum : Gen<char> =
        Gen (fun rng -> alphaNumChars.[rng.NextIntSmall (0, alphaNumChars.Length - 1)])

    let int32 (range : Range<int>) : Gen<int> =
        Gen (fun rng ->
            if float range.Hi - float range.Lo + 1.0 >= 2147483648.0 then
                rng.NextInt32Full ()
            else
                rng.NextIntSmall (range.Lo, range.Hi))

    let string (range : Range<int>) (Gen ch : Gen<char>) : Gen<string> =
        Gen (fun rng ->
            let n = rng.NextIntSmall (range.Lo, range.Hi)
            System.String (Array.init n (fun _ -> ch rng)))

    let option (Gen g : Gen<'a>) : Gen<'a option> =
        Gen (fun rng -> if rng.NextIntSmall (0, 4) = 0 then None else Some (g rng))

    let list (range : Range<int>) (Gen g : Gen<'a>) : Gen<'a list> =
        Gen (fun rng ->
            let n = rng.NextIntSmall (range.Lo, range.Hi)
            List.init n (fun _ -> g rng))

/// A testable property. Running it executes the body; assertion helpers throw
/// on failure, which propagates and fails the test.
type Property<'T> = internal Property of (Rng -> 'T)

module Property =
    /// Number of test cases generated per property (matches Hedgehog's default).
    let private testLimit = 100

    let check (Property run : Property<unit>) : unit =
        let rng = Rng 424242
        for _ in 1 .. testLimit do
            run rng

type GenBuilder () =
    member _.Bind (m : Gen<'a>, k : 'a -> Gen<'b>) : Gen<'b> = Gen.bind m k
    member _.Return (x : 'a) : Gen<'a> = Gen.constant x
    member _.ReturnFrom (m : Gen<'a>) : Gen<'a> = m
    member _.Delay (f : unit -> Gen<'a>) : Gen<'a> = Gen (fun rng -> Gen.run (f ()) rng)
    member _.Zero () : Gen<unit> = Gen.constant ()

type PropertyBuilder () =
    member _.Bind (Gen g : Gen<'a>, k : 'a -> Property<'b>) : Property<'b> =
        Property (fun rng ->
            let a = g rng
            let (Property p) = k a
            p rng)
    member _.Return (b : bool) : Property<unit> =
        Property (fun _ -> if not b then failwith "Expected 'true' but was 'false'.")
    member _.ReturnFrom (m : Property<'a>) : Property<'a> = m
    member _.Zero () : Property<unit> = Property (fun _ -> ())
    member _.Delay (f : unit -> Property<'a>) : Property<'a> =
        Property (fun rng ->
            let (Property p) = f ()
            p rng)
    member _.Combine (Property a : Property<unit>, Property b : Property<'a>) : Property<'a> =
        Property (fun rng ->
            a rng
            b rng)
    member _.For (xs : seq<'a>, k : 'a -> Property<unit>) : Property<unit> =
        Property (fun rng ->
            for x in xs do
                let (Property p) = k x
                p rng)
    member _.While (cond : unit -> bool, Property body : Property<unit>) : Property<unit> =
        Property (fun rng -> while cond () do body rng)
    member _.TryWith (Property body : Property<'a>, handler : exn -> Property<'a>) : Property<'a> =
        Property (fun rng ->
            try body rng
            with e ->
                let (Property p) = handler e
                p rng)
    member _.TryFinally (Property body : Property<'a>, compensation : unit -> unit) : Property<'a> =
        Property (fun rng ->
            try body rng
            finally compensation ())
    member _.Using (resource : 'a, k : 'a -> Property<'b>) : Property<'b> when 'a :> System.IDisposable =
        Property (fun rng ->
            use r = resource
            let (Property p) = k r
            p rng)

[<AutoOpen>]
module Builders =
    let gen = GenBuilder ()
    let property = PropertyBuilder ()
