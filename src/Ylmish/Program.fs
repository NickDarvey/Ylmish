[<RequireQualifiedAccess>]
module Ylmish.Program

// Plan 0002, Step 7 — withYlmish on the binding runtime. The v1
// materialize-per-update implementation is gone: local changes flow to the
// doc as origin-tagged deltas via Ylmish.Internal.Binding (Step 5), and
// remote transactions surface as exactly one `Set` each via the decode
// direction (Step 6).

open System

open Elmish
open FSharp.Data.Adaptive
open Yjs

open Ylmish.Codec
open Ylmish.Internal

type Message<'model, 'msg> =
    | Set of 'model
    | User of 'msg

/// Decode-failure policy: a malformed or newer-versioned doc must never crash
/// the loop — the current model stays in place and the errors go here
/// (plan 0002, open question 1). Schema drift on the write path (the doc holds
/// a different kind than the schema expects) is routed here too.
type OnError = { Handle : Error list -> unit }

[<RequireQualifiedAccess>]
module OnError =
    /// Log and keep the current model — the default policy.
    let log : OnError =
        { Handle = fun errors -> eprintfn "withYlmish: keeping the current model. %A" errors }

type Options<'model, 'amodel> = {
    Doc : Y.Doc
    /// Adaptify-generated — the one place Adaptive remains on the surface.
    Create : 'model -> 'amodel
    Update : 'amodel -> 'model -> unit
    Encode : 'amodel -> Encoded
    Decode : Decoder<'model, 'model>
    OnError : OnError
}

/// Bind an Elmish program to a Y.Doc through the v2 codec.
///
/// - **Init** decodes whatever is already in the doc through your decoder
///   (with your init model in `Decode.ask`'s environment) — an empty doc
///   decodes to your init state through the same path: decode-empty = init.
///   Nothing is written at startup.
/// - **Local updates** flow to the doc as one origin-tagged Y transaction per
///   Elmish update (however many fields changed), as deltas: text as splices,
///   keyed items per key, lists as index operations, registers as LWW sets.
/// - **Remote transactions** each produce exactly one `Set` carrying the
///   re-decoded model; your own writes never echo back.
let withYlmish
    (options : Options<'model, 'amodel>)
    (program : Program<'arg, 'model, 'msg, 'view>)
    : Program<'arg, 'model, Message<'model, 'msg>, 'view> =

    let mutable attached : ('amodel * Encoded * Binding.Attachment) option = None
    let mutable currentModel : 'model option = None

    let decodeCurrent (encoded : Encoded) (current : 'model) : Result<'model, Error list> =
        Decode.runElement current options.Decode (Binding.read options.Doc encoded)

    let init userInit arg =
        let m, c = userInit arg
        let am = options.Create m
        let encoded = options.Encode am
        // Restore existing doc state through the consumer's decoder; an empty
        // doc restores to `m` itself (decode-empty = init).
        let restored =
            match decodeCurrent encoded m with
            | Ok r -> r
            | Error errors ->
                options.OnError.Handle errors
                m
        // L4: adaptive mutation demands a transaction.
        transact (fun () -> options.Update am restored)
        // Attach AFTER aligning the adaptive model: attach writes nothing, and
        // its subscriptions then see only genuine post-init changes.
        let att = Binding.attach options.Doc encoded
        attached <- Some (am, encoded, att)
        currentModel <- Some restored
        restored, Cmd.map User c

    let update userUpdate msg model =
        match attached with
        | None -> invalidOp "withYlmish: update ran before init."
        | Some (am, _, att) ->
            match msg with
            | Set m ->
                // A decoded remote model: fold into the adaptive model with
                // the encode direction suppressed — the doc already holds
                // exactly this content, so writing it back would duplicate it
                // (the L4 read-back hazard).
                currentModel <- Some m
                att.RunSuppressed (fun () -> transact (fun () -> options.Update am m))
                m, Cmd.none
            | User userMsg ->
                let m, c = userUpdate userMsg model
                currentModel <- Some m
                try
                    // One Y transaction per Elmish update: the binding's own
                    // transacts nest inside and share this origin.
                    Y.transact (options.Doc, (fun _ -> transact (fun () -> options.Update am m)), att.Origin)
                with Binding.SchemaDrift error ->
                    options.OnError.Handle [ error ]
                m, Cmd.map User c

    let subs userSubscribe model =
        Sub.batch [
            userSubscribe model |> Sub.map "ylmish" User
            [ [ "ylmish-doc" ], fun dispatch ->
                match attached with
                | Some (_, encoded, att) ->
                    Binding.subscribe options.Doc att.Origin (fun () ->
                        match currentModel with
                        | Some current ->
                            match decodeCurrent encoded current with
                            | Ok m -> dispatch (Set m)
                            | Error errors -> options.OnError.Handle errors
                        | None -> ())
                | None ->
                    { new IDisposable with member _.Dispose () = () } ]
        ]

    let setState userSetState model dispatch =
        userSetState model (User >> dispatch)

    let view userView model dispatch =
        userView model (User >> dispatch)

    let termination (userPredicate, userTerminate) =
        (fun msg ->
            match msg with
            | Set _ -> false
            | User userMsg -> userPredicate userMsg),
        (fun model ->
            match attached with
            | Some (_, _, att) -> (att :> IDisposable).Dispose ()
            | None -> ()
            userTerminate model)

    program
    |> Program.map init update view setState subs termination
