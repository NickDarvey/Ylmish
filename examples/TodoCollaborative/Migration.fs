module TodoCollaborative.Migration

// A small, in-process vignette: the codec is the schema-migration boundary. A
// peer authored a todo under schema **v1** (the completion flag was named `done`);
// the current app is **v2** (renamed to `completed`). Because a collaborative CRDT
// document has no single migration moment, v2's codec reads both shapes — so the
// v1-authored todo loads correctly and the two schema versions coexist.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Adaptive.Codec

let run () =
    // A v1 peer's document: a completed todo stored under the OLD `done` key.
    let v1Item : CollectionItem =
        { Id = "1"; Fields = [ "done", "true"; "order", "a0" ]; Texts = [ "text", "ship v2" ] }
    let cl = clist [ v1Item ]
    let merged = cval ([] : CollectionItem list)
    let enc =
        Encode.object [ "todos", Encode.collection [ "text" ] (fun it -> AVal.constant it) merged (cl :> alist<_>) ]
    let v1 = Y.Doc.Create ()
    // Keep the connection for the lifetime of this short vignette (no dispose).
    let _ = Y.Doc.connect v1 enc

    // The v2 app loads it. The codec reads the legacy `done` via its fallback.
    let d2 = Y.Doc.Create ()
    let mutable model = TodoModel.init
    Main.makeProgram d2
    |> Elmish.Program.withSetState (fun m _ -> model <- m)
    |> Elmish.Program.runWith ()
    Y.applyUpdate (d2, Y.encodeStateAsUpdate v1)

    printfn "[migration] a v1-authored todo (old 'done' schema) loaded in v2:"
    for t in TodoModel.ordered model do
        printfn "[migration]   %s  done=%b  text=%A" t.Id t.Done t.Text
    printfn "[migration] -> the codec read the legacy key; v1 and v2 coexist"
