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
]
