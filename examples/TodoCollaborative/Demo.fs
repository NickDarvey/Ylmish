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
            dispatch (Program.Message.User (AddItem (msg?text)))
        | "op-note" ->
            dispatch (Program.Message.User (SetNote (msg?text)))
        | _ -> ())

    // Run the Ylmish-wired program, capturing dispatch and logging each model.
    let setState (model : TodoModel) (d : Program.Message<TodoModel, Msg> -> unit) =
        dispatch <- d
        printfn "[peer %s] items=%A note=%A"
            name (model.Items |> IndexList.toList) model.Note

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

                // Peer A adds an item; it should appear on B.
                Node.setTimeout 250 (fun () ->
                    printfn "[launcher] -> A: add \"Buy milk\""
                    Node.childSend peerA {| kind = "op-add"; text = "Buy milk" |})

                // Peer B adds an item; it should appear on A.
                Node.setTimeout 750 (fun () ->
                    printfn "[launcher] -> B: add \"Walk the dog\""
                    Node.childSend peerB {| kind = "op-add"; text = "Walk the dog" |})

                // Both peers edit the collaborative note CONCURRENTLY. With
                // last-writer-wins one would clobber the other; Encode.text makes
                // the edits CRDT-merge, so both fragments survive on both peers.
                Node.setTimeout 1100 (fun () ->
                    printfn "[launcher] -> A & B: concurrent note edits (should merge)"
                    Node.childSend peerA {| kind = "op-note"; text = "A-says-hi " |}
                    Node.childSend peerB {| kind = "op-note"; text = "B-says-yo " |})

                // Wrap up.
                Node.setTimeout 2000 (fun () ->
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
