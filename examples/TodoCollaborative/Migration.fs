module TodoCollaborative.Migration

// A small, in-process vignette: the codec is the schema-migration boundary. A peer
// authored a todo under schema **v1** (the completion flag was named `done`); the
// current app is **v2** (renamed to `completed`). Because a collaborative CRDT
// document has no single migration moment, v2's codec reads both shapes — so the
// v1-authored todo loads correctly and the two schema versions coexist.

open Yjs

let run () =
    // A v1 peer's document, written directly in the codec's key scheme with only
    // the OLD `done` key (no `completed`).
    let v1 = Y.Doc.Create ()
    let m : Y.Map<obj> = v1.getMap "todos"
    m.set ("@1", box "1") |> ignore
    m.set ("1/v/done", box "true") |> ignore
    m.set ("1/v/order", box "a0") |> ignore
    m.set ("1/t/text", box "") |> ignore
    (v1.getText "todos/1/text").insert (0, "ship v2")

    // The v2 app loads it. The codec reads the legacy `done` via its fallback.
    let d2 = Y.Doc.Create ()
    let mutable model = TodoModel.init
    Main.makeProgram d2
    |> Elmish.Program.withSetState (fun mo _ -> model <- mo)
    |> Elmish.Program.runWith ()
    Y.applyUpdate (d2, Y.encodeStateAsUpdate v1)

    printfn "[migration] a v1-authored todo (old 'done' schema) loaded in v2:"
    for (id, t) in TodoModel.ordered model do
        printfn "[migration]   %s  done=%b  text=%A" id t.Done t.Text
    printfn "[migration] -> the codec read the legacy key; v1 and v2 coexist"
