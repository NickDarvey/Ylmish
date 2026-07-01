module TodoCollaborative.Demo

// A runnable, two-process demonstration of TodoCollaborative.
//
//   node Demo.js                 -> launcher: forks two peers and relays
//                                   Yjs updates between them over IPC.
//   node Demo.js --peer <name>   -> peer: runs the Ylmish-wired Elmish
//                                   program over its own Y.Doc.
//
// The launcher acts as a tiny message hub: every local Yjs update a peer
// produces is forwarded to the other peer, who applies it to its own Y.Doc.
// Ylmish then decodes the change back into that peer's Elmish model.

open Fable.Core
open Fable.Core.JsInterop
open FSharp.Data.Adaptive
open Elmish
open Yjs

open Ylmish
open TodoCollaborative

// ---------------------------------------------------------------------------
// Minimal Node.js interop (kept local so the example needs no extra packages)
// ---------------------------------------------------------------------------

module Node =
    [<Emit("process.argv")>]
    let argv : string[] = jsNative

    [<Emit("typeof process.send === 'function'")>]
    let isChild : bool = jsNative

    [<Emit("process.send($0)")>]
    let send (_msg : obj) : unit = jsNative

    [<Emit("process.on('message', $0)")>]
    let onMessage (_handler : obj -> unit) : unit = jsNative

    [<Import("fork", "child_process")>]
    let private forkRaw (_modulePath : string) (_args : string[]) : obj = jsNative

    /// Fork a child process running the given module with args.
    let fork (modulePath : string) (args : string[]) : obj = forkRaw modulePath args

    [<Emit("$0.send($1)")>]
    let childSend (_child : obj) (_msg : obj) : unit = jsNative

    [<Emit("$0.on($1, $2)")>]
    let childOn (_child : obj) (_event : string) (_handler : obj -> unit) : unit = jsNative

    [<Emit("$0.kill()")>]
    let childKill (_child : obj) : unit = jsNative

    [<Emit("setTimeout($1, $0)")>]
    let setTimeout (_ms : int) (_f : unit -> unit) : unit = jsNative

    [<Emit("process.exit($0)")>]
    let exit (_code : int) : unit = jsNative

// Yjs interop the bindings don't expose conveniently.
[<Emit("$0.on('update', $1)")>]
let private onDocUpdate (_doc : Y.Doc) (_handler : obj -> obj -> unit) : unit = jsNative

[<Emit("$0 === 'remote'")>]
let private isRemoteOrigin (_origin : obj) : bool = jsNative

// Yjs updates are Uint8Array; convert to/from plain number arrays so the
// default JSON-based IPC serialization round-trips them safely.
[<Emit("Array.from($0)")>]
let private bytesToNums (_bytes : obj) : int[] = jsNative

[<Emit("Uint8Array.from($0)")>]
let private numsToBytes (_nums : int[]) : JS.Uint8Array = jsNative

// ---------------------------------------------------------------------------
// Peer process
// ---------------------------------------------------------------------------

let runPeer (name : string) =
    let doc = Y.Doc.Create ()

    // Forward our own (local) Yjs updates to the launcher. Updates we apply
    // from a remote peer carry the "remote" origin and must not be re-sent.
    onDocUpdate doc (fun update origin ->
        if not (isRemoteOrigin origin) then
            Node.send {| kind = "update"; from = name; bytes = bytesToNums update |})

    // Apply inbound updates / scripted operations from the launcher.
    let mutable dispatch : Program.Message<TodoModel, Msg> -> unit = ignore
    Node.onMessage (fun msg ->
        match msg?kind : string with
        | "update" ->
            Y.applyUpdate (doc, numsToBytes (msg?bytes), "remote")
        | "op-add" ->
            // Type into the new-item box, then add it with the given id.
            dispatch (Program.Message.User (SetNewItem (msg?text)))
            dispatch (Program.Message.User (Add (msg?id)))
        | "op-edit" ->
            dispatch (Program.Message.User (Edit (msg?id, msg?text)))
        | "op-toggle" ->
            dispatch (Program.Message.User (Toggle (msg?id)))
        | "op-move" ->
            let opt (s : string) = if System.String.IsNullOrEmpty s then None else Some s
            dispatch (Program.Message.User (Move (msg?id, opt (msg?prev), opt (msg?next))))
        | _ -> ())

    // Run the Ylmish-wired program, capturing dispatch and logging each model.
    let setState (model : TodoModel) (d : Program.Message<TodoModel, Msg> -> unit) =
        dispatch <- d
        let render (_, t : Todo) = sprintf "%s%s" (if t.Done then "[x] " else "[ ] ") t.Text
        printfn "[peer %s] %A" name (TodoModel.visible model |> List.map render)

    Main.makeProgram doc
    |> Program.withSetState setState
    |> Program.runWith ()

    // Tell the launcher we're up and our dispatch loop is live.
    Node.send {| kind = "ready"; from = name |}

// ---------------------------------------------------------------------------
// Launcher process
// ---------------------------------------------------------------------------

let runLauncher () =
    let selfPath = Node.argv.[1]

    // First, three self-contained vignettes:
    //  - a custom-element grow-only counter whose concurrent increments SUM,
    //  - a reorderable collaborative list whose text survives reorder via id naming,
    //  - a schema-migration demo: a v1-authored todo loads in the v2 codec.
    Counter.run ()
    ReorderableList.run ()
    Migration.run ()

    printfn "[launcher] forking two peers (A and B)…"

    let peerA = Node.fork selfPath [| "--peer"; "A" |]
    let peerB = Node.fork selfPath [| "--peer"; "B" |]

    let mutable readyCount = 0

    // Relay updates from one peer to the other, track readiness, kick off the
    // scripted scenario once both peers are live.
    let route (other : obj) (msg : obj) =
        match msg?kind : string with
        | "update" -> Node.childSend other msg
        | "ready" ->
            readyCount <- readyCount + 1
            if readyCount = 2 then
                printfn "[launcher] both peers ready — running scenario"

                // A and B add a todo each, CONCURRENTLY. The element-wise collection
                // merges both — neither add is lost (the old whole-list LWW dropped one).
                Node.setTimeout 250 (fun () ->
                    printfn "[launcher] -> A & B: concurrent adds (both should survive)"
                    Node.childSend peerA {| kind = "op-add"; id = "1"; text = "milk" |}
                    Node.childSend peerB {| kind = "op-add"; id = "2"; text = "walk the dog" |})

                // Both edit todo 1's text CONCURRENTLY; the per-item CRDT text merges.
                Node.setTimeout 900 (fun () ->
                    printfn "[launcher] -> A & B: concurrent edits to todo 1's text (should merge)"
                    Node.childSend peerA {| kind = "op-edit"; id = "1"; text = "buy milk" |}
                    Node.childSend peerB {| kind = "op-edit"; id = "1"; text = "milk (2%)" |})

                // Toggle different todos; both completions stick.
                Node.setTimeout 1300 (fun () ->
                    printfn "[launcher] -> A: complete todo 1;  B: complete todo 2"
                    Node.childSend peerA {| kind = "op-toggle"; id = "1" |}
                    Node.childSend peerB {| kind = "op-toggle"; id = "2" |})

                // Reorder: A moves todo 2 to the front (a single fractional-key write).
                Node.setTimeout 1700 (fun () ->
                    printfn "[launcher] -> A: prioritise todo 2 (move it to the front)"
                    Node.childSend peerA {| kind = "op-move"; id = "2"; prev = ""; next = "1" |})

                // Wrap up.
                Node.setTimeout 2400 (fun () ->
                    printfn "[launcher] done — shutting peers down"
                    Node.childKill peerA
                    Node.childKill peerB
                    Node.exit 0)
        | _ -> ()

    Node.childOn peerA "message" (route peerB)
    Node.childOn peerB "message" (route peerA)

// ---------------------------------------------------------------------------
// Entry point: dispatch on argv
// ---------------------------------------------------------------------------

let main () =
    let args = Node.argv
    let peerIndex = args |> Array.tryFindIndex (fun a -> a = "--peer")
    match peerIndex with
    | Some i when i + 1 < args.Length -> runPeer args.[i + 1]
    | _ -> runLauncher ()

main ()
