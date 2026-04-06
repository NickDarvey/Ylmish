module Ylmish.Adaptive.Codec

open FSharp.Data.Adaptive

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
    | List
    | Map

[<RequireQualifiedAccess>]
type Element<'Value> =
    | Value of 'Value
    | AList of alist<Element<'Value> option>
    | AMap of amap<string, Element<'Value> option>
    with 
    member this.toKind () =
        match this with
        | Value _ -> Kind.Value
        | AList _ -> Kind.List
        | AMap _ -> Kind.Map

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