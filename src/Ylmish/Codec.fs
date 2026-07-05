namespace Ylmish.Codec

// Plan 0002, Step 4 — the v2 codec, for real. Pure schema layer: zero Yjs
// runtime involvement (the `CustomElement`/`BindContext` *types* reference
// Fable.Yjs — the decided dependency posture). The binding runtime arrives in
// Steps 5/6; until then `Decode.run` (against a live doc) stays unimplemented
// and tests exercise the codec through the internal snapshot/element pipeline.
// The v1 codec (Ylmish.Adaptive.Codec) stays untouched and running until
// Step 7. See doc/plans/0002-ylmish-redesign.md ("The codec, v2").
//
// The taxonomy — the model's type is the merge choice:
//
//   Text                     Encode.text    → Y.Text     splices interleave
//   bool/int/float/string    Encode.bool/…  → map entry  LWW register
//   record                   Encode.object  → Y.Map      per-key merge
//   HashMap<string,'T>       Encode.map     → Y.Map      element-wise by key
//   IndexList<'v> (values)   Encode.list    → Y.Array    insert/delete merge
//   any subtree              Encode.atomic  → one value  wholesale LWW
//   anything                 Encode.custom  → your type  consumer-defined

open FSharp.Data.Adaptive
open Yjs

open Ylmish

// -----------------------------------------------------------------------------
// JSON primitives — what Y.Map entries and Y.Array items can hold (U10).
// Numbers are JS numbers (floats); ints ride them and are checked on decode.
// -----------------------------------------------------------------------------

type internal Primitive =
    | PString of string
    | PNumber of float
    | PBool of bool

// -----------------------------------------------------------------------------
// The Value sub-language — incorrect by construction (design inputs / L1).
// Positions that can only hold JSON primitives (list items, registers) take
// these opaque types, constructible ONLY from the primitives below. There is no
// injection from Encoded, so "a list of texts" or "a list of objects" is a type
// error at the call site, and the fix (Encode.map) is visible in the signature
// you reach for instead.
// -----------------------------------------------------------------------------

module Value =

    type Encoder<'a> = internal VEncoder of ('a -> Primitive)
    /// Decodes a primitive; failures carry a message, and the surrounding
    /// decoder adds the path.
    type Decoder<'a> = internal VDecoder of (Primitive -> Result<'a, string>)

    [<RequireQualifiedAccess>]
    module Encode =
        let string : Encoder<string> = VEncoder PString
        let int : Encoder<int> = VEncoder (float >> PNumber)
        let float : Encoder<float> = VEncoder PNumber
        let bool : Encoder<bool> = VEncoder PBool
        /// Domain types ride a primitive by mapping into it, staying inside the
        /// sub-language: `Value.Encode.contramap TodoId.value Value.Encode.string`.
        let contramap (f : 'b -> 'a) (VEncoder e : Encoder<'a>) : Encoder<'b> =
            VEncoder (f >> e)

    [<RequireQualifiedAccess>]
    module Decode =
        let private expected (kind : string) (p : Primitive) : Result<'a, string> =
            let actual =
                match p with
                | PString _ -> "a string"
                | PNumber _ -> "a number"
                | PBool _ -> "a bool"
            Error (sprintf "expected %s but found %s" kind actual)

        let string : Decoder<string> =
            VDecoder (function PString s -> Ok s | p -> expected "a string" p)

        let int : Decoder<int> =
            VDecoder (function
                | PNumber n when System.Double.IsNaN n |> not && float (int n) = n -> Ok (int n)
                | PNumber _ -> Error "expected an integer but found a non-integral number"
                | p -> expected "an integer" p)

        let float : Decoder<float> =
            VDecoder (function PNumber n -> Ok n | p -> expected "a number" p)

        let bool : Decoder<bool> =
            VDecoder (function PBool b -> Ok b | p -> expected "a bool" p)

        let map (f : 'a -> 'b) (VDecoder d : Decoder<'a>) : Decoder<'b> =
            VDecoder (d >> Result.map f)

// -----------------------------------------------------------------------------
// Errors — path-tracked, so a decode failure names where it happened.
// -----------------------------------------------------------------------------

type PathSegment =
    | ObjectKey of string
    | MapKey of string
    | ArrayIndex of int

type Path = PathSegment list

type Error =
    /// The doc holds a different kind at this path than the decoder expects —
    /// including schema drift against an existing Y type (L5).
    | UnexpectedKind of path : Path * message : string
    | UnexpectedValue of path : Path * message : string
    | MissingProperty of path : Path

// -----------------------------------------------------------------------------
// Custom elements — the escape hatch (public by decision; Fable.Yjs types on
// purpose so a consumer can hand a real Y.Text to an editor binding).
// -----------------------------------------------------------------------------

/// Handed to a CustomElement's Connect. Get-or-creates this element's slot in
/// the doc as a given Y type; the binding layer guarantees the instance is
/// integrated exactly once (U5) and adopted, never replaced (U11).
type BindContext =
    { GetText : unit -> Y.Text
      GetMap : unit -> Y.Map<obj>
      GetArray : unit -> Y.Array<obj>
      /// The origin token local writes must be transacted under — echo
      /// suppression (U6), and the seam Y.UndoManager integration would use.
      Origin : obj }

/// A consumer-defined merge strategy. Implementations push local model changes
/// into their Y type inside `doc.transact(_, ctx.Origin)` and keep `Value`
/// current; remote changes surface through the same root observeDeep as
/// everything else (no Subscribe needed under bind-in-place — L6).
type CustomElement =
    abstract Connect : BindContext -> System.IDisposable
    /// Current merged value, read at decode time. Boxed: Decode.custom unboxes
    /// under the consumer's type.
    abstract Value : obj

// -----------------------------------------------------------------------------
// Encoded — the live description the binding layer walks (Step 5). Opaque to
// consumers; internally it keeps hold of the ADAPTIVE views so the runtime can
// observe deltas, not snapshots.
// -----------------------------------------------------------------------------

type Encoded =
    internal
    | EncObject of (string * Encoded) list
    | EncValue of aval<Primitive>
    | EncText of aval<Text>
    | EncMap of amap<string, Encoded>
    | EncList of alist<Primitive>
    /// Presence flag + the inner encoding. The inner adaptive views must only
    /// be forced while the flag is true (Step 5's Some-window projection makes
    /// this airtight; until then `Element.ofEncoded` guards the force).
    | EncOption of isSome : aval<bool> * inner : Encoded
    | EncAtomic of Encoded
    | EncCustom of CustomElement

/// A point-in-time pure tree of an Encoded — what decoding consumes. The
/// binding layer (Step 6) produces these from live doc state; tests produce
/// them from Encoded via `Element.ofEncoded`.
type internal Element =
    | ElValue of Primitive
    | ElText of string
    | ElObject of HashMap<string, Element>
    | ElMap of HashMap<string, Element>
    | ElList of Primitive list
    | ElAtomic of Element
    | ElCustom of CustomElement

module internal Element =
    /// Snapshot an Encoded by forcing its adaptive views. None = an absent
    /// optional. (Step 4 uses this for round-trip tests; Step 5's encode
    /// direction walks Encoded live instead of snapshotting.)
    let rec ofEncoded (e : Encoded) : Element option =
        match e with
        | EncObject props ->
            props
            |> List.choose (fun (k, v) -> ofEncoded v |> Option.map (fun el -> k, el))
            |> HashMap.ofList
            |> ElObject
            |> Some
        | EncValue a -> Some (ElValue (AVal.force a))
        | EncText a -> Some (ElText (AVal.force a |> Text.toString))
        | EncMap m ->
            AMap.force m
            |> HashMap.choose (fun _ v -> ofEncoded v)
            |> ElMap
            |> Some
        | EncList l -> Some (ElList (AList.force l |> IndexList.toList))
        | EncOption (isSome, inner) ->
            if AVal.force isSome then ofEncoded inner else None
        | EncAtomic e -> ofEncoded e |> Option.map ElAtomic
        | EncCustom c -> Some (ElCustom c)

    let kind (el : Element) : string =
        match el with
        | ElValue _ -> "a value"
        | ElText _ -> "a text"
        | ElObject _ -> "an object"
        | ElMap _ -> "a map"
        | ElList _ -> "a list"
        | ElAtomic _ -> "an atomic value"
        | ElCustom _ -> "a custom element"

// -----------------------------------------------------------------------------
// Decoder — a Reader over (current model, path, element). No aval in the
// surface: liveness (when to re-decode) is the runtime's concern (Step 6).
// -----------------------------------------------------------------------------

type Decoder<'model, 'a> =
    internal | Decoder of ('model -> Path -> Element -> Result<'a, Error list>)

// -----------------------------------------------------------------------------
// Encoders — one word per field is the merge choice.
// -----------------------------------------------------------------------------

[<RequireQualifiedAccess>]
module Encode =

    /// A record: per-key merge of whatever each field chose.
    let object (props : (string * Encoded) list) : Encoded = EncObject props

    /// An LWW register holding a Value-sub-language primitive.
    let value (Value.VEncoder e : Value.Encoder<'a>) (a : aval<'a>) : Encoded =
        EncValue (a |> AVal.map e)

    // Sugar over `value` — the codec has exactly one notion of "primitive".
    let string (a : aval<string>) : Encoded = value Value.Encode.string a
    let int (a : aval<int>) : Encoded = value Value.Encode.int a
    let float (a : aval<float>) : Encoded = value Value.Encode.float a
    let bool (a : aval<bool>) : Encoded = value Value.Encode.bool a

    /// Collaboratively-edited text, backed by a Y.Text: splices interleave.
    let text (a : aval<Text>) : Encoded = EncText a

    /// The keyed-collection primitive: element-wise merge by the map key.
    /// Different items never conflict; per-item fields merge per their own
    /// encodings; the shape for anything creatable offline (app-minted keys).
    let map (encodeItem : 'v -> Encoded) (items : amap<string, 'v>) : Encoded =
        EncMap (items |> AMap.map (fun _ v -> encodeItem v))

    /// A sequence of VALUES (insert/delete merge, diff-reconciled). Items are
    /// Value-sub-language primitives by construction — entities with identity
    /// belong in `map` (L1).
    let list (Value.VEncoder e : Value.Encoder<'a>) (items : alist<'a>) : Encoded =
        EncList (items |> AList.map e)

    /// Presence/absence of any single-aval encoding, composing by name:
    /// `Encode.option Encode.text m.Note`, `Encode.option Encode.string m.Nick`.
    /// None = the key is ABSENT — never null (a stored null is unreadable
    /// through the Fable binding's `get`, see the Step 1 lesson). None→Some
    /// creates the backing type lazily at the transition (a local edit);
    /// Some→None deletes the key, and delete beats concurrent edits inside
    /// (U9). Collections have no aval view — an empty map/list is their "none".
    let option (encodeSome : aval<'a> -> Encoded) (a : aval<'a option>) : Encoded =
        // The Some-window projection: while Some, the inner view tracks the
        // payload; across a Some→None transition it HOLDS the last payload
        // (instead of a null-ish default) so any in-flight inner callback that
        // runs before the option's own handler detaches it sees an unchanged
        // value and does nothing. The binding layer never writes while None.
        let inner =
            let mutable lastSome = Unchecked.defaultof<'a>
            a |> AVal.map (function
                | Some v ->
                    lastSome <- v
                    v
                | None -> lastSome)
        EncOption (a |> AVal.map Option.isSome, encodeSome inner)

    /// Deliberate wholesale-LWW replacement of an entire subtree (L8).
    let atomic (encoded : Encoded) : Encoded = EncAtomic encoded

    /// The escape hatch: a consumer-defined merge strategy over a real Y type.
    let custom (element : CustomElement) : Encoded = EncCustom element

// -----------------------------------------------------------------------------
// Decoders — a computation expression mirroring v1's ergonomics.
// -----------------------------------------------------------------------------

[<RequireQualifiedAccess>]
module Decode =

    let internal runElement
        (model : 'model) (Decoder d : Decoder<'model, 'a>) (el : Element)
        : Result<'a, Error list> =
        d model [] el

    /// The current model, from the Reader environment — how app-only fields
    /// survive remote updates and how decode-empty-=-init falls out.
    [<GeneralizableValue>]
    let ask<'model> : Decoder<'model, 'model> =
        Decoder (fun model _ _ -> Ok model)

    [<GeneralizableValue>]
    let text<'model> : Decoder<'model, Text> =
        Decoder (fun _ path el ->
            match el with
            | ElText s -> Ok (Text.ofString s)
            | el -> Error [ UnexpectedKind (path, sprintf "expected a text but found %s" (Element.kind el)) ])

    /// A Value-sub-language primitive out of an LWW register.
    let value (Value.VDecoder d : Value.Decoder<'a>) : Decoder<'model, 'a> =
        Decoder (fun _ path el ->
            match el with
            | ElValue p -> d p |> Result.mapError (fun m -> [ UnexpectedValue (path, m) ])
            | el -> Error [ UnexpectedKind (path, sprintf "expected a value but found %s" (Element.kind el)) ])

    [<GeneralizableValue>]
    let string<'model> : Decoder<'model, string> = value Value.Decode.string
    [<GeneralizableValue>]
    let int<'model> : Decoder<'model, int> = value Value.Decode.int
    [<GeneralizableValue>]
    let float<'model> : Decoder<'model, float> = value Value.Decode.float
    [<GeneralizableValue>]
    let bool<'model> : Decoder<'model, bool> = value Value.Decode.bool

    /// Accumulates errors across items rather than stopping at the first.
    let map (decodeItem : Decoder<'model, 'i>) : Decoder<'model, HashMap<string, 'i>> =
        let (Decoder item) = decodeItem
        Decoder (fun model path el ->
            match el with
            | ElMap items ->
                let mutable oks = HashMap.empty
                let mutable errs = []
                for (k, v) in HashMap.toSeq items do
                    match item model (MapKey k :: path) v with
                    | Ok i -> oks <- HashMap.add k i oks
                    | Error e -> errs <- errs @ e
                if List.isEmpty errs then Ok oks else Error errs
            | el -> Error [ UnexpectedKind (path, sprintf "expected a map but found %s" (Element.kind el)) ])

    /// Accumulates errors across items rather than stopping at the first.
    let list (Value.VDecoder d : Value.Decoder<'a>) : Decoder<'model, IndexList<'a>> =
        Decoder (fun _ path el ->
            match el with
            | ElList ps ->
                let mutable oks = IndexList.empty
                let mutable errs = []
                ps |> List.iteri (fun i p ->
                    match d p with
                    | Ok v -> oks <- IndexList.add v oks
                    | Error m -> errs <- errs @ [ UnexpectedValue (ArrayIndex i :: path, m) ])
                if List.isEmpty errs then Ok oks else Error errs
            | el -> Error [ UnexpectedKind (path, sprintf "expected a list but found %s" (Element.kind el)) ])

    /// Decodes the subtree the way `Encode.atomic` wrote it: as one value.
    let atomic (decodeInner : Decoder<'model, 'a>) : Decoder<'model, 'a> =
        let (Decoder inner) = decodeInner
        Decoder (fun model path el ->
            match el with
            | ElAtomic e -> inner model path e
            | el -> Error [ UnexpectedKind (path, sprintf "expected an atomic value but found %s" (Element.kind el)) ])

    /// Reads the CustomElement's merged `Value`, unboxed under 'a. NB: the
    /// unbox is trust-based under Fable (JS casts are unchecked), so a binding
    /// whose Value type drifts from its decoder surfaces downstream, not here —
    /// both live in the same consumer's hands.
    [<GeneralizableValue>]
    let custom<'model, 'a> : Decoder<'model, 'a> =
        Decoder (fun _ path el ->
            match el with
            | ElCustom c -> Ok (unbox<'a> c.Value)
            | el -> Error [ UnexpectedKind (path, sprintf "expected a custom element but found %s" (Element.kind el)) ])

    /// Run a decoder against the doc's current state. Total: errors are
    /// path-tracked values, not exceptions.
    let run (model : 'model) (decoder : Decoder<'model, 'r>) (doc : Y.Doc) : Result<'r, Error list> =
        failwith "plan 0002: decoding live doc state is the binding runtime's job — Step 6"

    type ObjectBuilder () =
        member _.Return (x : 'a) : Decoder<'model, 'a> =
            Decoder (fun _ _ _ -> Ok x)
        member _.ReturnFrom (d : Decoder<'model, 'a>) : Decoder<'model, 'a> = d
        member _.Bind (Decoder d : Decoder<'model, 'a>, f : 'a -> Decoder<'model, 'b>) : Decoder<'model, 'b> =
            Decoder (fun model path el ->
                match d model path el with
                | Error e -> Error e
                | Ok a ->
                    let (Decoder next) = f a
                    next model path el)
        member _.Zero () : Decoder<'model, unit> =
            Decoder (fun _ _ _ -> Ok ())
        member _.Run (d : Decoder<'model, 'a>) : Decoder<'model, 'a> = d

        /// Decode a property of the object by key; missing = MissingProperty.
        member _.required (key : string) (Decoder d : Decoder<'model, 'a>) : Decoder<'model, 'a> =
            Decoder (fun model path el ->
                match el with
                | ElObject props ->
                    match HashMap.tryFind key props with
                    | Some v -> d model (ObjectKey key :: path) v
                    | None -> Error [ MissingProperty (ObjectKey key :: path) ]
                | el -> Error [ UnexpectedKind (path, sprintf "expected an object but found %s" (Element.kind el)) ])

        /// Decode a property of the object by key; missing = None.
        member _.optional (key : string) (Decoder d : Decoder<'model, 'a>) : Decoder<'model, 'a option> =
            Decoder (fun model path el ->
                match el with
                | ElObject props ->
                    match HashMap.tryFind key props with
                    | Some v -> d model (ObjectKey key :: path) v |> Result.map Some
                    | None -> Ok None
                | el -> Error [ UnexpectedKind (path, sprintf "expected an object but found %s" (Element.kind el)) ])

    let object = ObjectBuilder ()
