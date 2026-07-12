module Ylmish.Tests.Delta

// Pins for Ylmish.Internal.Delta.applyAdaptiveDelta — the binding's list
// machinery (Encode.list ships local list changes as coalesced index
// operations). Specialised here onto a Y.Text (chars) because that makes the
// expected results readable; the binding itself drives it with Y.Array<obj>.

open FSharp.Data.Adaptive
open Yjs

open Ylmish

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

/// The char/Y.Text specialisation the assertions below read naturally in.
let private applyToText (list : IndexList<char>) (delta : IndexListDelta<char>) (y : Y.Text) : unit =
    Internal.Delta.applyAdaptiveDelta
        (fun (y : Y.Text) n s -> y.insert (n, s))
        (fun (cs : char list) -> System.String (Array.ofList cs))
        (fun y i length -> y.delete (i, length))
        list delta y

/// Convenience function to convert (index: int, op) pairs into an IndexList of placeholders and
/// an IndexListDelta<char>.
let private toIndexListDelta list =
    let placeholders: IndexList<char> =
        if List.isEmpty list then
            IndexList.empty
        else
            let maxIndex =
                list
                |> List.maxBy fst
                |> fst

            maxIndex
            |> List.unfold (fun i ->
                if i < 0 then
                  None
                else
                  Some ((string (maxIndex - i))[0], i - 1)
            )
            |> IndexList.ofList

    let delta: IndexListDelta<char> =
        list
        |> List.map (fun (p, op) -> (placeholders.TryGetIndex p).Value, op)
        |> IndexListDelta.ofList

    placeholders, delta

// Test cases from https://docs.yjs.dev/api/delta-format
// https://quilljs.com/docs/delta/#playground
let tests = testList "Delta" [
    testList "applyAdaptiveDelta" [
        test "applyAdaptiveDelta ins 'abc'" {
            let input = [
                0, ElementOperation.Set 'a'
                1, ElementOperation.Set 'b'
                2, ElementOperation.Set 'c'
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "abc" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta ret 2, ins 'abc', ret 2, ins 'efg'" {
            let input = [
                // 0
                // 1
                2, ElementOperation.Set 'a'
                3, ElementOperation.Set 'b'
                4, ElementOperation.Set 'c'
                // 5
                // 6
                7, ElementOperation.Set 'e'
                8, ElementOperation.Set 'f'
                9, ElementOperation.Set 'g'
            ]

            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "0123456789")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "01abc23efg456789" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta del 2" {
            let input = [
                0, ElementOperation.Remove
                1, ElementOperation.Remove
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "0123456789")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "23456789" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta ret 1, del 2" {
            let input = [
                // 0
                1, ElementOperation.Remove
                2, ElementOperation.Remove
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "0123456789")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "03456789" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta ret 2, del 2" {
            let input = [
                // 0
                // 1
                2, ElementOperation.Remove
                3, ElementOperation.Remove
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "0123456789")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "01456789" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta []" {
            let input = []
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "abc")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "abc" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta ins 'abc', ret 2, del 2" {
            let input = [
                0, ElementOperation.Set 'a'
                1, ElementOperation.Set 'b'
                2, ElementOperation.Set 'c'
                // 3
                // 4
                5, ElementOperation.Remove
                6, ElementOperation.Remove
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "0123456789")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "abc01456789" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta del 2, insert 'efg'" {
            let input = [
                0, ElementOperation.Remove
                1, ElementOperation.Remove
                2, ElementOperation.Set 'a'
                3, ElementOperation.Set 'b'
                4, ElementOperation.Set 'c'
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "0123456789")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "abc23456789" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta ins 'abc', ret 2, del 2, insert 'efg'" {
            let input = [
                0, ElementOperation.Set 'a'
                1, ElementOperation.Set 'b'
                2, ElementOperation.Set 'c'
                // 3
                // 4
                5, ElementOperation.Remove
                6, ElementOperation.Remove
                7, ElementOperation.Set 'e'
                8, ElementOperation.Set 'f'
                9, ElementOperation.Set 'g'
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "0123456789")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "abc01efg456789" "ytext doesn't equal expected value"
        }
        test "applyAdaptiveDelta given \"abd\", insert 'c' between the 'b' and the 'd'" {
            let input = [
                2, ElementOperation.Set 'c'
            ]
            let list, delta = toIndexListDelta input

            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "abd")

            applyToText list delta ytext

            Expect.equal (ytext.toString()) "abcd" "ytext doesn't equal expected value"
        }
    ]
 ]
