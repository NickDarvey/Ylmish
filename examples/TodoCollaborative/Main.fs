module TodoCollaborative.Main

open Elmish
open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

/// Create a Program.withYlmish-wired Elmish program for a given Y.Doc. The shared
/// `merged` cell carries the converged collection between encoder and decoder.
let makeProgram (doc : Y.Doc) =
    let merged = cval ([] : CollectionItem list)
    Program.mkSimple (fun () -> TodoModel.init) TodoModel.update TodoModel.view
    |> Program.withYlmish {
        Create = AdaptiveTodoModel.Create
        Update = fun a b -> a.Update b
        Encode = Codec.encode merged
        Decode = Codec.decode merged
        Doc = doc
    }

/// Sync updates from one Y.Doc to another (simulating a network round-trip).
let sync (src : Y.Doc) (dst : Y.Doc) =
    let update = Y.encodeStateAsUpdate src
    Y.applyUpdate (dst, update)
