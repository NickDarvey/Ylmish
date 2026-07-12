module Ylmish.Tests.Program

// Plan 0002, Step 7 — withYlmish on the binding runtime. The v1 suite (which
// pinned materialize-per-update semantics, eager persistence included) died
// with that path; these tests pin the v2 contract:
//
//   - init decodes existing doc state through the consumer's decoder, and an
//     empty doc decodes to the init model with NOTHING written (decode-empty
//     = init);
//   - one Elmish update = one origin-tagged Y transaction, however many
//     fields changed (the write-side U14);
//   - each remote transaction dispatches exactly one Set, and own writes
//     never echo;
//   - decode failures and schema drift go to OnError and the loop survives;
//   - the dual-key migration recipe works with plain codec combinators, and
//     mixed-schema clients never destroy each other's representation (U15).

open System

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Codec
open Ylmish.Internal

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

// --- A small consumer model ---------------------------------------------------

type Model = {
    Name : string
    Body : Text
    Local : string   // app-only: never encoded
}

module Model =
    let init = { Name = ""; Body = Text.empty; Local = "" }

type Msg =
    | SetName of string
    | EditBody of (Text -> Text)
    | SetLocal of string
    | Multi of string * (Text -> Text)   // touches two encoded fields at once

let update msg m =
    match msg with
    | SetName v -> { m with Name = v }, Elmish.Cmd.none
    | EditBody f -> { m with Body = f m.Body }, Elmish.Cmd.none
    | SetLocal v -> { m with Local = v }, Elmish.Cmd.none
    | Multi (n, f) -> { m with Name = n; Body = f m.Body }, Elmish.Cmd.none

type AdaptiveModel (m : Model) =
    let name = cval m.Name
    let body = cval m.Body
    member _.Name = name :> aval<string>
    member _.Body = body :> aval<Text>
    member _.Update (m : Model) =
        name.Value <- m.Name
        body.Value <- m.Body

let private encode (am : AdaptiveModel) : Encoded =
    Encode.object [
        "name", Encode.string am.Name
        "body", Encode.text am.Body
    ]

let private decode : Decoder<Model, Model> =
    Decode.object {
        let! model = Decode.ask
        let! name = Decode.object.optional "name" Decode.string
        let! body = Decode.object.optional "body" Decode.text
        return
            { model with
                Name = defaultArg name model.Name
                Body = defaultArg body model.Body }
    }

let private makeProgramWith (onError : Ylmish.Program.OnError) (doc : Y.Doc) =
    Elmish.Program.mkProgram (fun () -> Model.init, Elmish.Cmd.none) update (fun _ _ -> ())
    |> Ylmish.Program.withYlmish {
        Doc = doc
        Create = AdaptiveModel
        Update = fun (am : AdaptiveModel) m -> am.Update m
        Encode = encode
        Decode = decode
        OnError = onError
    }

let private makeProgram doc = makeProgramWith Ylmish.Program.OnError.log doc

let private user msg = Ylmish.Program.Message.User msg

let private sync (src : Y.Doc) (dst : Y.Doc) =
    Y.applyUpdate (dst, Y.encodeStateAsUpdate src, box "test-remote")

let tests = testList "Program (withYlmish v2)" [

    test "init on an empty doc is the init model, and writes nothing" {
        let doc = Y.Doc.Create ()
        use p = Elmish.Program.test (makeProgram doc)
        Expect.equal p.Model Model.init "decode-empty = init"
        Expect.isEmpty ((doc.getMap () : Y.Map<obj>).keys () |> Seq.toList)
            "no eager writes at startup"
    }

    test "init on a doc with existing state restores it through the decoder" {
        let d1 = Y.Doc.Create ()
        let seed () =
            use p1 = Elmish.Program.test (makeProgram d1)
            p1.Dispatch (user (SetName "restored"))
            p1.Dispatch (user (EditBody (Text.edit "existing text")))
        seed ()

        // A brand-new program over the same doc starts from the doc's state.
        use p2 = Elmish.Program.test (makeProgram d1)
        Expect.equal p2.Model.Name "restored" "register restored at init"
        Expect.equal (Text.toString p2.Model.Body) "existing text" "text restored at init"
        Expect.equal p2.Model.Local "" "app-only fields come from init (via ask)"
    }

    test "one Elmish update = one origin-tagged Y transaction, however many fields changed" {
        let doc = Y.Doc.Create ()
        let mutable transactions = 0
        use _all = Binding.subscribe doc (box (obj ())) (fun () -> transactions <- transactions + 1)
        use p = Elmish.Program.test (makeProgram doc)

        p.Dispatch (user (Multi ("both", Text.edit "fields")))
        Expect.equal transactions 1 "the register write and the text splice shared one transaction"
        Expect.equal ((doc.getMap () : Y.Map<obj>).get "name" |> Option.map string) (Some "both") "register landed"
        Expect.equal ((doc.getText "body" : Y.Text).toString ()) "fields" "text landed"
    }

    test "a remote transaction dispatches one Set; own writes never echo" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (makeProgram d1)
        use p2 = Elmish.Program.test (makeProgram d2)

        p1.Dispatch (user (SetName "from-p1"))
        Expect.equal p2.Model.Name "" "nothing crossed the wire yet"
        sync d1 d2
        Expect.equal p2.Model.Name "from-p1" "the remote change decoded into p2's model"

        // App-only state must survive remote folds (ask), and never sync.
        p2.Dispatch (user (SetLocal "only-here"))
        p1.Dispatch (user (EditBody (Text.edit "hi")))
        sync d1 d2
        Expect.equal (Text.toString p2.Model.Body) "hi" "remote text arrived"
        Expect.equal p2.Model.Local "only-here" "app-only state survived the remote Set (ask)"
        Expect.isFalse ((d2.getMap () : Y.Map<obj>).has "local") "app-only state never synced"
    }

    test "a decode failure goes to OnError and the model is kept; the loop survives" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        let errors = ResizeArray<Error list> ()
        use p2 = Elmish.Program.test (makeProgramWith { Handle = errors.Add } d2)
        use p1 = Elmish.Program.test (makeProgram d1)

        p1.Dispatch (user (SetName "good"))
        sync d1 d2
        Expect.equal p2.Model.Name "good" "healthy path works"

        // A foreign client poisons the register with the wrong primitive.
        Y.transact (d1, (fun _ -> (d1.getMap () : Y.Map<obj>).set ("name", box 42) |> ignore), box "v99")
        sync d1 d2
        Expect.equal errors.Count 1 "the failure surfaced through OnError"
        Expect.equal p2.Model.Name "good" "the model was kept"

        // Healing write: the loop is alive and decodes again.
        Y.transact (d1, (fun _ -> (d1.getMap () : Y.Map<obj>).set ("name", box "healed") |> ignore), box "v99")
        sync d1 d2
        Expect.equal p2.Model.Name "healed" "the loop survived the failure"
    }

    test "unknown keys survive a whole session (U15)" {
        let doc = Y.Doc.Create ()
        (doc.getMap () : Y.Map<obj>).set ("someone-elses", box "v1-data") |> ignore
        use p = Elmish.Program.test (makeProgram doc)
        p.Dispatch (user (SetName "mine"))
        p.Dispatch (user (EditBody (Text.edit "mine too")))
        Expect.equal ((doc.getMap () : Y.Map<obj>).get "someone-elses" |> Option.map string) (Some "v1-data")
            "the encoder never touches keys it does not mention"
    }

    // --- The dual-key migration recipe, with plain combinators (no module) -----

    test "dual-key migration: v1 and v2 schemas coexist on one doc without destroying each other" {
        // v1 schema: a single "title" register.
        // v2 schema: renames it to "heading"; reads new-or-old, writes BOTH.
        let mkV1 (doc : Y.Doc) =
            let title = cval ""
            let enc = Encode.object [ "title", Encode.string title ]
            let att = Binding.attach doc enc
            title, enc, att
        // mkV2 and decodeV2 are quoted verbatim by doc/guides/recipes.md.
        let mkV2 (doc : Y.Doc) =
            let heading = cval ""
            let enc =
                Encode.object [
                    "heading", Encode.string heading   // the new shape
                    "title", Encode.string heading     // dual-write for v1 readers
                ]
            let att = Binding.attach doc enc
            heading, enc, att
        let decodeV2 : Decoder<string, string> =
            Decode.object {
                let! newKey = Decode.object.optional "heading" Decode.string
                let! oldKey = Decode.object.optional "title" Decode.string
                // Read new-or-old, prefer new.
                return
                    match newKey, oldKey with
                    | Some h, _ -> h
                    | None, Some t -> t
                    | None, None -> ""
            }
        let decodeV1 : Decoder<string, string> =
            Decode.object {
                let! t = Decode.object.optional "title" Decode.string
                return defaultArg t ""
            }

        let d1 = Y.Doc.Create ()   // the v1 client
        let d2 = Y.Doc.Create ()   // the v2 client
        let title1, encV1, _a1 = mkV1 d1
        let heading2, encV2, _a2 = mkV2 d2

        // The v1 client writes its shape; the v2 client reads it via fallback.
        transact (fun () -> title1.Value <- "from v1")
        sync d1 d2
        Expect.equal (Decode.runElement "" decodeV2 (Binding.read d2 encV2)) (Ok "from v1")
            "v2 reads the old shape through the fallback"

        // The v2 client writes; the v1 client still reads its own key.
        transact (fun () -> heading2.Value <- "from v2")
        sync d2 d1
        Expect.equal (Decode.runElement "" decodeV1 (Binding.read d1 encV1)) (Ok "from v2")
            "v1 keeps working because v2 dual-writes the old key"
        Expect.equal ((d1.getMap () : Y.Map<obj>).get "heading" |> Option.map string) (Some "from v2")
            "and v1 never deletes the key it does not understand (U15)"
    }
]
