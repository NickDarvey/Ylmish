module Ylmish.Adaptive.Codec

open FSharp.Data.Adaptive
open Yjs

type Validation<'Ok, 'Error> = Result<'Ok, 'Error list>

// From [FsToolkit.ErrorHandling](https://github.com/demystifyfp/FsToolkit.ErrorHandling/blob/master/src/FsToolkit.ErrorHandling/Validation.fs)
// MIT License
// 
// Copyright (c) 2018 DemystifyFP
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
[<RequireQualifiedAccess>]
module internal Validation =

    let inline ok (value: 'ok) : Validation<'ok, 'error> =
        Ok value

    let inline error (error: 'error) : Validation<'ok, 'error> =
        Error [ error ]

    let inline errors (errors: 'error list) : Validation<'ok, 'error> =
        Error errors

    let inline ofResult (result: Result<'ok, 'error>) : Validation<'ok, 'error> =
        Result.mapError List.singleton result

    let inline apply
        (applier: Validation<'okInput -> 'okOutput, 'error>)
        (input: Validation<'okInput, 'error>)
        : Validation<'okOutput, 'error> =
        match applier, input with
        | Ok f, Ok x ->
            Ok (f x)
        | Error errs, Ok _
        | Ok _, Error errs ->
            Error errs
        | Error errs1, Error errs2 ->
            Error (errs1 @ errs2)

    let inline map
        ([<InlineIfLambda>] mapper: 'okInput -> 'okOutput)
        (input: Validation<'okInput, 'error>)
        : Validation<'okOutput, 'error> =
        Result.map mapper input

    let inline zip
        (left: Validation<'left, 'error>)
        (right: Validation<'right, 'error>)
        : Validation<'left * 'right, 'error> =
        match left, right with
        | Ok x1res, Ok x2res ->
            Ok (x1res, x2res)
        | Error e, Ok _
        | Ok _, Error e ->
            Error e
        | Error e1, Error e2 ->
            Error (e1 @ e2)

module private AVal =
    let apply (f: ('a->'b) aval) (x: 'a aval)  = adaptive {
        let! f = f
        let! x = x
        return f x
    }

    let rec traverse (f : 'a -> 'b) (a : 'a aval list) : 'b list aval =
        let (<*>) = apply
        let retn = AVal.init
        let cons head tail = head :: tail
        match a with
        | [] ->
            retn []
        | head::tail ->
            retn cons <*> (AVal.map f head) <*> (traverse f tail)

[<RequireQualifiedAccess>]
type Kind =
    | Value
    | Text
    | List
    | Map
    | Custom

/// Where a custom element is placed within a Y.Doc. `Y.Map` / `Y.Array` here are
/// the Fable.Yjs bindings — under the Option-A layering (plan 0003) the codec,
/// whose whole job is to map to Yjs, names Yjs types directly rather than behind
/// an abstraction. A binding never invents a root name (the `Scheme` owns
/// layout); it only needs its parent + slot to get-or-create convergently. The
/// safe default is `Root` (a top-level root, relying on A1), never a freshly
/// created nested shared type both peers race to make (A3).
type ParentContainer =
    | Root                          // a top-level root, named by Slot (the A3-safe case)
    | InMap   of Y.Map<obj>
    | InArray of Y.Array<obj>

/// The slot a custom element occupies within its `ParentContainer`.
and Slot =
    | Named of string               // root name / map key
    | Index of int                  // array index

/// What a custom element needs in order to attach itself to a Y.Doc. `connect`
/// builds one of these from the current parent/slot/reentrancy-guard and hands
/// it to `CustomElement.Connect` (plan 0003, Step 1).
and BindContext = {
    Doc    : Y.Doc                  // the document
    Parent : ParentContainer        // where this element is placed
    Slot   : Slot                   // its slot within the parent
    Active : bool ref               // the shared reentrancy guard threaded by connect
}

/// Extension seam (plans 0002/0003): a consumer-defined element backed by its
/// own merge strategy, surfaced through the `Element.Custom` case so that the
/// well-known kinds stay closed while open-ended growth goes through a single
/// door. The concrete connect surface (`Connect`) is added by plan 0003, Step 1;
/// for now this reserves the open contract and the union case.
type CustomElement =
    /// The kind this element reports, for error messages / `Kind` dispatch.
    abstract Kind : Kind
    /// Get-or-create the shared type at (`Parent`, `Slot`), wire both sync
    /// directions honouring the shared `Active` reentrancy guard, and return a
    /// disposable that tears *both* directions down. This is the same attach
    /// primitive the built-ins use, so there is one connect contract.
    /// Dispatched by `Y.Doc.connect` (plan 0003, Step 3).
    abstract Connect : BindContext -> System.IDisposable

[<RequireQualifiedAccess>]
type Element<'Value> =
    | Value of 'Value                               // atomic register (last-writer-wins)
    | Text of clist<char>                           // character-level CRDT
    | AList of alist<Element<'Value> option>
    | AMap of amap<string, Element<'Value> option>
    | Custom of CustomElement                        // extension seam (see CustomElement)
    /// Opt-in whole-subtree last-writer-wins (plan 0009): the wrapped subtree is
    /// materialized as a *single* Y value and replaced wholesale each update, the
    /// opposite of the flat-by-default per-field merge nested objects otherwise get.
    /// It round-trips (dematerializes) as an ordinary nested object, so decoding is
    /// unchanged — the marker only steers `materialize`.
    | Atomic of Element<'Value>
    with
    member this.toKind () =
        match this with
        | Value _ -> Kind.Value
        | Text _ -> Kind.Text
        | AList _ -> Kind.List
        | AMap _ -> Kind.Map
        | Custom b -> b.Kind
        | Atomic e -> e.toKind ()

type PathSegment =
    | ObjectKey   of string
    | ArrayIndex  of int
    /// A list item named by a **stable, immutable** identity (a consumer guid),
    /// not its position. The connect-time list walk emits this instead of
    /// `ArrayIndex` when an identity resolver is available (plan 0004, Step 2),
    /// so a collaborative leaf inside a reorderable list gets a root name that
    /// stays with its logical item across concurrent reorder/insert. Positional
    /// `ArrayIndex` remains correct for constant-indexed (append-only) lists.
    ///
    /// Immutability matters: the id *names a persisted root*, so it must never
    /// change for a given item — a fractional **order** index is the wrong thing
    /// here (it is mutable by design); use a guid for identity and keep ordering
    /// as ordinary model state. (Step 0 finding.)
    | KeyById     of string

type Path = PathSegment list

module Path =
    open System.Text

    let toString (path : Path) =
        let sb = StringBuilder ()
        for p in path do
            match p with
            | ObjectKey k ->
                ignore <| sb.Append $".%s{k}"
            | ArrayIndex i ->
                ignore <| sb.Append $"[%i{i}]"
            | KeyById id ->
                ignore <| sb.Append $"[#%s{id}]"
        $"${sb.ToString ()}"

/// A `Scheme` decides how an encoded Element tree is laid out across a Y.Doc's
/// top-level roots — the seam a consumer overrides to choose their own persisted
/// state schema without forking `connect`. Given the path to a collaborative
/// leaf (innermost segment first, as codecs build it), it returns the stable
/// top-level root name under which that leaf is get-or-created.
///
/// The A3 spike forces every collaborative leaf to be a *root* (relying only on
/// A1 root get-or-create); nesting is therefore encoded *by name*. The library
/// ships a pragmatic default (`Scheme.flat`); a richer path-flattened or
/// id-based scheme can be provided in the box later, or supplied by a consumer.
type Scheme = {
    /// The top-level root name for a collaborative leaf at this path.
    RootName : Path -> string
    /// For a list encountered at this path, the item-field name whose value is
    /// each item's stable, immutable id — so list items are named by identity
    /// (`KeyById`) rather than position (`ArrayIndex`). `None` keeps positional
    /// naming (correct for constant-indexed, append-only lists). The connect-time
    /// walk reads this and does the extraction; the `Scheme` stays `Element`-free
    /// (it speaks only `Path` and `string`).
    ListKeyField : Path -> string option
}

module Scheme =
    /// The default, A3-safe scheme: flatten the path to a dotted top-level name
    /// (e.g. `items[2].body` -> `"items.2.body"`). Each collaborative leaf
    /// becomes its own root, named **positionally**. Correct when list positions
    /// are stable (append-only); a list that is concurrently reordered wants
    /// `Scheme.byKey` (or a custom id-aware scheme) so text stays with its item.
    let flat : Scheme = {
        RootName = fun path ->
            path
            |> List.rev
            |> List.map (function
                | ObjectKey k -> k
                | ArrayIndex i -> string i
                | KeyById id -> id)
            |> String.concat "."
        ListKeyField = fun _ -> None
    }

    /// Like `flat`, but names **every** list's items by a stable id read from the
    /// given item field (e.g. `byKey "id"` → `items.<id>.body`). Use this when a
    /// list is reordered concurrently: the id must be **immutable** (a guid), so
    /// the root name stays with the logical item across reorder/insert. Ordering
    /// is separate ordinary state (e.g. a fractional `order` field); see plan 0004.
    let byKey (field : string) : Scheme =
        { flat with ListKeyField = fun _ -> Some field }

type Error =
    | UnexpectedKind of {|
            Path : Path
            Actual : Kind
            Expected : Kind list 
        |}
    | UnexpectedType of {|
            Path : Path
            Actual : System.Type
            Expected : System.Type list 
        |}
    | MissingProperty of {| Path : Path |}

module Error =
    let print (error : Error) =
        match error with
        | UnexpectedKind e -> $"\
            {Path.toString e.Path} is of kind %A{e.Actual} but expected one of %A{e.Expected}"
        | UnexpectedType e -> $"\
            {Path.toString e.Path} is of type %A{e.Actual} but expected one of %A{e.Expected}"
        | MissingProperty e -> $"\
            {Path.toString e.Path} does not exist but was expected."

    let printAll (errors : Error list) =
        let sb = System.Text.StringBuilder ()
        ignore <| sb.Append "Failed to decode, because:"
        ignore <| sb.AppendLine ()
        for error in errors do
            ignore <| sb.Append $"- %s{print error}"
            ignore <| sb.AppendLine ()
        sb.ToString ()

type Decoded<'Result> = Validation<'Result, Error> aval
type Decoder<'Model, 'Element, 'Result> = 'Model -> Path * 'Element -> Decoded<'Result>
//type Decoder<'Result> = Decoder<Element<string>, 'Result>
type Encoded<'Element> = 'Element option aval
type Encoder<'Input, 'Element> = 'Input -> Encoded<'Element>

/// A custom element that surfaces its converged value so a decoder can read it
/// straight off `Element.Custom` — no consumer-threaded cell (completes plan 0005).
/// `Value` is a boxed `aval<_>`; the reader unboxes to the type it expects.
type IValuedElement =
    abstract Value : aval<obj>

/// The element-wise keyed **map** (plan 0008): like `Collection`, but its items are
/// ordinary `Element` object-trees (from the item object-codec), keyed by the
/// model's map key. One top-level `Y.Map` carries membership (`@<k>`) and each
/// item's fields — scalars at `<k>/v/<field>`, text markers at `<k>/t/<field>` with
/// content in a `Y.Text` root `<name>/<k>/<field>`. Decode surfaces each item as an
/// `Element.AMap` (scalars as `Element.Value`, text as its converged-string
/// `Element.Value`), so the item decoder is an ordinary `Decode.object`; the merged
/// value is exposed via `IValuedElement` (no cell). `local` carries each field's
/// type as a `Choice` — `Choice1Of2` scalar value, `Choice2Of2` text content.
module internal MapBinding =
    [<Literal>]
    let private Marker = "@"
    [<Literal>]
    let private Sep = "/"
    [<Literal>]
    let private VTag = "v"
    [<Literal>]
    let private TTag = "t"

    let private textRoot (name : string) (k : string) (field : string) = name + Sep + k + Sep + field

    /// Mirror a whole string into a `Y.Text` by the minimal common-affix diff, so
    /// concurrent edits to the *same* item text field merge character-by-character.
    let private mirrorText (ytext : Y.Text) (newStr : string) =
        let cur = ytext.toString ()
        if cur <> newStr then
            let curLen, newLen = cur.Length, newStr.Length
            let minLen = min curLen newLen
            let mutable p = 0
            while p < minLen && cur.[p] = newStr.[p] do p <- p + 1
            let mutable s = 0
            while s < (minLen - p) && cur.[curLen - 1 - s] = newStr.[newLen - 1 - s] do s <- s + 1
            let delLen = curLen - s - p
            if delLen > 0 then ytext.delete (p, delLen)
            let insLen = newLen - s - p
            if insLen > 0 then ytext.insert (p, newStr.Substring (p, insLen))

    let binding (local : aval<HashMap<string, (string * Choice<string, string>) list>>) : CustomElement =
        let merged = cval (HashMap.empty<string, Element<string>>)
        { new CustomElement with
            member _.Kind = Kind.Custom
            member _.Connect (ctx : BindContext) =
                let name =
                    match ctx.Slot with
                    | Slot.Named n -> n
                    | Slot.Index i -> string i
                let ymap : Y.Map<obj> = ctx.Doc.getMap name
                let active = ctx.Active
                let observed = System.Collections.Generic.HashSet<string> ()
                let observedTexts = ResizeArray<Y.Text> ()

                // A non-marker key `<k>/<tag>/<field>` → (k, tag, field).
                let parseKey (key : string) =
                    let parts = key.Split (Sep.[0])
                    if parts.Length >= 3 then Some (parts.[0], parts.[1], System.String.Join (Sep, parts.[2..]))
                    else None

                // Read the live Yjs (map scalars + text roots) into per-item
                // `Element` object-trees, sorted by key for a deterministic shape.
                let readLive () : HashMap<string, Element<string>> =
                    let items = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, Element<string>>> ()
                    let ensure k =
                        match items.TryGetValue k with
                        | true, d -> d
                        | _ ->
                            let d = System.Collections.Generic.Dictionary<string, Element<string>> ()
                            items.[k] <- d
                            d
                    ymap.forEach (fun v key _ ->
                        if key.StartsWith Marker then
                            ensure (key.Substring Marker.Length) |> ignore
                        else
                            match parseKey key with
                            | Some (k, tag, field) ->
                                let d = ensure k
                                if tag = VTag then d.[field] <- Element.Value (string v)
                                elif tag = TTag then d.[field] <- Element.Value ((ctx.Doc.getText (textRoot name k field)).toString ())
                            | None -> ())
                    items
                    |> Seq.map (fun kv ->
                        let fields = kv.Value |> Seq.map (fun fv -> fv.Key, Some fv.Value) |> HashMap.ofSeq
                        kv.Key, Element.AMap (AMap.ofHashMap fields))
                    |> HashMap.ofSeq

                let textFieldRoots () =
                    let roots = ResizeArray<string * string> ()
                    ymap.forEach (fun _ key _ ->
                        match parseKey key with
                        | Some (k, tag, field) when tag = TTag -> roots.Add (k, field)
                        | _ -> ())
                    roots

                let rec sync () =
                    // Observe any newly-appeared item text roots so remote text edits
                    // (which never touch the map) still refresh `merged`.
                    for (k, field) in textFieldRoots () do
                        let root = textRoot name k field
                        if observed.Add root then
                            let yt = ctx.Doc.getText root
                            yt.observe onText
                            observedTexts.Add yt
                    transact (fun () -> merged.Value <- readLive ())
                and onText (_ : Types.YText.YTextEvent) (_ : Y.Transaction) =
                    if not active.Value then
                        active.Value <- true
                        try sync () finally active.Value <- false

                let onMap (_ : Types.YMap.YMapEvent<obj>) (_ : Y.Transaction) =
                    if not active.Value then
                        active.Value <- true
                        try sync () finally active.Value <- false
                ymap.observe onMap

                let cb =
                    local.AddCallback (fun items ->
                        if not active.Value then
                            active.Value <- true
                            try
                                let target = System.Collections.Generic.Dictionary<string, string> ()
                                let textWrites = ResizeArray<string * string> ()
                                items
                                |> HashMap.iter (fun k fields ->
                                    target.[Marker + k] <- k
                                    for (field, choice) in fields do
                                        match choice with
                                        | Choice1Of2 v -> target.[k + Sep + VTag + Sep + field] <- v
                                        | Choice2Of2 content ->
                                            target.[k + Sep + TTag + Sep + field] <- ""
                                            textWrites.Add (textRoot name k field, content))
                                ctx.Doc.transact (fun _ ->
                                    let live = ResizeArray<string> ()
                                    ymap.forEach (fun _ key _ -> live.Add key)
                                    for key in live do
                                        if not (target.ContainsKey key) then ymap.delete key
                                    for KeyValue (key, v) in target do
                                        match ymap.get key with
                                        | Some cur when string cur = v -> ()
                                        | _ -> ymap.set (key, box v) |> ignore
                                    for (root, content) in textWrites do mirrorText (ctx.Doc.getText root) content)
                                sync ()
                            finally active.Value <- false)
                { new System.IDisposable with
                    member _.Dispose () =
                        ymap.unobserve onMap
                        for yt in observedTexts do yt.unobserve onText
                        cb.Dispose () }
          interface IValuedElement with
            member _.Value = merged |> AVal.map box }

/// A keyless **CRDT sequence** (plan 0008) over a top-level `Y.Array` of values:
/// concurrent inserts/removes merge (both adds survive; order is CRDT-resolved) —
/// but there is no per-item identity, so you cannot address/update an element in
/// place; the value *is* the content. Reconciled by the minimal common-affix diff
/// (like `Encode.text`, one level up), so edits at different positions compose. The
/// merged `string list` is exposed via `IValuedElement` (no cell).
module internal SequenceBinding =
    /// Replace only the changed middle (keep the shared prefix/suffix), so
    /// concurrent inserts/removes at different positions merge.
    let private reconcile (arr : Y.Array<obj>) (target : string list) =
        let cur = arr.toArray () |> Seq.map string |> Seq.toArray
        let tgt = List.toArray target
        let curLen, tgtLen = cur.Length, tgt.Length
        let minLen = min curLen tgtLen
        let mutable p = 0
        while p < minLen && cur.[p] = tgt.[p] do p <- p + 1
        let mutable s = 0
        while s < (minLen - p) && cur.[curLen - 1 - s] = tgt.[tgtLen - 1 - s] do s <- s + 1
        let delLen = curLen - s - p
        if delLen > 0 then arr.delete (float p, float delLen)
        let insLen = tgtLen - s - p
        if insLen > 0 then arr.insert (float p, tgt.[p .. p + insLen - 1] |> Array.map box)

    let binding (local : aval<string list>) : CustomElement =
        let merged = cval ([] : string list)
        { new CustomElement with
            member _.Kind = Kind.Custom
            member _.Connect (ctx : BindContext) =
                let name =
                    match ctx.Slot with
                    | Slot.Named n -> n
                    | Slot.Index i -> string i
                let arr : Y.Array<obj> = ctx.Doc.getArray name
                let active = ctx.Active
                let read () = arr.toArray () |> Seq.map string |> Seq.toList
                let sync () = transact (fun () -> merged.Value <- read ())
                let observe (_ : Y.Array.Event<obj>) (_ : Y.Transaction) =
                    if not active.Value then
                        active.Value <- true
                        try sync () finally active.Value <- false
                arr.observe observe
                let cb =
                    local.AddCallback (fun items ->
                        if not active.Value then
                            active.Value <- true
                            try
                                ctx.Doc.transact (fun _ -> reconcile arr items)
                                sync ()
                            finally active.Value <- false)
                { new System.IDisposable with
                    member _.Dispose () =
                        arr.unobserve observe
                        cb.Dispose () }
          interface IValuedElement with
            member _.Value = merged |> AVal.map box }

module Encode =
        
    let object (props : (string * Encoded<_>) list) : Encoded<_> =
        props
        |> List.map (fun (key, value) -> value |> AVal.map (fun v -> key, v))
        |> AVal.traverse id
        |> AMap.ofAVal
        |> Element.AMap
        |> Some
        |> AVal.constant

    let option (a : 'a option aval) : Encoded<Element<'a>> =
        a |> AVal.map (Option.map Element.Value)

    let value f (a : 'a aval) : Encoded<Element<'b>> =
        a |> AVal.map f |> AVal.map Element.Value |> AVal.map Some

    let inline valueWith a f = value f a

    /// Mirror a new whole-string value into a stable clist<char> by applying the
    /// minimal common-affix diff (5A): keep the shared prefix/suffix, replace
    /// only the changed middle. This recovers character operations from the
    /// successive immutable strings the Elmish/Adaptive layer hands us — the same
    /// job Adaptive already does for lists, one level down.
    let private mirrorString (chars : clist<char>) (oldStr : string) (newStr : string) =
        let oldLen = oldStr.Length
        let newLen = newStr.Length
        let minLen = min oldLen newLen
        let mutable p = 0
        while p < minLen && oldStr.[p] = newStr.[p] do
            p <- p + 1
        let mutable s = 0
        while s < (minLen - p) && oldStr.[oldLen - 1 - s] = newStr.[newLen - 1 - s] do
            s <- s + 1
        // Delete the changed middle of the old string: [p, oldLen - s)
        for _ in 1 .. (oldLen - s - p) do
            chars.RemoveAt p |> ignore
        // Insert the changed middle of the new string: [p, newLen - s)
        for i in p .. (newLen - s - 1) do
            chars.InsertAt (i, newStr.[i]) |> ignore

    /// Encode a plain `string` field as character-level CRDT text (`Element.Text`).
    /// The model field stays an immutable F# string; the encoder owns a stable
    /// `clist<char>` that it keeps mirrored to the latest string via `mirrorString`.
    /// Diffing against the clist's *current* contents is the single reconciliation
    /// point: it yields a minimal delta for whole-string replacement and naturally
    /// suppresses echoes (a value that already matches produces no delta).
    let text (a : aval<string>) : Encoded<Element<'b>> =
        let chars : clist<char> = clist []
        a.AddCallback (fun newStr ->
            let current = System.String.Concat chars
            if newStr <> current then
                transact (fun () -> mirrorString chars current newStr)
        ) |> ignore
        AVal.constant (Some (Element.Text chars))

    /// Encode a consumer-defined collaborative field as `Element.Custom`. The
    /// `binding` owns its Yjs get-or-create and bi-directional, delta-level merge
    /// (see `CustomElement` / `BindContext`); `Y.Doc.connect` dispatches it.
    /// Pair it with `Decode.custom` over the same merged-value cell to read the
    /// converged result back into the model — symmetric with `Encode.text` /
    /// `Decode.text`.
    let custom (binding : CustomElement) : Encoded<Element<'b>> =
        AVal.constant (Some (Element.Custom binding))

    let list (f : 'a -> Encoded<Element<'b>>) (a : 'a alist) : Encoded<Element<'b>> =
        a
        |> AList.mapA f
        |> Element.AList
        |> Some
        |> AVal.constant

    let inline listWith a f = list f a

    /// Encode a `bool` scalar as a string-backed value (so it shares an object's
    /// uniform value type with `string` fields). Pair with `Decode.bool`.
    let bool (a : aval<bool>) : Encoded<Element<'b>> =
        value (fun b -> if b then "true" else "false") a

    /// Encode a `HashMap<string, _>` as an **element-wise, id-keyed map** (plan
    /// 0008): concurrent adds/removes merge and per-item scalar fields are per-id
    /// last-writer-wins. `item` is an *object* codec for each value (e.g.
    /// `Encode.object [ ... ]`); the map key is the identity. `Decode.map` reads the
    /// converged map back off the element — no `merged` cell.
    let map (item : 'a -> Encoded<Element<string>>) (items : amap<string, 'a>) : Encoded<Element<'b>> =
        // Force each item's object-codec into a CONCRETE field snapshot
        // (`(field, value) list`) that changes by value when a field edits — re-wrapping
        // the same `Element` reference would defeat change-detection. `item v` is an
        // `AVal.constant` whose fields live in a dynamic inner amap, so bind through it.
        let snapshot (elOpt : Element<string> option) : aval<(string * Choice<string, string>) list> =
            match elOpt with
            | Some (Element.AMap m) ->
                m
                |> AMap.toAVal
                |> AVal.bind (fun fields ->
                    fields
                    |> HashMap.toList
                    |> List.map (fun (f, leaf) ->
                        match leaf with
                        | Some (Element.Value v) -> AVal.constant (Some (f, Choice1Of2 v))
                        // Force the text clist so an edit inside the item propagates.
                        | Some (Element.Text chars) ->
                            chars |> AList.toAVal |> AVal.map (fun cs -> Some (f, Choice2Of2 (System.String.Concat cs)))
                        | _ -> AVal.constant None)
                    |> AVal.traverse id
                    |> AVal.map (List.choose id))
            | _ -> AVal.constant []
        let local =
            items
            |> AMap.mapA (fun _ v -> item v |> AVal.bind snapshot)
            |> AMap.toAVal
        custom (MapBinding.binding local)

    let inline mapWith a f = map f a

    /// Encode a list of string values as a keyless **CRDT sequence** (plan 0008):
    /// concurrent inserts/removes merge; there is no per-item identity, so it is for
    /// values you add/remove/reorder as a whole (tags, log lines), not records you
    /// edit in place. Read back with `Decode.sequence`.
    let sequence (items : alist<string>) : Encoded<Element<'b>> =
        let local = items |> AList.toAVal |> AVal.map IndexList.toList
        custom (SequenceBinding.binding local)

    /// Encode a subtree as a **single last-writer-wins unit** (plan 0009): the
    /// nested object is stored as one Y value and replaced wholesale each update,
    /// so concurrent edits to *different* fields of it clobber rather than merge.
    /// This is the deliberate opt-out from flat-by-default per-field merge — use it
    /// when a record should move atomically (it always replaces as a whole). Wrap an
    /// object encoder: `Encode.atomic (Encode.object [ ... ])`. Read with
    /// `Decode.atomic` (the subtree round-trips as an ordinary nested object).
    let atomic (inner : Encoded<Element<'b>>) : Encoded<Element<'b>> =
        inner |> AVal.map (Option.map Element.Atomic)

module Decoded =
    let inline ofValidation (c : Validation<'a, Error>) : Decoded<'a> = AVal.constant c
    let inline ok (c : 'a) : Decoded<'a> = ofValidation <| Validation.ok c
    let inline error (e : Error) : Decoded<'a> = ofValidation <| Validation.error e
    let inline errors (e : Error list) : Decoded<'a> = ofValidation <| Validation.errors e
    let inline value (a : Decoded<'a>) = AVal.force a

    let inline map (f : 'a -> 'b) (a : Decoded<'a>) :  Decoded<'b> =
        AVal.map (Validation.map f) a

    let inline mapError f =
        AVal.map (Result.mapError f)

    let inline bind (f : 'a -> Decoded<'b>) (a : Decoded<'a>) : Decoded<'b> =
        AVal.bind (function Ok v -> f v | Error e -> errors e) a

    let traversei (f : int -> 'a -> Decoded<'b>) (source : 'a alist) : Decoded<'b IndexList> =
        let folder (i : int, state : Decoded<'b IndexList>) (next : 'a) =
            let result = adaptive {
                let! state = state
                let! next = f i next
                match state, next with
                | Ok state, Ok next ->
                    return Validation.ok <| IndexList.append state (IndexList.single next)
                | Error e, Ok _
                | Ok _, Error e ->
                    return Validation.errors e
                | Error e1, Error e2 ->
                    return Validation.errors (e1 @ e2)
            }
            i + 1, result

        AList.fold folder (0, ok IndexList.empty) source
        // Surely these steps can be avoided?
        |> AVal.map snd 
        |> AVal.bind id


    let flatten (decoded : Decoded<'a aval>) : Decoded<'a> = adaptive {
        match! decoded with
        | Ok value ->
            let! value = value
            return Ok value
        | Error e ->
            return Error e
    }

module Decoder =
    /// Lift a Validation<'a, Failure> to a Decoder<'a>.
    let ofValidation (result : Validation<'Result, Error>) : Decoder<_,_,'Result> =
        fun _ _ -> Decoded.ofValidation result

    /// Creates a Decoder<'a> from an 'a.
    let ok c : Decoder<_,_,_> =
        fun _ _ -> Decoded.ok c

    /// Creates a Decoder<_> from an error.
    let error e : Decoder<_,_,_> =
        fun _ _ -> Decoded.error e

    /// Creates a Decoder<_> from an error, passing the current path.
    let errorAt e : Decoder<_,_,_> =
        fun _ (p, _) -> Decoded.error (e p)

    let map (f : 'a -> 'b) (a : Decoder<_, _, 'a>) : Decoder<_, _, 'b> =
        fun model dep -> a model dep |> Decoded.map f

    let bind (f : 'a -> Decoder<_,_,'b>) (a : Decoder<_,_,'a>) : Decoder<_,_,'b> =
        fun model dep -> a model dep |> Decoded.bind (fun x -> f x model dep)

    let id : Decoder<_, 'a, 'a> =
        fun _ (_, e) -> Decoded.ok e

    /// Returns the current model from the Reader environment.
    let ask : Decoder<'model, _, 'model> =
        fun model _ -> Decoded.ok model

    /// Tries to parse a value to the inferred type using the built-in System parser.
    let inline tryParse (_model : 'm) (path : Path, element : 'a) : Decoded<'b> =
        let mutable value = Unchecked.defaultof< ^b>
        let result = (^b: (static member TryParse: 'a * byref< ^b> -> bool) element, &value)
        if result then Decoded.ok value
        else Decoded.error <| Error.UnexpectedType {| Actual = typeof<string>; Expected = [ typeof<'b> ]; Path = path |}

module Decode = 
    module Element =        
        let value (f : Decoder<_,_,_>) : Decoder<_,_,_> = fun model (path, el) ->
            match el with
            | Element.Value v ->
                f model (path, v)
            | el ->
                Decoded.error <| UnexpectedKind {|
                    Path = path
                    Actual = el.toKind ()
                    Expected = [ Kind.Value ]
                |}

        let list (f : Decoder<_,_,_>) : Decoder<_,_,_> = fun model (path, el) ->
            let decodeAt i el = f model (ArrayIndex i :: path, el)
            match el with
            | Element.AList v ->
                v |> Decoded.traversei decodeAt
            | el ->
                Decoded.error <| UnexpectedKind {|
                    Path = path
                    Actual = el.toKind ()
                    Expected = [ Kind.List ]
                |}

        /// Decode an `Element.Text` back to a plain `string`. Reads the live
        /// `clist<char>` so the decoded value recomputes as the backing text
        /// changes (e.g. a remote CRDT edit), keeping it symmetric with `value`.
        let text : Decoder<_,_,string> = fun _ (path, el) ->
            match el with
            | Element.Text chars ->
                chars
                |> AList.toAVal
                |> AVal.map (fun il -> Validation.ok (System.String.Concat il))
            // A converged text field surfaced by `Encode.map` arrives as a plain
            // value (its merged string); read it as text too.
            | Element.Value v ->
                Decoded.ok (string v)
            | el ->
                Decoded.error <| UnexpectedKind {|
                    Path = path
                    Actual = el.toKind ()
                    Expected = [ Kind.Text ]
                |}

        /// Decode an `Element.Custom` field by reading a consumer-supplied
        /// adaptive view of its merged value. The merge itself is the binding's
        /// job (wired by `connect`); the decoder just surfaces the current value,
        /// recomputing as the binding updates it — symmetric with `text`. The
        /// consumer threads the same value cell into the binding (`Encode.custom`)
        /// and here, so reading never has to reach inside the opaque binding.
        let custom (value : aval<'a>) : Decoder<_,_,'a> = fun _ (path, el) ->
            match el with
            | Element.Custom _ ->
                value |> AVal.map Validation.ok
            | el ->
                Decoded.error <| UnexpectedKind {|
                    Path = path
                    Actual = el.toKind ()
                    Expected = [ Kind.Custom ]
                |}

    let optional (f : Decoder<_,_,_>) : Decoder<_,_,_> = fun model (path, el) ->
        match el with
        | Some el ->
            f model (path, el) |> Decoded.map (fun i -> Some i)
        | None ->
            Decoded.ok None

    let required (f : Decoder<_,_,_>) : Decoder<_,_,_> = fun model (path, el) ->
        match el with
        | Some el -> f model (path, el)
        | None -> Decoded.error <| MissingProperty {| Path = path |}

    let value x = Element.value Decoder.id x

    /// Decode a collaborative-text field as a plain `string`.
    let text : Decoder<_,_,string> = Element.text

    /// Decode a consumer-defined collaborative field by reading the merged-value
    /// cell threaded through `Encode.custom` (see `Element.custom`).
    let custom (value : aval<'a>) : Decoder<_,_,'a> = Element.custom value

    /// Decode a `bool` written by `Encode.bool` (string-backed "true"/"false").
    let bool x = Element.value (fun _ (_, v) -> Decoded.ok (string v = "true")) x

    let private decodeItemsMap (item : Decoder<'m, Element<string>, 'i>) (model : 'm) (path : Path) (items : HashMap<string, Element<string>>) : Decoded<HashMap<string, 'i>> =
        items
        |> HashMap.fold (fun (state : Decoded<HashMap<string, 'i>>) k el ->
            adaptive {
                let! s = state
                let! d = item model (ObjectKey k :: path, el)
                match s, d with
                | Ok m, Ok v -> return Validation.ok (HashMap.add k v m)
                | Error e, Ok _ -> return Validation.errors e
                | Ok _, Error e -> return Validation.errors e
                | Error e1, Error e2 -> return Validation.errors (e1 @ e2)
            }) (Decoded.ok HashMap.empty)

    /// Decode an element-wise `Encode.map`: read the converged item object-trees off
    /// the element (no cell) and run the item *object* decoder over each value.
    let map (item : Decoder<'m, Element<string>, 'i>) : Decoder<'m, Element<string>, HashMap<string, 'i>> =
        fun model (path, el) ->
            match el with
            // Read the converged item trees off the element. An `Encode.map` binding
            // is always an `IValuedElement`; cast directly (Fable can't runtime-test
            // an interface, but the cast is safe by construction).
            | Element.Custom b ->
                (b :?> IValuedElement).Value
                |> AVal.bind (fun o -> decodeItemsMap item model path (unbox<HashMap<string, Element<string>>> o))
            | el ->
                Decoded.error <| UnexpectedKind {| Path = path; Actual = el.toKind (); Expected = [ Kind.Custom ] |}

    /// Decode a keyless `Encode.sequence`: read the converged `string list` off the
    /// element (no cell).
    let sequence : Decoder<_,_,string list> = fun _ (path, el) ->
        match el with
        | Element.Custom b -> (b :?> IValuedElement).Value |> AVal.map (fun o -> Validation.ok (unbox<string list> o))
        | el ->
            Decoded.error <| UnexpectedKind {| Path = path; Actual = el.toKind (); Expected = [ Kind.Custom ] |}

    /// Decode a subtree written by `Encode.atomic`. An atomic subtree round-trips as
    /// an ordinary nested object (`materialize` stores it wholesale; `dematerialize`
    /// reads it back as a plain nested `Element.AMap`), so the inner decoder runs
    /// unchanged — this wrapper only documents the symmetry with `Encode.atomic` at
    /// the call site.
    let atomic (inner : Decoder<'m, Element<'a>, 'r>) : Decoder<'m, Element<'a>, 'r> = inner

    let inline tryParse x = Element.value Decoder.tryParse x

    let key key (f : Decoder<_,_,_>) : Decoder<_,_,_> = fun model (path, el) ->
        match el with
        | Element.AMap v -> adaptive {
                let path = ObjectKey key :: path

                let! value = v |> AMap.tryFind key
                // the outer option is whether we found the key or not
                // the inner option is whether the value is none or not.
                // we flatten them here, but we could instead keep these separate so developers can handle them separately.
                let value = value |> Option.flatten

                return! f model (path, value)
            }
        | el ->
            Decoded.error <| UnexpectedKind {|
                Path = path
                Actual = el.toKind ()
                Expected = [ Kind.Map ]
            |}

    let run (model : 'model) (decoder : Decoder<'model, Element<'a>, 'b>) (input : Encoded<Element<'a>>) : Decoded<'b> =
        input |> AVal.bind (fun i -> required decoder model ([], i))

    type ObjectBuilder () =
        member _.Return x = Decoder.ok x
        member _.Bind (m, f) = Decoder.bind f m
        member _.ReturnFrom m = m
        member _.Zero () = Decoder.ok ()
        member _.Run f = f

        /// Returns the current model from the Reader environment.
        member _.ask () = Decoder.ask

        /// Decodes a property of the object, by its key, which is required.
        member _.required (k : string) f =
            key k (required f)

        /// Decodes a property of the object, by its key, which is optional.
        member _.optional (k : string) f =
            key k (optional f)

    let object = ObjectBuilder ()

    module list =
        /// Decodes a list whose elements are required.
        let required f = Element.list <| required f
        /// Decodes a list whose elements are optional.
        let optional f = Element.list <| optional f

// let (?) decode path = Decoder.key path decode