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

let tests = testList "TodoCollaborative" [
    test "two-doc sync: docs converge after sync" {
        // Create two independent Y.Doc instances (simulating two peers)
        let doc1 = Y.Doc.Create ()
        let doc2 = Y.Doc.Create ()

        // Peer 1: materialize a todo model with one item
        let model1 = { TodoModel.init with Items = IndexList.ofList [ "Buy milk" ] }
        let amodel1 = AdaptiveTodoModel.Create model1
        let encoded1 = Codec.encode amodel1
        Y.Doc.materialize doc1 encoded1

        // Sync doc1 → doc2
        Main.sync doc1 doc2

        // Peer 2 should now have the same state
        let element2 = Y.Doc.dematerialize doc2
        let decoded2 = Codec.decode model1 ([], element2) |> AVal.force
        match decoded2 with
        | Ok result ->
            Expect.equal result.Items (IndexList.ofList [ "Buy milk" ]) "Items should match after sync"
            Expect.equal result.NewItem "" "NewItem should match after sync"
        | Error errors ->
            failwithf "Failed to decode doc2: %s" (Error.printAll errors)
    }

    test "two-doc sync: bidirectional sync converges" {
        let doc1 = Y.Doc.Create ()
        let doc2 = Y.Doc.Create ()

        // Peer 1: materialize initial model
        let model1 = { TodoModel.init with Items = IndexList.ofList [ "Task A" ]; NewItem = "draft" }
        let amodel1 = AdaptiveTodoModel.Create model1
        let encoded1 = Codec.encode amodel1
        Y.Doc.materialize doc1 encoded1

        // Sync doc1 → doc2
        Main.sync doc1 doc2

        // Peer 2: update the model on doc2
        let model2 = { model1 with Items = IndexList.ofList [ "Task A"; "Task B" ]; NewItem = "" }
        let amodel2 = AdaptiveTodoModel.Create model2
        let encoded2 = Codec.encode amodel2
        Y.Doc.materialize doc2 encoded2

        // Sync doc2 → doc1
        Main.sync doc2 doc1

        // Both docs should now have converged to the same state
        let element1 = Y.Doc.dematerialize doc1
        let decoded1 = Codec.decode model2 ([], element1) |> AVal.force
        match decoded1 with
        | Ok result ->
            Expect.equal result.NewItem "" "NewItem should be empty after sync"
        | Error errors ->
            failwithf "Failed to decode doc1 after bidirectional sync: %s" (Error.printAll errors)

        let element2 = Y.Doc.dematerialize doc2
        let decoded2 = Codec.decode model2 ([], element2) |> AVal.force
        match decoded2 with
        | Ok result ->
            Expect.equal result.NewItem "" "NewItem should be empty on doc2"
        | Error errors ->
            failwithf "Failed to decode doc2 after bidirectional sync: %s" (Error.printAll errors)
    }

    test "Program.withYlmish wired with TodoCollaborative model" {
        let doc = Y.Doc.Create ()
        use dispatcher =
            Main.makeProgram doc
            |> Elmish.Program.test

        // Dispatch AddItem via the User message wrapper
        dispatcher.Dispatch (Ylmish.Program.Message.User (AddItem "Buy eggs"))

        Expect.equal (dispatcher.Model.Items |> IndexList.toList) [ "Buy eggs" ] "Items should contain the added item"

        // Verify the Y.Doc has the data by dematerializing and decoding
        let element = Y.Doc.dematerialize doc
        let decoded = Codec.decode dispatcher.Model ([], element) |> AVal.force
        match decoded with
        | Ok result ->
            Expect.equal (result.Items |> IndexList.toList) [ "Buy eggs" ] "Y.Doc should contain the item after decode"
        | Error errors ->
            failwithf "Failed to decode Y.Doc: %s" (Error.printAll errors)
    }
]
