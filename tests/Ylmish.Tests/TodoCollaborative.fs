module Ylmish.TodoCollaborative

// Plan 0002, Step 7 — the example app over withYlmish v2, including issue
// #83's acceptance at the example level: concurrent adds from two peers both
// survive in both Elmish models.

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

let tests = testList "TodoCollaborative" [
    test "Program.withYlmish wired: a dispatched add reaches the model and the doc" {
        let doc = Y.Doc.Create ()
        use p = Elmish.Program.test (Main.makeProgram doc)
        p.Dispatch (user (AddItem "Buy eggs"))
        Expect.equal (p.Model.Items |> IndexList.toList) [ "Buy eggs" ] "model updated"
        Expect.equal
            ((doc.getArray "items" : Y.Array<obj>).toArray () |> Seq.map string |> Seq.toList)
            [ "Buy eggs" ]
            "the item landed in the doc as a list element"
    }

    test "two programs converge after sync" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (Main.makeProgram d1)
        use p2 = Elmish.Program.test (Main.makeProgram d2)

        p1.Dispatch (user (AddItem "Task A"))
        syncBoth d1 d2
        Expect.equal (p2.Model.Items |> IndexList.toList) [ "Task A" ] "p2 received the item"

        p2.Dispatch (user (AddItem "Task B"))
        syncBoth d1 d2
        Expect.equal (p1.Model.Items |> IndexList.toList) (p2.Model.Items |> IndexList.toList) "converged"
        Expect.equal (p1.Model.Items |> IndexList.toList |> List.sort) [ "Task A"; "Task B" ] "both present"
    }

    test "concurrent adds from both peers both survive (issue #83's class, at the example level)" {
        let d1 = Y.Doc.Create ()
        let d2 = Y.Doc.Create ()
        use p1 = Elmish.Program.test (Main.makeProgram d1)
        use p2 = Elmish.Program.test (Main.makeProgram d2)

        // Offline: both peers add concurrently.
        p1.Dispatch (user (AddItem "From peer 1"))
        p2.Dispatch (user (AddItem "From peer 2"))
        syncBoth d1 d2

        let items1 = p1.Model.Items |> IndexList.toList
        let items2 = p2.Model.Items |> IndexList.toList
        Expect.equal items1 items2 "models converge"
        Expect.equal (List.sort items1) [ "From peer 1"; "From peer 2" ]
            "NEITHER add was lost — the exact failure mode the materialize path had"
    }
]
