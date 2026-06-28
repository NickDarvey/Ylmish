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

    // The model rides through withYlmish and round-trips the persisted shape.
    // (Step 0's codec is structural; element-wise collaborative merge is Step 3.)
    test "withYlmish persists a todo (structural round-trip)" {
        let doc = Y.Doc.Create ()
        use disp = Main.makeProgram doc |> Elmish.Program.test
        disp.Dispatch (Ylmish.Program.Message.User (SetNewItem "Buy eggs"))
        disp.Dispatch (Ylmish.Program.Message.User (Add "1"))
        Expect.equal (texts disp.Model) [ "Buy eggs" ] "the model holds the added item"
        let element = Y.Doc.dematerialize doc
        match Codec.decode disp.Model ([], element) |> AVal.force with
        | Ok result ->
            Expect.equal (result.Todos |> IndexList.toList |> List.map (fun t -> t.Text)) [ "Buy eggs" ]
                "the Y.Doc round-trips the item"
        | Error errors -> failwithf "decode failed: %s" (Error.printAll errors)
    }
]
