module Ylmish.TodoCollaborative

// Plan 0002, Step 7 — the example app over withYlmish v2, including issue
// #83's acceptance at the example level: concurrent adds from two peers both
// survive in both Elmish models. (Step 10 grew the example model to the demo
// shape: keyed per-field todos, a collaborative note, a theme register, the
// counter escape hatch, and an app-only draft.)

open FSharp.Data.Adaptive
open Yjs

open Ylmish

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

open TodoCollaborative

let private user msg = Ylmish.Program.Message.User msg

let private syncBoth (a : Y.Doc) (b : Y.Doc) =
    Main.sync a b
    Main.sync b a

let private titles (m : TodoModel) =
    m.Todos |> HashMap.toList |> List.map (fun (_, t) -> t.Title) |> List.sort

let tests = testList "TodoCollaborative" [
    test "Program.withYlmish wired: a dispatched add reaches the model and the doc" {
        let doc = Y.Doc.Create ()
        use p = Elmish.Program.test (Main.makeProgram doc)
        p.Dispatch (user (AddTodo ("id-1", "Buy eggs", 1.0)))
        Expect.equal
            (p.Model.Todos |> HashMap.tryFind "id-1" |> Option.map (fun t -> t.Title))
            (Some "Buy eggs")
            "model updated"
        Expect.isTrue ((doc.getMap "todos" : Y.Map<obj>).has "id-1")
            "the todo landed in the doc under its app-minted key"
    }

    test "two programs converge after sync" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (Main.makeProgram d1)
        use p2 = Elmish.Program.test (Main.makeProgram d2)

        p1.Dispatch (user (AddTodo ("id-a", "Task A", 1.0)))
        syncBoth d1 d2
        Expect.equal (titles p2.Model) [ "Task A" ] "p2 received the item"

        p2.Dispatch (user (AddTodo ("id-b", "Task B", 2.0)))
        syncBoth d1 d2
        Expect.equal p1.Model.Todos p2.Model.Todos "converged"
        Expect.equal (titles p1.Model) [ "Task A"; "Task B" ] "both present"
    }

    test "concurrent adds from both peers both survive (issue #83's class, at the example level)" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (Main.makeProgram d1)
        use p2 = Elmish.Program.test (Main.makeProgram d2)

        // Offline: both peers add concurrently, under their own unique keys.
        p1.Dispatch (user (AddTodo ("id-1", "From peer 1", 1.0)))
        p2.Dispatch (user (AddTodo ("id-2", "From peer 2", 2.0)))
        syncBoth d1 d2

        Expect.equal p1.Model.Todos p2.Model.Todos "models converge"
        Expect.equal (titles p1.Model) [ "From peer 1"; "From peer 2" ]
            "NEITHER add was lost — the exact failure mode the materialize path had"
    }

    test "concurrent edits to different fields of the same todo both stick (demo act 4)" {
        // Regression for the Step 10 discovery: a one-field record edit
        // re-flushes the whole keyed item, and an unconditional flush restamps
        // the UNCHANGED fields too — entering LWW races the consumer never
        // intended (here: B's restamped done=false clobbered A's done=true).
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (Main.makeProgram d1)
        use p2 = Elmish.Program.test (Main.makeProgram d2)

        p1.Dispatch (user (AddTodo ("id-1", "buy milk", 1.0)))
        syncBoth d1 d2

        // Offline: A ticks it done while B renames it.
        p1.Dispatch (user (SetDone ("id-1", true)))
        p2.Dispatch (user (Rename ("id-1", "buy oat milk")))
        syncBoth d1 d2

        let todo = p1.Model.Todos |> HashMap.tryFind "id-1" |> Option.get
        Expect.equal todo.Title "buy oat milk" "B's rename stuck"
        Expect.isTrue todo.Done "and A's tick stuck too — per-field merge inside a keyed item"
        Expect.equal p1.Model.Todos p2.Model.Todos "converged"
    }
]
