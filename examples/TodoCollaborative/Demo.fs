module TodoCollaborative.Demo

// npm run demo — TodoCollaborative as a scripted two-peer narrative
// (plan 0002, Step 10).
//
// Two complete `withYlmish` programs run in this one process, each over its
// own Y.Doc. There is no server and no network: "offline" simply means sync
// has not been called yet, and every act prints the peers' ELMISH models —
// what a UI would render — never the docs directly.
//
// ClientIDs are pinned (A=1, B=2) so Yjs's concurrency tiebreaks always
// resolve the same way: the transcript is reproducible byte for byte, and it
// is embedded in the README as documentation.

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open TodoCollaborative

// ---------------------------------------------------------------------------
// A peer: a real Elmish program bound to its own doc.
// ---------------------------------------------------------------------------

type private Peer (name : string, clientId : float) =
    let doc = Y.Doc.Create ()
    do doc.clientID <- clientId
    let mutable model = TodoModel.init
    let mutable dispatch : Program.Message<TodoModel, Msg> -> unit = ignore
    do
        Main.makeProgram doc
        |> Elmish.Program.withSetState (fun m d ->
            model <- m
            dispatch <- d)
        |> Elmish.Program.runWith ()
    member _.Name = name
    member _.Doc = doc
    member _.Model = model
    member _.Do (msg : Msg) = dispatch (Program.Message.User msg)

// ---------------------------------------------------------------------------
// Narration helpers
// ---------------------------------------------------------------------------

let private show (p : Peer) =
    let m = p.Model
    printfn "  %s | theme %s | hits %d | draft \"%s\"" p.Name m.Theme m.Hits m.Draft
    m.Todos
    |> HashMap.toList
    |> List.sortBy (fun (_, t) -> t.Order)
    |> List.iter (fun (id, t) ->
        printfn "  %s |   %s %s — \"%s\"  (%s, order %g)"
            p.Name (if t.Done then "[x]" else "[ ]") t.Title (Text.toString t.Note) id t.Order)

let private act (n : int) (title : string) =
    printfn ""
    printfn "Act %d — %s" n title

let private say (line : string) = printfn "  %s" line

let private sync (a : Peer) (b : Peer) =
    Main.sync a.Doc b.Doc
    Main.sync b.Doc a.Doc
    printfn "  ~ sync ~"

// ---------------------------------------------------------------------------
// The script
// ---------------------------------------------------------------------------

let private run () =
    printfn "TodoCollaborative — two Elmish programs, one shared document, no server."

    let a = Peer ("A", 1.0)
    let b = Peer ("B", 2.0)

    act 1 "an empty doc decodes to your init state"
    say "Both peers start against empty docs. Nothing is written at startup:"
    say "init is what an empty doc decodes to, not something to persist."
    show a
    show b

    act 2 "concurrent edits to the same todo's note interleave"
    say "A creates the first todo, writes its note, and syncs."
    a.Do (AddTodo ("a-1", "buy milk", 1.0))
    a.Do (EditNote ("a-1", Text.edit "hello"))
    sync a b
    say "Offline, A appends to that note while B prepends to the same note."
    a.Do (EditNote ("a-1", Text.insert 5 " world"))
    b.Do (EditNote ("a-1", Text.insert 0 "oh, "))
    say "before the network heals:"
    show a
    show b
    sync a b
    say "after: both edits survive, interleaved — nobody's keystrokes lost."
    show a
    show b

    act 3 "offline creation is safe under app-minted keys"
    say "Still offline, each peer creates a todo. The ids are the app's own"
    say "(anything creatable offline needs a unique key — that's the rule)."
    a.Do (AddTodo ("a-2", "water plants", 3.0))
    a.Do (SetDraft "eggs too?")
    b.Do (AddTodo ("b-1", "walk dog", 2.0))
    sync a b
    say "after sync: BOTH creations survive (keyed element-wise merge)."
    show a
    show b

    act 4 "same todo, different fields: per-field merge"
    say "Concurrently, A ticks 'buy milk' done while B renames it."
    a.Do (SetDone ("a-1", true))
    b.Do (Rename ("a-1", "buy oat milk"))
    sync a b
    say "after sync: both stick — a todo is a record of independent registers."
    show a
    show b

    act 5 "same register, concurrent writes: an honest clobber"
    say "Both flip the theme at once. A register is last-writer-wins: one value"
    say "survives, deterministically (clientID tiebreak) — NOT 'whoever was later'."
    a.Do (SetTheme "dark")
    b.Do (SetTheme "sepia")
    sync a b
    show a
    show b

    act 6 "delete beats concurrent edits inside"
    say "A deletes 'walk dog' while B concurrently ticks it done."
    a.Do (RemoveTodo "b-1")
    b.Do (SetDone ("b-1", true))
    sync a b
    say "after sync: the todo is gone on both — ticking it could not resurrect it."
    show a
    show b

    act 7 "reordering is data, not structure"
    say "Concurrently: A moves 'water plants' to the top (order 0.5) while B"
    say "pushes 'buy oat milk' to the bottom (order 4). Order is a fractional"
    say "index: a reorder writes one number, so reorders cannot duplicate items."
    a.Do (Reorder ("a-2", 0.5))
    b.Do (Reorder ("a-1", 4.0))
    sync a b
    say "after sync: one converged order, every item exactly once."
    show a
    show b

    act 8 "the escape hatch: a merge no built-in provides"
    say "Hits is a consumer-authored counter over a raw Y.Array (see Counter.fs)."
    say "Offline, A bumps twice and B bumps once — optimistically:"
    a.Do Bump
    a.Do Bump
    b.Do Bump
    show a
    show b
    sync a b
    say "after sync: the counts SUM — concurrent increments are all kept."
    show a
    show b

    act 9 "app-only state never syncs"
    say "A's draft has said \"eggs too?\" since act 3 — B never saw it, because"
    say "the codec never mentions Draft. It is not in the doc either:"
    show a
    show b
    let root : Y.Map<obj> = a.Doc.getMap ()
    say (sprintf "A's doc, top-level register keys: %A" (root.keys () |> List.ofSeq))
    say (sprintf "A's doc has a 'draft' key: %b" (root.has "draft"))

    printfn ""
    printfn "The models above are what each peer's UI renders — no peer ever read"
    printfn "another's memory, only Yjs updates travelled."

run ()
