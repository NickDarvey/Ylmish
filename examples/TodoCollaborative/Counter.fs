namespace TodoCollaborative

// A consumer-authored binding through Ylmish's escape hatch. Note the opens:
// Yjs and Ylmish.Codec only — writing a custom merge strategy needs neither
// Ylmish's internals nor FSharp.Data.Adaptive.

open System

open Yjs
open Ylmish.Codec

// sample:begin counter
/// A grow-only counter over a Y.Array of ticks. Concurrent increments from
/// different peers BOTH survive (array inserts merge), so the merged value is
/// their SUM — a merge no built-in encoding provides.
type GrowOnlyCounter () =
    let mutable ticks : Y.Array<obj> option = None
    let mutable origin : obj = null

    /// Push one tick. Call from a Cmd effect after an optimistic increment;
    /// the authoritative count comes back through Decode.custom. The write is
    /// tagged with the attachment origin so it never echoes back as a remote.
    member _.Bump () =
        match ticks with
        | Some arr ->
            match arr.doc with
            | Some doc -> Y.transact (doc, (fun _ -> arr.push [| box 1 |]), origin)
            | None -> ()
        | None -> ()

    interface CustomElement with
        member _.Connect ctx =
            ticks <- Some (ctx.GetArray ())
            origin <- ctx.Origin
            { new IDisposable with member _.Dispose () = () }
        member _.Value =
            match ticks with
            | Some arr -> box ((arr.toArray ()).Count)
            | None -> box 0
// sample:end counter
