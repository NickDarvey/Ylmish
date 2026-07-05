module internal Ylmish.Internal.Binding

// Plan 0002, Step 5 — the binding layer, encode direction. Internal: walks an
// Encoded tree once and establishes live, delta-level bindings from the
// adaptive views to Y types. Built ALONGSIDE the v1 materialize path, which
// Step 7 deletes. See doc/plans/0002-ylmish-redesign.md ("The runtime").
//
// The behavioural contract (each rule is pinned by tests):
//   - No eager writes: attaching writes NOTHING; state flows only when a local
//     edit changes it (decode-empty = init made real). The exceptions are the
//     two semantic "creations": a keyed-map item ADD and an option None→Some,
//     which flush the new subtree's current state into fresh containers.
//   - Bind, never replace (U5/U11): containers are adopted if present, created
//     if absent, and never re-created. A kind mismatch with existing doc state
//     raises a structured SchemaDrift carrying a path-tracked Codec.Error (L5).
//   - Leave what you don't own (U15): keys the encoder doesn't mention are
//     never touched or deleted.
//   - Every write happens inside Y.transact tagged with this attachment's
//     Origin token (U6/L4) — Step 6 filters these out of the decode direction,
//     and Y.UndoManager can scope to them later.
//   - O(delta): text flows as splices (drained intents), lists as index
//     deltas, maps as per-key ops, registers skip no-op re-stamps.

open System

open FSharp.Data.Adaptive
open Yjs

open Ylmish
open Ylmish.Codec

// -----------------------------------------------------------------------------
// Structured schema-drift failure (L5): the doc already holds a different kind
// at this path than the encoding expects. Step 7 routes this to OnError.
// -----------------------------------------------------------------------------

exception SchemaDrift of Error

// -----------------------------------------------------------------------------
// Runtime type tests on Y values (mirrors Y.fs's approach).
// -----------------------------------------------------------------------------

#if FABLE_COMPILER
[<Fable.Core.Import("Text", "yjs")>]
let private jsYText : obj = obj ()

[<Fable.Core.Import("Array", "yjs")>]
let private jsYArray : obj = obj ()

[<Fable.Core.Import("Map", "yjs")>]
let private jsYMap : obj = obj ()

[<Fable.Core.Emit("$0 instanceof $1")>]
let private jsInstanceOf (_x : obj) (_ctor : obj) : bool = false

let private isYText (v : obj) = jsInstanceOf v jsYText
let private isYMap (v : obj) = jsInstanceOf v jsYMap
let private isYArray (v : obj) = jsInstanceOf v jsYArray

let private plainObject (fields : (string * obj) list) : obj =
    Fable.Core.JsInterop.createObj fields

let private plainArray (items : obj list) : obj =
    box (List.toArray items)
#else
let private isYText (v : obj) = v.GetType().Name.StartsWith "YText"
let private isYMap (v : obj) = v.GetType().Name.StartsWith "YMap"
let private isYArray (v : obj) = v.GetType().Name.StartsWith "YArray"

let private plainObject (fields : (string * obj) list) : obj =
    box (dict fields)

let private plainArray (items : obj list) : obj =
    box (List.toArray items)
#endif

let private primToObj (p : Primitive) : obj =
    match p with
    | PString s -> box s
    | PNumber n -> box n
    | PBool b -> box b

// -----------------------------------------------------------------------------
// Container slots: get-or-create-or-adopt, kind-checked, never replaced.
// -----------------------------------------------------------------------------

/// Lazily materializes the Y.Map an object/keyed-map node is backed by.
type private EnsureMap = unit -> Y.Map<obj>

let private drift path expectedKind (found : obj) =
    let foundKind =
        if isYText found then "a Y.Text"
        elif isYArray found then "a Y.Array"
        elif isYMap found then "a Y.Map"
        else "a plain value"
    raise (SchemaDrift (UnexpectedKind (path, sprintf "the doc holds %s where the schema expects %s — schema drift" foundKind expectedKind)))

let private ensureChildMap (parent : EnsureMap) (key : string) (path : Path) : EnsureMap =
    fun () ->
        let p = parent ()
        match p.get key with
        | Some v when isYMap v -> unbox<Y.Map<obj>> v
        | Some v -> drift path "a map" v
        | None ->
            let m : Y.Map<obj> = Y.Map.Create ()
            p.set (key, box m) |> ignore
            m

let private ensureText (parent : EnsureMap) (key : string) (path : Path) : unit -> Y.Text * bool =
    fun () ->
        let p = parent ()
        match p.get key with
        | Some v when isYText v -> unbox<Y.Text> v, false
        | Some v -> drift path "a text" v
        | None ->
            let t = Y.Text.Create ()
            p.set (key, box t) |> ignore
            t, true

let private ensureArray (parent : EnsureMap) (key : string) (path : Path) : unit -> Y.Array<obj> =
    fun () ->
        let p = parent ()
        match p.get key with
        | Some v when isYArray v -> unbox<Y.Array<obj>> v
        | Some v -> drift path "a list" v
        | None ->
            let a : Y.Array<obj> = Y.Array.Create ()
            p.set (key, box a) |> ignore
            a

// -----------------------------------------------------------------------------
// Text: apply drained intents as precise splices.
// -----------------------------------------------------------------------------

/// New intents in `next` relative to `prev`: when `next` derives from `prev` by
/// edits, its pending list shares `prev`'s as a structural tail (immutable
/// list sharing), so the difference is exactly the new splices. When it does
/// not derive (wholesale replacement, a decoded value), fall back to a single
/// affix-diff splice between the contents.
let private newSplices (prev : Text) (next : Text) : Splice list =
    let prevPending = Text.pending prev
    let nextPending = Text.pending next
    let prevLen = List.length prevPending
    let nextLen = List.length nextPending
    let derived =
        nextLen >= prevLen
        && List.skip (nextLen - prevLen) nextPending = prevPending
    if derived then
        nextPending |> List.take (nextLen - prevLen)
    else
        Text.pending (Text.edit (Text.toString next) (Text.ofString (Text.toString prev)))

let private applySplicesToYText (ytext : Y.Text) (splices : Splice list) =
    for s in splices do
        if s.Removed > 0 then ytext.delete (s.At, s.Removed)
        if s.Inserted <> "" then ytext.insert (s.At, s.Inserted)

// -----------------------------------------------------------------------------
// Atomic: any change in the subtree re-stamps it wholesale as plain JSON data.
// -----------------------------------------------------------------------------

let rec private plainOfElement (el : Element) : obj =
    match el with
    | ElValue p -> primToObj p
    | ElText s -> box s
    | ElObject fields | ElMap fields ->
        fields |> HashMap.toList |> List.map (fun (k, v) -> k, plainOfElement v) |> plainObject
    | ElList ps -> ps |> List.map primToObj |> plainArray
    | ElAtomic inner -> plainOfElement inner
    | ElCustom _ ->
        raise (SchemaDrift (UnexpectedKind ([], "a custom element cannot live inside Encode.atomic — atomic subtrees are pure data")))

/// An aval that changes whenever anything in the encoded subtree changes.
/// Children are hoisted so the dependency graph is built once.
let rec private subtreeVersion (e : Encoded) : aval<int> =
    let deps : IAdaptiveValue list =
        let rec collect (e : Encoded) : IAdaptiveValue list =
            match e with
            | EncValue a -> [ a :> IAdaptiveValue ]
            | EncText a -> [ a :> IAdaptiveValue ]
            | EncObject props -> props |> List.collect (snd >> collect)
            | EncMap m -> [ AMap.toAVal m :> IAdaptiveValue ]
            | EncList l -> [ AList.toAVal l :> IAdaptiveValue ]
            | EncOption (isSome, inner) -> (isSome :> IAdaptiveValue) :: collect inner
            | EncAtomic inner -> collect inner
            | EncCustom _ -> []
        collect e
    let mutable version = 0
    AVal.custom (fun token ->
        for d in deps do d.GetValueUntyped token |> ignore
        version <- version + 1
        version)

// -----------------------------------------------------------------------------
// The attachment
// -----------------------------------------------------------------------------

type Attachment (origin : obj, disposables : ResizeArray<IDisposable>) =
    member _.Origin = origin
    member _.Add (d : IDisposable) = disposables.Add d
    interface IDisposable with
        member _.Dispose () =
            for d in disposables do d.Dispose ()
            disposables.Clear ()

/// Write the CURRENT state of an encoding into the doc — used only for the two
/// semantic creations (keyed-map item add, option None→Some), where the whole
/// new subtree must appear at once. Adopts existing containers; never replaces.
let rec private flush (doc : Y.Doc) (origin : obj) (parent : EnsureMap) (key : string) (path : Path) (e : Encoded) : unit =
    match e with
    | EncValue a -> (parent ()).set (key, primToObj (AVal.force a)) |> ignore
    | EncText a ->
        let ytext, fresh = ensureText parent key path ()
        if fresh then ytext.insert (0, Text.toString (AVal.force a))
    | EncObject props ->
        let self = ensureChildMap parent key path
        for (k, child) in props do
            flush doc origin self k (ObjectKey k :: path) child
    | EncMap items ->
        let self = ensureChildMap parent key path
        for (k, child) in AMap.force items |> HashMap.toSeq do
            flush doc origin self k (MapKey k :: path) child
    | EncList l ->
        let yarr = ensureArray parent key path ()
        if (yarr.toArray ()).Count = 0 then
            let items = AList.force l |> IndexList.toList |> List.map primToObj
            if not (List.isEmpty items) then yarr.push (Array.ofList items)
    | EncOption (isSome, inner) ->
        if AVal.force isSome then flush doc origin parent key path inner
    | EncAtomic inner ->
        match Element.ofEncoded inner with
        | Some el -> (parent ()).set (key, plainOfElement el) |> ignore
        | None -> ()
    | EncCustom _ ->
        // A custom manages its own writes through Connect; nothing to flush.
        ()

/// Subscribe an encoding's adaptive views so subsequent local changes flow to
/// the doc as deltas. Writes nothing at attach time.
let rec private attachNode
    (doc : Y.Doc) (attachment : Attachment)
    (parent : EnsureMap) (key : string) (path : Path) (e : Encoded) : unit =
    match e with
    | EncValue a -> attachValue doc attachment parent key a
    | EncText a -> attachText doc attachment (ensureText parent key path) a
    | EncObject props -> attachObject doc attachment (ensureChildMap parent key path) path props
    | EncMap items -> attachMap doc attachment (ensureChildMap parent key path) path items
    | EncList l -> attachList doc attachment (ensureArray parent key path) l
    | EncOption (isSome, inner) -> attachOption doc attachment parent key path isSome inner
    | EncAtomic inner -> attachAtomic doc attachment parent key inner
    | EncCustom custom -> attachCustom attachment parent key path custom

and private attachValue (doc : Y.Doc) (attachment : Attachment) (parent : EnsureMap) (key : string) (a : aval<Primitive>) : unit =
    let transact (f : unit -> unit) = Y.transact (doc, (fun _ -> f ()), attachment.Origin)
    let mutable initial = true
    attachment.Add (a.AddCallback (fun (p : Primitive) ->
        if initial then initial <- false
        else transact (fun () ->
            let v = primToObj p
            let pm = parent ()
            // Skip no-op re-stamps: a same-value set is a real Y op and
            // would clobber a concurrent peer write for no reason.
            match pm.get key with
            | Some existing when existing = v -> ()
            | _ -> pm.set (key, v) |> ignore)))

and private attachText (doc : Y.Doc) (attachment : Attachment) (ensure : unit -> Y.Text * bool) (a : aval<Text>) : unit =
    let transact (f : unit -> unit) = Y.transact (doc, (fun _ -> f ()), attachment.Origin)
    let mutable last = AVal.force a
    attachment.Add (a.AddCallback (fun (next : Text) ->
        if not (Object.ReferenceEquals (next, last)) && Text.toString next <> Text.toString last then
            transact (fun () ->
                let ytext, fresh = ensure ()
                if fresh then ytext.insert (0, Text.toString next)
                else applySplicesToYText ytext (newSplices last next)
                last <- next)
        elif not (Object.ReferenceEquals (next, last)) then
            last <- next))

and private attachObject (doc : Y.Doc) (attachment : Attachment) (self : EnsureMap) (path : Path) (props : (string * Encoded) list) : unit =
    for (k, child) in props do
        attachNode doc attachment self k (ObjectKey k :: path) child

and private attachMap (doc : Y.Doc) (attachment : Attachment) (self : EnsureMap) (path : Path) (items : amap<string, Encoded>) : unit =
    let origin = attachment.Origin
    let transact (f : unit -> unit) = Y.transact (doc, (fun _ -> f ()), origin)
    let children = System.Collections.Generic.Dictionary<string, Attachment> ()
    let attachItem (k : string) (child : Encoded) =
        let childAttachment = Attachment (origin, ResizeArray ())
        attachNode doc childAttachment self k (MapKey k :: path) child
        children.[k] <- childAttachment
        attachment.Add (childAttachment :> IDisposable)
    // Reader callbacks fire nothing for an empty delta, so an empty map's
    // "initial" callback never happens — subscribe the at-attach items
    // ourselves and skip exactly one callback only when there IS a non-empty
    // registration delta to skip.
    let initialState = AMap.force items
    for (k, child) in HashMap.toSeq initialState do
        attachItem k child
    let mutable pendingInitial = not (HashMap.isEmpty initialState)
    attachment.Add (items.AddCallback (fun _state (delta : HashMapDelta<string, Encoded>) ->
        if pendingInitial then
            pendingInitial <- false
        else
            transact (fun () ->
                for (k, op) in HashMapDelta.toSeq delta do
                    match op with
                    | Set child ->
                        match children.TryGetValue k with
                        | true, old ->
                            // The item's encoding was replaced wholesale
                            // (non-keyed regeneration): re-subscribe and
                            // re-flush INTO the adopted containers.
                            (old :> IDisposable).Dispose ()
                            children.Remove k |> ignore
                            flush doc origin self k (MapKey k :: path) child
                            attachItem k child
                        | _ ->
                            // A new entity: the one eager write — its
                            // creation IS the local edit (U2a discipline,
                            // protected by the app-minted unique key).
                            flush doc origin self k (MapKey k :: path) child
                            attachItem k child
                    | Remove ->
                        match children.TryGetValue k with
                        | true, old ->
                            (old :> IDisposable).Dispose ()
                            children.Remove k |> ignore
                        | _ -> ()
                        (self ()).delete k)))

and private attachList (doc : Y.Doc) (attachment : Attachment) (ensure : unit -> Y.Array<obj>) (l : alist<Primitive>) : unit =
    let transact (f : unit -> unit) = Y.transact (doc, (fun _ -> f ()), attachment.Origin)
    // Same empty-initial quirk as maps: skip one callback only when the
    // at-attach list is non-empty (its registration delta fires then).
    let mutable pendingInitial = not (IndexList.isEmpty (AList.force l))
    attachment.Add (l.AddCallback (fun (state : IndexList<Primitive>) (delta : IndexListDelta<Primitive>) ->
        if pendingInitial then pendingInitial <- false
        else transact (fun () ->
            let yarr = ensure ()
            Ylmish.Y.Delta.applyAdaptiveDelta
                (fun (y : Y.Array<obj>) i items -> y.insert (i, items))
                (fun (ps : Primitive list) -> ps |> List.map primToObj |> Array.ofList)
                (fun y i n -> y.delete (i, n))
                state delta yarr)))

and private attachOption (doc : Y.Doc) (attachment : Attachment) (parent : EnsureMap) (key : string) (path : Path) (isSome : aval<bool>) (inner : Encoded) : unit =
    let origin = attachment.Origin
    let transact (f : unit -> unit) = Y.transact (doc, (fun _ -> f ()), origin)
    let mutable innerAttachment : Attachment option = None
    let attachInner () =
        let a = Attachment (origin, ResizeArray ())
        attachNode doc a parent key path inner
        innerAttachment <- Some a
        attachment.Add (a :> IDisposable)
    // Subscribe the inner views only while Some — the Some-window discipline.
    if AVal.force isSome then attachInner ()
    let mutable initial = true
    attachment.Add (isSome.AddCallback (fun (someNow : bool) ->
        if initial then initial <- false
        else
            match someNow, innerAttachment with
            | true, None ->
                transact (fun () -> flush doc origin parent key path inner)
                attachInner ()
            | false, Some a ->
                (a :> IDisposable).Dispose ()
                innerAttachment <- None
                transact (fun () -> (parent ()).delete key)
            | _ -> ()))

and private attachAtomic (doc : Y.Doc) (attachment : Attachment) (parent : EnsureMap) (key : string) (inner : Encoded) : unit =
    let transact (f : unit -> unit) = Y.transact (doc, (fun _ -> f ()), attachment.Origin)
    let version = subtreeVersion inner
    let mutable initial = true
    attachment.Add (version.AddCallback (fun (_ : int) ->
        if initial then initial <- false
        else transact (fun () ->
            match Element.ofEncoded inner with
            | Some el -> (parent ()).set (key, plainOfElement el) |> ignore
            | None -> ())))

and private attachCustom (attachment : Attachment) (parent : EnsureMap) (key : string) (path : Path) (custom : CustomElement) : unit =
    let ctx : BindContext =
        { GetText = fun () -> ensureText parent key path () |> fst
          GetMap = fun () -> ensureChildMap parent key path ()
          GetArray = fun () -> ensureArray parent key path ()
          Origin = attachment.Origin }
    attachment.Add (custom.Connect ctx)

/// Attach an encoded tree to a doc. The top level must be an object.
///
/// LAYOUT (the wire format): top-level REGISTERS (and options/atomics/customs)
/// live as entries in the argless root map, which exists everywhere and merges
/// per-key — no creation race (U1). Top-level structural CONTAINERS — objects,
/// keyed maps, lists, texts — anchor to NAMED Yjs root types (doc.getMap
/// "todos", doc.getText "body", …), which merge by name (U2b): two offline
/// peers creating the same top-level collection can never clobber each other.
/// Below the top level, containers are created lazily in place; keyed-map
/// items are protected by their app-minted unique keys, and a fixed-path
/// nested record's FIRST write remains the accepted U2a residual race.
let attach (doc : Y.Doc) (encoded : Encoded) : Attachment =
    let attachment = Attachment (box (obj ()), ResizeArray ())
    let rootMap : EnsureMap = fun () -> doc.getMap ()
    match encoded with
    | EncObject props ->
        for (k, child) in props do
            let path = [ ObjectKey k ]
            match child with
            | EncObject nested -> attachObject doc attachment (fun () -> doc.getMap k) path nested
            | EncMap items -> attachMap doc attachment (fun () -> doc.getMap k) path items
            | EncList l -> attachList doc attachment (fun () -> doc.getArray k) l
            | EncText a ->
                let ensure () =
                    let t : Y.Text = doc.getText k
                    t, (t.toString () = "")
                attachText doc attachment ensure a
            | EncValue _ | EncOption _ | EncAtomic _ | EncCustom _ ->
                attachNode doc attachment rootMap k path child
        attachment
    | _ ->
        raise (SchemaDrift (UnexpectedKind ([], "the top-level encoding must be Encode.object")))
