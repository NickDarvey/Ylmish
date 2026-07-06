module TodoCollaborative.Main

open Elmish
open Yjs

open Ylmish

/// Create a Program.withYlmish-wired Elmish program for a given Y.Doc. Each
/// peer owns one counter binding — created here so update's Bump effect and
/// the codec share the instance.
let makeProgram (doc : Y.Doc) =
    let counter = GrowOnlyCounter ()
    Program.mkProgram (fun () -> TodoModel.init, Cmd.none) (TodoModel.update counter) TodoModel.view
    |> Program.withYlmish {
        Doc = doc
        Create = AdaptiveTodoModel.Create
        Update = fun a b -> a.Update b
        Encode = Codec.encode counter
        Decode = Codec.decode
        OnError = Program.OnError.log
    }

/// Sync updates from one Y.Doc to another (simulating a network round-trip).
let sync (src : Y.Doc) (dst : Y.Doc) =
    let update = Y.encodeStateAsUpdate src
    Y.applyUpdate (dst, update)
