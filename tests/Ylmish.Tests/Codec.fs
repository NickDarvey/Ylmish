module Ylmish.Tests.Codec

// Plan 0002, Step 4 — the v2 codec, specified with zero Yjs runtime
// involvement: encodings are snapshotted to the internal Element tree
// (Element.ofEncoded, via InternalsVisibleTo) and decoded back. The binding
// runtime (Steps 5/6) replaces the snapshot with live doc state; these tests
// pin the schema semantics either way.
//
// The L1 restriction is TYPE-LEVEL, so there is no runtime test for it; this
// is the should-not-compile record:
//
//     Encode.list Encode.text texts      // ✗ Encode.text : aval<Text> -> Encoded
//                                        //   is not a Value.Encoder<'a>
//     Encode.list (fun t -> Encode.object []) xs   // ✗ same reason
//
// There is no injection from Encoded into Value.Encoder, so lists hold
// primitives only; entities belong in Encode.map (keyed by identity).

open FSharp.Data.Adaptive
open Hedgehog

open Ylmish
open Ylmish.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// ---- helpers -----------------------------------------------------------------

let private decodeVia (model : 'm) (decoder : Decoder<'m, 'r>) (encoded : Encoded) : Result<'r, Error list> =
    match Element.ofEncoded encoded with
    | Some el -> Decode.runElement model decoder el
    | None -> failwith "top-level encoding was absent"

let private ok (r : Result<'r, Error list>) : 'r =
    match r with
    | Ok v -> v
    | Error e -> failwithf "decode failed: %A" e

/// A domain type riding a string primitive — the contramap/map path.
type TodoId = TodoId of string

module TodoId =
    let valueEncoder = Value.Encode.contramap (fun (TodoId s) -> s) Value.Encode.string
    let valueDecoder = Value.Decode.map TodoId Value.Decode.string

let private fakeCustom (v : obj) : CustomElement =
    { new CustomElement with
        member _.Connect _ = { new System.IDisposable with member _.Dispose () = () }
        member _.Value = v }

// A consumer-shaped record for object/ask tests.
type private Rec = { A : string; B : bool; Keep : int }

let tests = testList "Codec (v2)" [

    testList "Value sub-language (4a)" [
        test "primitives round-trip" {
            Property.check <| property {
                let! i = Gen.int32 (Range.linear -100000 100000)
                let! b = Gen.int32 (Range.linear 0 1)
                let! s = Gen.string (Range.linear 0 12) Gen.alphaNum
                let f = float i / 128.0
                let b = b = 1
                return
                    (decodeVia () Decode.int (Encode.int (AVal.constant i)) |> ok) = i
                    && (decodeVia () Decode.float (Encode.float (AVal.constant f)) |> ok) = f
                    && (decodeVia () Decode.bool (Encode.bool (AVal.constant b)) |> ok) = b
                    && (decodeVia () Decode.string (Encode.string (AVal.constant s)) |> ok) = s
            }
        }

        test "a domain type rides a primitive via contramap/map" {
            let e = Encode.value TodoId.valueEncoder (AVal.constant (TodoId "id-7"))
            Expect.equal (decodeVia () (Decode.value TodoId.valueDecoder) e |> ok) (TodoId "id-7")
                "TodoId round-trips through a string primitive"
        }

        test "int refuses a non-integral number, with the path" {
            let e = Encode.object [ "n", Encode.float (AVal.constant 1.5) ]
            let d = Decode.object { let! n = Decode.object.required "n" Decode.int in return n }
            match decodeVia () d e with
            | Error [ UnexpectedValue (path, _) ] ->
                Expect.equal path [ ObjectKey "n" ] "the error names where it happened"
            | r -> failwithf "expected one UnexpectedValue, got %A" r
        }
    ]

    testList "objects and ask (4c)" [
        test "object round-trips and ask preserves app-only fields" {
            let current = { A = ""; B = false; Keep = 42 }
            let e =
                Encode.object [
                    "a", Encode.string (AVal.constant "hello")
                    "b", Encode.bool (AVal.constant true)
                ]
            let d = Decode.object {
                let! model = Decode.ask
                let! a = Decode.object.required "a" Decode.string
                let! b = Decode.object.optional "b" Decode.bool
                return { model with A = a; B = defaultArg b false }
            }
            let result = decodeVia current d e |> ok
            Expect.equal result { A = "hello"; B = true; Keep = 42 }
                "encoded fields decode; the app-only field survives via ask"
        }

        test "a missing required key is a MissingProperty at its path" {
            let e = Encode.object []
            let d = Decode.object { let! a = Decode.object.required "a" Decode.string in return a }
            match decodeVia () d e with
            | Error [ MissingProperty path ] -> Expect.equal path [ ObjectKey "a" ] "path names the key"
            | r -> failwithf "expected MissingProperty, got %A" r
        }

        test "a missing optional key decodes to None; decode-empty = init falls out" {
            let d = Decode.object {
                let! a = Decode.object.optional "a" Decode.string
                return defaultArg a "init"
            }
            Expect.equal (decodeVia () d (Encode.object []) |> ok) "init"
                "an empty doc decodes through the same decoder into the init value"
        }
    ]

    testList "lists of values (4c)" [
        test "lists round-trip in order" {
            Property.check <| property {
                let! items = Gen.list (Range.linear 0 8) (Gen.string (Range.linear 0 6) Gen.alphaNum)
                let e = Encode.list Value.Encode.string (AList.ofList items)
                let decoded = decodeVia () (Decode.list Value.Decode.string) e |> ok
                return IndexList.toList decoded = items
            }
        }

        test "item errors accumulate, each with its index" {
            let e = Encode.list Value.Encode.string (AList.ofList [ "x"; "y" ])
            match decodeVia () (Decode.list Value.Decode.int) e with
            | Error [ UnexpectedValue (p0, _); UnexpectedValue (p1, _) ] ->
                Expect.equal p0 [ ArrayIndex 0 ] "first item's path"
                Expect.equal p1 [ ArrayIndex 1 ] "second item's path"
            | r -> failwithf "expected two indexed errors, got %A" r
        }
    ]

    testList "keyed maps (4d)" [
        test "maps of objects round-trip by key" {
            let items =
                HashMap.ofList [
                    "id-1", ("buy milk", true)
                    "id-2", ("walk dog", false)
                ]
            let encodeItem (title : string, don : bool) =
                Encode.object [
                    "title", Encode.string (AVal.constant title)
                    "done", Encode.bool (AVal.constant don)
                ]
            let decodeItem = Decode.object {
                let! title = Decode.object.required "title" Decode.string
                let! don = Decode.object.required "done" Decode.bool
                return (title, don)
            }
            let e = Encode.map encodeItem (AMap.ofHashMap items)
            Expect.equal (decodeVia () (Decode.map decodeItem) e |> ok) items
                "element-wise round-trip, keyed by the map key"
        }

        test "a bad item error carries its key in the path" {
            let e = Encode.map (fun (v : string) -> Encode.string (AVal.constant v)) (AMap.ofList [ "k1", "v" ])
            match decodeVia () (Decode.map Decode.int) e with
            | Error [ UnexpectedValue (path, _) ] ->
                Expect.equal path [ MapKey "k1" ] "path names the item's key"
            | r -> failwithf "expected one keyed error, got %A" r
        }
    ]

    testList "text (4d)" [
        test "text round-trips content and decodes intent-free" {
            let t = Text.ofString "hello" |> Text.insert 5 "!"
            let e = Encode.text (AVal.constant t)
            let decoded = decodeVia () Decode.text e |> ok
            Expect.equal (Text.toString decoded) "hello!" "content round-trips"
            Expect.equal decoded t "content-only equality"
        }

        test "decoding text where a value sits is an UnexpectedKind" {
            let e = Encode.object [ "body", Encode.string (AVal.constant "plain") ]
            let d = Decode.object { let! b = Decode.object.required "body" Decode.text in return b }
            match decodeVia () d e with
            | Error [ UnexpectedKind (path, _) ] -> Expect.equal path [ ObjectKey "body" ] "path names the field"
            | r -> failwithf "expected UnexpectedKind, got %A" r
        }
    ]

    testList "option (4d)" [
        test "Some is present, None is an absent key — and text composes" {
            let enc (v : Text option) =
                Encode.object [ "note", Encode.option Encode.text (AVal.constant v) ]
            let d = Decode.object { return! Decode.object.optional "note" Decode.text }

            let some = decodeVia () d (enc (Some (Text.ofString "hi"))) |> ok
            Expect.equal (some |> Option.map Text.toString) (Some "hi") "Some round-trips"

            let none = decodeVia () d (enc None) |> ok
            Expect.isNone none "None means the key is absent, not null"
        }
    ]

    testList "atomic (4d)" [
        test "atomic round-trips as the inner codec" {
            let e =
                Encode.atomic (Encode.object [
                    "name", Encode.string (AVal.constant "nick")
                    "bio", Encode.string (AVal.constant "fsharp")
                ])
            let inner = Decode.object {
                let! name = Decode.object.required "name" Decode.string
                let! bio = Decode.object.required "bio" Decode.string
                return (name, bio)
            }
            Expect.equal (decodeVia () (Decode.atomic inner) e |> ok) ("nick", "fsharp")
                "the subtree decodes with the unchanged inner decoder"
        }
    ]

    testList "custom (4e)" [
        test "Decode.custom reads the binding's merged value" {
            let e = Encode.object [ "hits", Encode.custom (fakeCustom (box 42)) ]
            let d = Decode.object { return! Decode.object.required "hits" Decode.custom }
            Expect.equal (decodeVia () d e |> ok) 42 "the boxed value surfaces, typed"
        }
    ]

    testList "nested composition (4e)" [
        test "deep paths report from the outside in" {
            // todos["id-1"].tags[0] holds a string; decode it as an int.
            let e =
                Encode.object [
                    "todos", Encode.map (fun (tags : string list) ->
                        Encode.object [ "tags", Encode.list Value.Encode.string (AList.ofList tags) ])
                        (AMap.ofList [ "id-1", [ "urgent" ] ])
                ]
            let d = Decode.object {
                return! Decode.object.required "todos" (Decode.map (Decode.object {
                    return! Decode.object.required "tags" (Decode.list Value.Decode.int)
                }))
            }
            match decodeVia () d e with
            | Error [ UnexpectedValue (path, _) ] ->
                Expect.equal path [ ArrayIndex 0; ObjectKey "tags"; MapKey "id-1"; ObjectKey "todos" ]
                    "the path walks object → map key → field → index (innermost first)"
            | r -> failwithf "expected one deep error, got %A" r
        }
    ]
]
