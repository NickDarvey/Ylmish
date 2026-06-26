module Ylmish.Codec.Text

// Plan 0002, Step 3 — Encode.text / Decode.text, the 5A diff mirror at the codec
// layer. The model field stays a plain immutable string; Encode.text recovers
// character operations by diffing successive whole-string values into a stable
// clist<char>, and Decode.text reads that clist back as a string. These tests
// prove the issue's core capability (collaborative text) through the codec, with
// no withYlmish yet.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

/// Extract the stable backing clist<char> that Encode.text owns.
let private backingText (enc : Encoded<Element<string>>) : clist<char> =
    match AVal.force enc with
    | Some (Element.Text c) -> c
    | _ -> failwith "expected Element.Text"

/// Force a Decode.text result to a plain string.
let private decodeText (enc : Encoded<Element<string>>) : string =
    Decode.run () Decode.text enc
    |> Decoded.mapError Error.printAll
    |> AVal.force
    |> function
    | Ok s -> s
    | Error e -> invalidOp e

let tests = testList "Ylmish.Codec.Text" [

    test "Encode.text then Decode.text round-trips a string" {
        let s = cval "hello world"
        let enc : Encoded<Element<string>> = Encode.text s
        Expect.equal (decodeText enc) "hello world" "string should survive encode then decode"
    }

    // A5 — a whole-string replacement must produce a MINIMAL delta on the backing
    // clist (and hence the Y.Text), not a full clear+reinsert. This is what lets
    // a concurrent edit elsewhere in the string survive a local edit.
    test "A5: whole-string replacement yields a minimal delta" {
        let s = cval "hello"
        let enc : Encoded<Element<string>> = Encode.text s
        let chars = backingText enc
        Expect.equal (System.String.Concat chars) "hello" "encoder mirrors the initial value"

        let mutable captured = []
        let mutable skipInit = true
        use _ =
            chars.AddCallback (fun (_ : IndexList<char>) (delta : IndexListDelta<char>) ->
                if skipInit then skipInit <- false
                else captured <- delta :: captured)

        transact (fun () -> s.Value <- "hełlo")

        Expect.equal (System.String.Concat chars) "hełlo" "encoder mirrors the new value"
        match captured with
        | [ delta ] ->
            Expect.isTrue (Seq.length delta <= 2)
                $"expected a minimal delta (<=2 ops) for a single-char change, got %i{Seq.length delta}"
        | _ ->
            failwithf "expected exactly one delta batch, got %i" (List.length captured)
    }

    test "Decode.text reflects live edits to the backing text" {
        let s = cval "hello"
        let enc : Encoded<Element<string>> = Encode.text s
        let chars = backingText enc
        let decoded = Decode.run () Decode.text enc |> Decoded.mapError Error.printAll

        Expect.equal (AVal.force decoded) (Ok "hello") "initial decode"
        // Simulate a remote CRDT edit landing on the backing clist (decode dir).
        transact (fun () -> chars.InsertAt (5, '!') |> ignore)
        Expect.equal (AVal.force decoded) (Ok "hello!") "decode recomputes from the live clist"
    }

    // The codec-level headline: two plain-string fields, each encoded with
    // Encode.text and bound to a shared-named Y.Text, interleave on sync.
    test "two Encode.text fields interleave through Y on sync" {
        let s1 = cval ""
        let s2 = cval ""
        let chars1 = backingText (Encode.text s1)
        let chars2 = backingText (Encode.text s2)

        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        let yt1 = d1.getText "body"
        let yt2 = d2.getText "body"
        let _ = Y.Text.attach (ref false) chars1 yt1
        let _ = Y.Text.attach (ref false) chars2 yt2

        // Local edits expressed as whole-string replacements (the MVU way).
        transact (fun () -> s1.Value <- "AAA")
        transact (fun () -> s2.Value <- "BBB")

        // Exchange updates both ways.
        Y.applyUpdate (d2, Y.encodeStateAsUpdate d1)
        Y.applyUpdate (d1, Y.encodeStateAsUpdate d2)

        let r1 = yt1.toString ()
        let r2 = yt2.toString ()
        Expect.equal r1 r2 "docs must converge"
        Expect.isTrue (r1.Contains "AAA" && r1.Contains "BBB")
            "both peers' edits survive through Encode.text + the bridge (minimal diff, no echo)"
    }
]
