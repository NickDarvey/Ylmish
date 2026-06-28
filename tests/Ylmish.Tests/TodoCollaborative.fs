module Ylmish.TodoCollaborative

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

open TodoCollaborative

let private ids (todos : Todo list) = todos |> List.map (fun t -> t.Id)
let private texts (m : TodoModel) = TodoModel.ordered m |> List.map (fun t -> t.Text)

/// Fold a sequence of messages over the initial model — the Elmish loop, run.
let private run (msgs : Msg list) : TodoModel =
    msgs |> List.fold (fun m msg -> TodoModel.update msg m) TodoModel.init

let tests = testList "TodoCollaborative" [

    // Step 0 — the pure Elmish loop: model + update + fractional-index ordering,
    // no Ylmish. These pin the readable core before any sync is involved.
    testList "update (pure Elmish loop)" [

        test "Add appends items, in priority order, clearing the new-item box" {
            let m = run [ SetNewItem "a"; Add "1"; SetNewItem "b"; Add "2"; SetNewItem "c"; Add "3" ]
            Expect.equal (TodoModel.ordered m |> ids) [ "1"; "2"; "3" ] "added items keep insertion order"
            Expect.equal (texts m) [ "a"; "b"; "c" ] "each item carries its text"
            Expect.equal m.NewItem "" "the new-item box is cleared after Add"
        }

        test "Move to the front reorders by fractional key" {
            let m = run [ SetNewItem "a"; Add "1"; SetNewItem "b"; Add "2"; SetNewItem "c"; Add "3" ]
            // Place 3 before 1 (no prev neighbour, next = 1).
            let m = TodoModel.update (Move ("3", None, Some "1")) m
            Expect.equal (TodoModel.ordered m |> ids) [ "3"; "1"; "2" ] "3 moved to the front"
        }

        test "Move between two items" {
            let m = run [ SetNewItem "a"; Add "1"; SetNewItem "b"; Add "2"; SetNewItem "c"; Add "3" ]
            // Place 1 between 2 and 3.
            let m = TodoModel.update (Move ("1", Some "2", Some "3")) m
            Expect.equal (TodoModel.ordered m |> ids) [ "2"; "1"; "3" ] "1 moved between 2 and 3"
        }

        test "Toggle flips done; visible respects the filter" {
            let m = run [ SetNewItem "a"; Add "1"; SetNewItem "b"; Add "2" ]
            let m = TodoModel.update (Toggle "1") m
            Expect.equal (TodoModel.update (SetFilter Active) m |> TodoModel.visible |> ids) [ "2" ]
                "Active hides the done item"
            Expect.equal (TodoModel.update (SetFilter Completed) m |> TodoModel.visible |> ids) [ "1" ]
                "Completed shows only the done item"
        }

        test "Edit changes text; Remove drops the item" {
            let m = run [ SetNewItem "a"; Add "1"; SetNewItem "b"; Add "2" ]
            let m = TodoModel.update (Edit ("1", "aa")) m
            let m = TodoModel.update (Remove "2") m
            Expect.equal (TodoModel.ordered m |> ids) [ "1" ] "item 2 removed"
            Expect.equal (texts m) [ "aa" ] "item 1 edited"
        }
    ]

    // Step 3 — the app is genuinely collaborative end-to-end under withYlmish.
    testList "collaborative (two withYlmish peers)" [

        let dispatch (d : Elmish.Program.ElmishDispatcher<_, _>) (msg : Msg) =
            d.Dispatch (Ylmish.Program.Message.User msg)
        let orderedIds (d : Elmish.Program.ElmishDispatcher<_, _>) =
            TodoModel.ordered d.Model |> List.map (fun t -> t.Id)
        let todo (d : Elmish.Program.ElmishDispatcher<_, _>) id =
            TodoModel.ordered d.Model |> List.tryFind (fun t -> t.Id = id)
        let exchange (a : Y.Doc) (b : Y.Doc) = Main.sync a b; Main.sync b a

        // A two-peer fixture sharing a base list, synced.
        let mkPair () =
            let d1, d2 = Y.Doc.Create (), Y.Doc.Create ()
            Main.makeProgram d1 |> Elmish.Program.test,
            Main.makeProgram d2 |> Elmish.Program.test,
            d1, d2

        yield test "concurrent adds: both todos survive in both models (element-wise, not LWW)" {
            let p1, p2, d1, d2 = mkPair ()
            use _ = p1
            use _ = p2
            dispatch p1 (SetNewItem "milk"); dispatch p1 (Add "1")
            dispatch p2 (SetNewItem "eggs"); dispatch p2 (Add "2")
            exchange d1 d2
            Expect.equal (orderedIds p1) (orderedIds p2) "models converge"
            Expect.equal (List.sort (orderedIds p1)) [ "1"; "2" ] "both concurrent adds survive"
        }

        yield test "concurrent edits to the same todo's text merge character-wise" {
            let p1, p2, d1, d2 = mkPair ()
            use _ = p1
            use _ = p2
            dispatch p1 (SetNewItem "hi"); dispatch p1 (Add "1")
            exchange d1 d2   // both peers now have todo 1, text "hi"
            dispatch p1 (Edit ("1", "hiA"))
            dispatch p2 (Edit ("1", "hiB"))
            exchange d1 d2
            let t1 = todo p1 "1" |> Option.map (fun t -> t.Text) |> Option.defaultValue ""
            Expect.equal (todo p1 "1" |> Option.map (fun t -> t.Text)) (todo p2 "1" |> Option.map (fun t -> t.Text))
                "text converges"
            Expect.isTrue (t1.Contains "A" && t1.Contains "B") "both edits merged (CRDT, not LWW)"
        }

        yield test "concurrent toggles of different todos both stick" {
            let p1, p2, d1, d2 = mkPair ()
            use _ = p1
            use _ = p2
            dispatch p1 (SetNewItem "a"); dispatch p1 (Add "1")
            dispatch p1 (SetNewItem "b"); dispatch p1 (Add "2")
            exchange d1 d2
            dispatch p1 (Toggle "1")
            dispatch p2 (Toggle "2")
            exchange d1 d2
            Expect.equal (todo p1 "1" |> Option.map (fun t -> t.Done)) (Some true) "1 completed (peer 1)"
            Expect.equal (todo p1 "2" |> Option.map (fun t -> t.Done)) (Some true) "2 completed (peer 2)"
            Expect.equal (List.map (fun t -> t.Id, t.Done) (TodoModel.ordered p1.Model))
                         (List.map (fun t -> t.Id, t.Done) (TodoModel.ordered p2.Model)) "models converge"
        }

        yield test "concurrent reorders of different todos converge" {
            let p1, p2, d1, d2 = mkPair ()
            use _ = p1
            use _ = p2
            dispatch p1 (SetNewItem "a"); dispatch p1 (Add "1")
            dispatch p1 (SetNewItem "b"); dispatch p1 (Add "2")
            dispatch p1 (SetNewItem "c"); dispatch p1 (Add "3")
            exchange d1 d2   // both: [1;2;3]
            dispatch p1 (Move ("3", None, Some "1"))     // peer 1 moves 3 to the front
            dispatch p2 (Move ("1", Some "3", None))     // peer 2 moves 1 to the end
            exchange d1 d2
            Expect.equal (orderedIds p1) (orderedIds p2) "the priority order converges across peers"
            Expect.equal (List.sort (orderedIds p1)) [ "1"; "2"; "3" ] "no item lost in the reorder"
        }
    ]

    // Step 4 — the codec is the schema-migration boundary. v2 renamed the
    // completion flag `done` -> `completed`. There is no single migration moment in
    // a CRDT doc, so the codec reads both shapes and dual-writes both keys, letting
    // v1 and v2 peers coexist on the same live document.
    testList "schema evolution (v1/v2 coexistence)" [

        let dispatch (d : Elmish.Program.ElmishDispatcher<_, _>) (msg : Msg) =
            d.Dispatch (Ylmish.Program.Message.User msg)
        let todoDone (d : Elmish.Program.ElmishDispatcher<_, _>) id =
            TodoModel.ordered d.Model |> List.tryFind (fun t -> t.Id = id) |> Option.map (fun t -> t.Done)

        // Author a doc directly with given collection items (simulating a peer on a
        // particular schema), connected so its keyed Y.Map is populated.
        let mkRawDoc (items : CollectionItem list) =
            let cl = clist items
            let merged = cval ([] : CollectionItem list)
            let enc = Encode.object [ "todos", Encode.collection [ "text" ] (fun it -> AVal.constant it) merged (cl :> alist<_>) ]
            let doc = Y.Doc.Create ()
            Y.Doc.connect doc enc |> ignore
            doc

        let v1Item id (isDone : bool) order text : CollectionItem =
            { Id = id; Fields = [ "done", (if isDone then "true" else "false"); "order", order ]; Texts = [ "text", text ] }

        // A v1-authored doc (only the old `done` key) loads correctly in v2.
        yield test "a v1-authored doc loads in v2 (decoder falls back to the old key)" {
            let v1 = mkRawDoc [ v1Item "1" true "a0" "legacy" ]
            let d2 = Y.Doc.Create ()
            use p2 = Main.makeProgram d2 |> Elmish.Program.test
            Main.sync v1 d2
            Expect.equal (todoDone p2 "1") (Some true)
                "v2 reads the v1 'done' field via the decoder's fallback"
        }

        // A v2 peer dual-writes both keys, so a still-running v1 peer keeps seeing
        // the completion state after the rename.
        yield test "a v2 peer dual-writes 'done' and 'completed' (so v1 peers still read it)" {
            let d = Y.Doc.Create ()
            use p = Main.makeProgram d |> Elmish.Program.test
            dispatch p (SetNewItem "x"); dispatch p (Add "1"); dispatch p (Toggle "1")
            let keys = ResizeArray<string> ()
            (d.getMap "todos").forEach (fun _ k _ -> keys.Add k)
            Expect.isTrue (Seq.contains "1/completed" keys) "writes the v2 key 'completed'"
            Expect.isTrue (Seq.contains "1/done" keys) "also writes the v1 key 'done' for backward compatibility"
        }

        // When both keys are present but disagree, v2 prefers its own.
        yield test "the decoder prefers the v2 key when both are present" {
            let weird : CollectionItem =
                { Id = "1"; Fields = [ "done", "false"; "completed", "true"; "order", "a0" ]; Texts = [ "text", "x" ] }
            let src = mkRawDoc [ weird ]
            let d2 = Y.Doc.Create ()
            use p2 = Main.makeProgram d2 |> Elmish.Program.test
            Main.sync src d2
            Expect.equal (todoDone p2 "1") (Some true) "v2's 'completed' wins over the legacy 'done'"
        }
    ]
]
