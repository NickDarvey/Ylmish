module Ylmish.Tests.Text

// Plan 0002, Step 3 — Ylmish.Text. Pure value semantics: no Yjs involved.
// The generators here (strings, edit ops) are reused by later steps' binding
// and stress tests.

open FSharp.Data.Adaptive
open Hedgehog

open Ylmish

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// ---- generators (reused by Steps 5/9) ---------------------------------------

/// A random edit operation, with positions deliberately allowed OUT of range so
/// the clamping contract is exercised, not avoided.
type TextOp =
    | OpInsert of at : int * value : string
    | OpRemove of at : int * count : int
    | OpReplace of at : int * count : int * value : string
    | OpEdit of newValue : string

module Gen =
    let smallString = Gen.string (Range.linear 0 12) Gen.alphaNum
    let position = Gen.int32 (Range.linear -3 15)   // intentionally exceeds bounds
    let count = Gen.int32 (Range.linear -2 15)

    let textOp : Gen<TextOp> = gen {
        let! tag = Gen.int32 (Range.linear 0 3)
        match tag with
        | 0 ->
            let! at = position
            let! value = smallString
            return OpInsert (at, value)
        | 1 ->
            let! at = position
            let! count = count
            return OpRemove (at, count)
        | 2 ->
            let! at = position
            let! count = count
            let! value = smallString
            return OpReplace (at, count, value)
        | _ ->
            let! value = smallString
            return OpEdit value
    }

    let textOps = Gen.list (Range.linear 0 8) textOp

let applyOp (t : Text) (op : TextOp) : Text =
    match op with
    | OpInsert (at, value) -> Text.insert at value t
    | OpRemove (at, count) -> Text.remove at count t
    | OpReplace (at, count, value) -> Text.replace at count value t
    | OpEdit value -> Text.edit value t

/// The reference implementation: the same op applied to a plain string with the
/// same clamping rules. Text must agree with this on content, always.
let applyOpToString (s : string) (op : TextOp) : string =
    let clamp lo hi v = max lo (min hi v)
    let splice (at : int) (count : int) (value : string) =
        let at = clamp 0 s.Length at
        let count = clamp 0 (s.Length - at) count
        s.Substring (0, at) + value + s.Substring (at + count)
    match op with
    | OpInsert (at, value) -> splice at 0 value
    | OpRemove (at, count) -> splice at count ""
    | OpReplace (at, count, value) -> splice at count value
    | OpEdit value -> value

let tests = testList "Text" [

    testList "content semantics" [
        test "ofString/toString round-trip; ToString mirrors toString" {
            let t = Text.ofString "hello"
            Expect.equal (Text.toString t) "hello" "round-trips"
            Expect.equal (string t) "hello" "ToString is the content"
            Expect.equal (Text.length t) 5 "length is content length"
            Expect.equal (Text.toString Text.empty) "" "empty is empty"
        }

        test "content agrees with the plain-string reference for any op sequence" {
            Property.check <| property {
                let! initial = Gen.smallString
                let! ops = Gen.textOps
                let text = ops |> List.fold applyOp (Text.ofString initial)
                let expected = ops |> List.fold applyOpToString initial
                return Text.toString text = expected
            }
        }

        test "bounds are clamped, not thrown" {
            let t = Text.ofString "abc"
            Expect.equal (Text.insert -5 "x" t |> Text.toString) "xabc" "negative insert clamps to 0"
            Expect.equal (Text.insert 99 "x" t |> Text.toString) "abcx" "past-end insert appends"
            Expect.equal (Text.remove 1 99 t |> Text.toString) "a" "over-long remove clamps to end"
            Expect.equal (Text.remove -1 1 t |> Text.toString) "bc" "negative remove clamps to 0"
            Expect.equal (Text.replace 2 99 "Z" t |> Text.toString) "abZ" "replace clamps count"
        }
    ]

    testList "equality is by content only" [
        test "intent-free and intent-carrying values with equal content are equal" {
            Property.check <| property {
                let! initial = Gen.smallString
                let! ops = Gen.textOps
                let text = ops |> List.fold applyOp (Text.ofString initial)
                let plain = Text.ofString (Text.toString text)
                return text = plain
                       && text.GetHashCode () = plain.GetHashCode ()
                       && compare text plain = 0
            }
        }

        test "comparison orders by content" {
            Expect.isTrue (Text.ofString "a" < Text.ofString "b") "a < b"
            Expect.equal
                ([ "c"; "a"; "b" ] |> List.map Text.ofString |> List.sort |> List.map Text.toString)
                [ "a"; "b"; "c" ]
                "sorting a list of Text sorts by content"
        }
    ]

    testList "intent (pending splices)" [
        test "replaying pending intents over the base content reproduces the content" {
            Property.check <| property {
                let! initial = Gen.smallString
                let! ops = Gen.textOps
                let text = ops |> List.fold applyOp (Text.ofString initial)
                let replayed = Text.pending text |> List.fold Text.applySplice initial
                return replayed = Text.toString text
            }
        }

        test "drain clears intent and preserves content and equality" {
            let t = Text.ofString "ab" |> Text.insert 2 "c"
            let d = Text.drain t
            Expect.isEmpty (Text.pending d) "drained"
            Expect.equal (Text.toString d) "abc" "content preserved"
            Expect.equal d t "drained value is equal (content-only equality)"
        }

        test "content-neutral edits are elided (documented consequence of content equality)" {
            let t = Text.ofString "a" |> Text.replace 0 1 "a"
            Expect.isEmpty (Text.pending t) "no intent recorded"
            let e = Text.ofString "ab" |> Text.edit "ab"
            Expect.isEmpty (Text.pending e) "edit to the same value records nothing"
        }
    ]

    testList "edit (affix diff)" [
        test "edit produces the new value and at most one splice" {
            Property.check <| property {
                let! oldValue = Gen.smallString
                let! newValue = Gen.smallString
                let t = Text.edit newValue (Text.ofString oldValue)
                let p = Text.pending t
                return Text.toString t = newValue && List.length p <= 1
            }
        }

        test "the L3 pin: a one-character change is a one-character splice" {
            // "hello" -> "hełlo": 2 Y ops (delete 1, insert 1), not clear+reinsert.
            let t = Text.edit "hełlo" (Text.ofString "hello")
            match Text.pending t with
            | [ s ] ->
                Expect.equal s.At 2 "edit starts after the common prefix 'he'"
                Expect.equal s.Removed 1 "removes exactly the one differing char"
                Expect.equal s.Inserted "ł" "inserts exactly the one new char"
            | p -> failwithf "expected exactly one splice, got %d" (List.length p)
        }

        test "the splice is affix-minimal: it neither starts nor ends with a kept character" {
            Property.check <| property {
                let! oldValue = Gen.smallString
                let! newValue = Gen.smallString
                let t = Text.edit newValue (Text.ofString oldValue)
                match Text.pending t with
                | [] -> return oldValue = newValue
                | [ s ] ->
                    let removed = oldValue.Substring (s.At, s.Removed)
                    let firstsDiffer =
                        removed = "" || s.Inserted = "" || removed.[0] <> s.Inserted.[0]
                    let lastsDiffer =
                        removed = "" || s.Inserted = ""
                        || removed.[removed.Length - 1] <> s.Inserted.[s.Inserted.Length - 1]
                    return firstsDiffer && lastsDiffer
                | _ -> return false
            }
        }
    ]

    testList "Adaptify round-trip" [
        test "a [<ModelType>] with a Text field generates a working adaptive wrapper" {
            let m : Example.TextModel = { Body = Text.ofString "hi" }
            let am = Example.AdaptiveTextModel.Create m
            Expect.equal (AVal.force am.Body) m.Body "initial value flows"

            let next : Example.TextModel = { Body = Text.insert 2 "!" m.Body }
            transact (fun () -> am.Update next)
            Expect.equal
                (AVal.force am.Body |> Text.toString) "hi!"
                "a content-changing update propagates through the adaptive graph"
        }
    ]
]
