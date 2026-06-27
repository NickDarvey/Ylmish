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
    with
    member this.toKind () =
        match this with
        | Value _ -> Kind.Value
        | Text _ -> Kind.Text
        | AList _ -> Kind.List
        | AMap _ -> Kind.Map
        | Custom b -> b.Kind

type PathSegment = 
    | ObjectKey   of string
    | ArrayIndex  of int

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
}

module Scheme =
    /// The default, A3-safe scheme: flatten the path to a dotted top-level name
    /// (e.g. `items[2].body` -> `"items.2.body"`). Each collaborative leaf
    /// becomes its own root. Suitable when leaf positions are stable;
    /// collaborative text nested inside a concurrently-reordered list wants a
    /// custom, id-based scheme instead (the seam this type exists to provide).
    let flat : Scheme = {
        RootName = fun path ->
            path
            |> List.rev
            |> List.map (function ObjectKey k -> k | ArrayIndex i -> string i)
            |> String.concat "."
    }

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

    let list (f : 'a -> Encoded<Element<'b>>) (a : 'a alist) : Encoded<Element<'b>> =
        a
        |> AList.mapA f
        |> Element.AList
        |> Some
        |> AVal.constant

    let inline listWith a f = list f a

    let map f a : Encoded<Element<'b>> =
        a
        |> AMap.mapA (fun _ v -> f v)
        |> Element.AMap
        |> Some
        |> AVal.constant

    let inline mapWith a f = map f a

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
            | el ->
                Decoded.error <| UnexpectedKind {|
                    Path = path
                    Actual = el.toKind ()
                    Expected = [ Kind.Text ]
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