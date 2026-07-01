module TodoCollaborative.Main

open Elmish
open Yjs

open Ylmish

/// Create a Program.withYlmish-wired Elmish program for a given Y.Doc.
let makeProgram (doc : Y.Doc) =
    Program.mkSimple (fun () -> TodoModel.init) TodoModel.update TodoModel.view
    |> Program.withYlmish {
        Create = AdaptiveTodoModel.Create
        Update = fun a b -> a.Update b
        Encode = Codec.encode
        Decode = Codec.decode
        Doc = doc
    }

/// Sync updates from one Y.Doc to another (simulating a network round-trip).
let sync (src : Y.Doc) (dst : Y.Doc) =
    let update = Y.encodeStateAsUpdate src
    Y.applyUpdate (dst, update)
