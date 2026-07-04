namespace Ylmish.Codec

// Plan 0002, Step 2b — target API skeleton for the v2 codec. Signatures are
// the deliverable (this is the design-review checkpoint); bodies are stubs
// until Step 4. The v1 codec (Ylmish.Adaptive.Codec) stays untouched and
// running until Step 7. See doc/plans/0002-ylmish-redesign.md ("The codec, v2").
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
// The Value sub-language — incorrect by construction (design inputs / L1).
// Positions that can only hold JSON primitives (list items, registers) take
// these opaque types, constructible ONLY from the primitives below. There is no
// injection from Encoded, so "a list of texts" or "a list of objects" is a type
// error at the call site, and the fix (Encode.map) is visible in the signature
// you reach for instead.
// -----------------------------------------------------------------------------

module Value =

    type Encoder<'a> = private | EncoderStub
    type Decoder<'a> = private | DecoderStub

    [<RequireQualifiedAccess>]
    module Encode =
        let string : Encoder<string> = EncoderStub
        let int : Encoder<int> = EncoderStub
        let float : Encoder<float> = EncoderStub
        let bool : Encoder<bool> = EncoderStub
        /// Domain types ride a primitive by mapping into it, staying inside the
        /// sub-language: `Value.Encode.contramap TodoId.value Value.Encode.string`.
        let contramap (f : 'b -> 'a) (encoder : Encoder<'a>) : Encoder<'b> = EncoderStub

    [<RequireQualifiedAccess>]
    module Decode =
        let string : Decoder<string> = DecoderStub
        let int : Decoder<int> = DecoderStub
        let float : Decoder<float> = DecoderStub
        let bool : Decoder<bool> = DecoderStub
        let map (f : 'a -> 'b) (decoder : Decoder<'a>) : Decoder<'b> = DecoderStub

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
// Core types. Encoded is an opaque description of "which shared type backs each
// field" — the binding layer walks it (Step 5); consumers only compose it.
// Decoder is a Reader over (current model, decoded position): `Decode.ask`
// returns the current model so app-only fields survive remote updates.
// Unlike v1 there is no aval in the public surface — liveness is the runtime's
// concern (dependency posture: Adaptive trends toward encapsulation).
// -----------------------------------------------------------------------------

type Encoded = private | EncodedStub

type Decoder<'model, 'a> = private | DecoderStub

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
// Encoders — one word per field is the merge choice.
// -----------------------------------------------------------------------------

[<RequireQualifiedAccess>]
module Encode =
    // Step 2b: combinators are INERT stubs — composition (including module-level
    // codec values) must be safe before Step 4; only the runtime entry points throw.

    /// A record: per-key merge of whatever each field chose.
    let object (props : (string * Encoded) list) : Encoded = EncodedStub

    /// An LWW register holding a Value-sub-language primitive.
    let value (encoder : Value.Encoder<'a>) (a : aval<'a>) : Encoded = EncodedStub

    // Sugar over `value` — the codec has exactly one notion of "primitive".
    let string (a : aval<string>) : Encoded = EncodedStub
    let int (a : aval<int>) : Encoded = EncodedStub
    let float (a : aval<float>) : Encoded = EncodedStub
    let bool (a : aval<bool>) : Encoded = EncodedStub

    /// Collaboratively-edited text, backed by a Y.Text: splices interleave.
    let text (a : aval<Text>) : Encoded = EncodedStub

    /// The keyed-collection primitive: element-wise merge by the map key.
    /// Different items never conflict; per-item fields merge per their own
    /// encodings; the shape for anything creatable offline (app-minted keys).
    let map (encodeItem : 'v -> Encoded) (items : amap<string, 'v>) : Encoded = EncodedStub

    /// A sequence of VALUES (insert/delete merge, diff-reconciled). Items are
    /// Value-sub-language primitives by construction — entities with identity
    /// belong in `map` (L1).
    let list (encodeItem : Value.Encoder<'a>) (items : alist<'a>) : Encoded = EncodedStub

    /// Presence/absence of any encoded position. None = the key is absent —
    /// never null (a stored null is unreadable through the Fable binding's
    /// `get`, see the Step 1 lesson).
    let option (encodeSome : 'a -> Encoded) (a : aval<'a option>) : Encoded = EncodedStub

    /// Deliberate wholesale-LWW replacement of an entire subtree (L8).
    let atomic (encoded : Encoded) : Encoded = EncodedStub

    /// The escape hatch: a consumer-defined merge strategy over a real Y type.
    let custom (element : CustomElement) : Encoded = EncodedStub

// -----------------------------------------------------------------------------
// Decoders — a computation expression mirroring v1's ergonomics, minus the
// exposed aval. `Decode.run` is synchronous: model + decoder + doc state in,
// Result out; the runtime drives it on remote transactions (Step 6).
// -----------------------------------------------------------------------------

[<RequireQualifiedAccess>]
module Decode =
    // Step 2b: combinators are INERT stubs (see Encode); only `run` throws.

    /// The current model, from the Reader environment — how app-only fields
    /// survive remote updates and how decode-empty-=-init falls out.
    [<GeneralizableValue>]
    let ask<'model> : Decoder<'model, 'model> = DecoderStub

    [<GeneralizableValue>]
    let text<'model> : Decoder<'model, Text> = DecoderStub

    /// A Value-sub-language primitive out of an LWW register.
    let value (decoder : Value.Decoder<'a>) : Decoder<'model, 'a> = DecoderStub

    [<GeneralizableValue>]
    let string<'model> : Decoder<'model, string> = DecoderStub
    [<GeneralizableValue>]
    let int<'model> : Decoder<'model, int> = DecoderStub
    [<GeneralizableValue>]
    let float<'model> : Decoder<'model, float> = DecoderStub
    [<GeneralizableValue>]
    let bool<'model> : Decoder<'model, bool> = DecoderStub

    let map (decodeItem : Decoder<'model, 'i>) : Decoder<'model, HashMap<string, 'i>> = DecoderStub

    let list (decodeItem : Value.Decoder<'a>) : Decoder<'model, IndexList<'a>> = DecoderStub

    /// Decodes the subtree the way `Encode.atomic` wrote it: as one value.
    let atomic (decodeInner : Decoder<'model, 'a>) : Decoder<'model, 'a> = DecoderStub

    /// Reads the CustomElement's merged `Value`, unboxed under 'a.
    [<GeneralizableValue>]
    let custom<'model, 'a> : Decoder<'model, 'a> = DecoderStub

    /// Run a decoder against the doc's current state. Total: errors are
    /// path-tracked values, not exceptions.
    let run (model : 'model) (decoder : Decoder<'model, 'r>) (doc : Y.Doc) : Result<'r, Error list> =
        failwith "plan 0002: the v2 codec is implemented in Step 4"

    type ObjectBuilder () =
        member _.Return (x : 'a) : Decoder<'model, 'a> = DecoderStub
        member _.ReturnFrom (d : Decoder<'model, 'a>) : Decoder<'model, 'a> = d
        member _.Bind (d : Decoder<'model, 'a>, f : 'a -> Decoder<'model, 'b>) : Decoder<'model, 'b> = DecoderStub
        member _.Zero () : Decoder<'model, unit> = DecoderStub
        member _.Run (d : Decoder<'model, 'a>) : Decoder<'model, 'a> = d

        /// Decode a property of the object by key; missing = MissingProperty.
        member _.required (key : string) (d : Decoder<'model, 'a>) : Decoder<'model, 'a> = DecoderStub

        /// Decode a property of the object by key; missing = None.
        member _.optional (key : string) (d : Decoder<'model, 'a>) : Decoder<'model, 'a option> = DecoderStub

    let object = ObjectBuilder ()
