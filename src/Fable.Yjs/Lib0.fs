// ts2fable 0.7.1
// Hand-maintained since: there is no regeneration step. Most modules stay
// commented out; a module is live only where something needs it.
module rec Lib0
#nowarn "1182"

open System
open Fable.Core
open Fable.Core.JS
// open Browser.Types

type Array<'T> = System.Collections.Generic.IList<'T>
type Error = System.Exception
type Function = System.Action
type Symbol = obj


// module Array =

//     type [<AllowNullLiteral>] IExports =
//         abstract last: arr: ResizeArray<'L> -> 'L
//         abstract create: unit -> ResizeArray<'C>
//         abstract copy: a: ResizeArray<'D> -> ResizeArray<'D>
//         abstract appendTo: dest: ResizeArray<'M> * src: ResizeArray<'M> -> unit
//         abstract from: IExportsFrom
//         abstract every: arr: ResizeArray<'ITEM> * f: ('ITEM -> float -> ResizeArray<'ITEM> -> bool) -> bool
//         abstract some: arr: ResizeArray<'S> * f: ('S -> float -> ResizeArray<'S> -> bool) -> bool
//         abstract equalFlat: a: ResizeArray<'ELEM> * b: ResizeArray<'ELEM> -> bool
//         abstract flatten: arr: ResizeArray<ResizeArray<'ELEM>> -> ResizeArray<'ELEM>
//         abstract isArray: (obj option -> bool)

//     type [<AllowNullLiteral>] IExportsFrom =
//         [<Emit "$0($1...)">] abstract Invoke: arrayLike: ArrayLike<T_1> -> ResizeArray<T_1>
//         [<Emit "$0($1...)">] abstract Invoke: arrayLike: ArrayLike<T_2> * mapfn: (T_2 -> float -> U) * ?thisArg: obj -> ResizeArray<U>
//         [<Emit "$0($1...)">] abstract Invoke: iterable: U2<Iterable<T_3>, ArrayLike<T_3>> -> ResizeArray<T_3>
//         [<Emit "$0($1...)">] abstract Invoke: iterable: U2<Iterable<T_4>, ArrayLike<T_4>> * mapfn: (T_4 -> float -> U_1) * ?thisArg: obj -> ResizeArray<U_1>

// // module Array_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testAppend: tc: T.TestCase -> unit
// //         abstract testflatten: tc: T.TestCase -> unit
// //         abstract testIsArray: tc: T.TestCase -> unit

// module Binary =

//     type [<AllowNullLiteral>] IExports =
//         abstract BIT1: float
//         abstract BIT2: obj
//         abstract BIT3: obj
//         abstract BIT4: obj
//         abstract BIT5: obj
//         abstract BIT6: obj
//         abstract BIT7: obj
//         abstract BIT8: obj
//         abstract BIT9: obj
//         abstract BIT10: obj
//         abstract BIT11: obj
//         abstract BIT12: obj
//         abstract BIT13: obj
//         abstract BIT14: obj
//         abstract BIT15: obj
//         abstract BIT16: obj
//         abstract BIT17: obj
//         abstract BIT18: float
//         abstract BIT19: float
//         abstract BIT20: float
//         abstract BIT21: float
//         abstract BIT22: float
//         abstract BIT23: float
//         abstract BIT24: float
//         abstract BIT25: float
//         abstract BIT26: float
//         abstract BIT27: float
//         abstract BIT28: float
//         abstract BIT29: float
//         abstract BIT30: float
//         abstract BIT31: float
//         abstract BIT32: float
//         abstract BITS0: float
//         abstract BITS1: obj
//         abstract BITS2: obj
//         abstract BITS3: obj
//         abstract BITS4: obj
//         abstract BITS5: obj
//         abstract BITS6: obj
//         abstract BITS7: obj
//         abstract BITS8: obj
//         abstract BITS9: obj
//         abstract BITS10: obj
//         abstract BITS11: obj
//         abstract BITS12: obj
//         abstract BITS13: obj
//         abstract BITS14: obj
//         abstract BITS15: obj
//         abstract BITS16: obj
//         abstract BITS17: float
//         abstract BITS18: float
//         abstract BITS19: float
//         abstract BITS20: float
//         abstract BITS21: float
//         abstract BITS22: float
//         abstract BITS23: float
//         abstract BITS24: float
//         abstract BITS25: float
//         abstract BITS26: float
//         abstract BITS27: float
//         abstract BITS28: float
//         abstract BITS29: float
//         abstract BITS30: float
//         abstract BITS31: float
//         abstract BITS32: float

// // module Binary_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testBitx: tc: T.TestCase -> unit
// //         abstract testBitsx: tc: T.TestCase -> unit

// module Broadcastchannel =

//     type [<AllowNullLiteral>] IExports =
//         abstract subscribe: room: string * f: (obj option -> obj option -> obj option) -> Set<(obj option -> obj option -> obj option)>
//         abstract unsubscribe: room: string * f: (obj option -> obj option -> obj option) -> bool
//         abstract publish: room: string * data: obj option * ?origin: obj -> unit

//     type [<AllowNullLiteral>] Channel =
//         abstract subs: Set<(obj option -> obj option -> obj option)> with get, set
//         abstract bc: obj option with get, set

/// lib0/buffer. Hand-written rather than restored from the ts2fable block this
/// replaced, keeping only the members with a caller: base64 is how a Yjs
/// update (`Y.encodeStateAsUpdate`, `Doc.onUpdate`) travels over a text wire.
module Buffer =
    /// Base64-encode bytes (standard alphabet, padded).
    [<ImportMember("lib0/buffer")>]
    let toBase64 (bytes: Uint8Array) : string = jsNative

    /// Decode a base64 string produced by `toBase64`.
    [<ImportMember("lib0/buffer")>]
    let fromBase64 (s: string) : Uint8Array = jsNative

// // module Buffer_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testRepeatBase64Encoding: tc: T.TestCase -> unit
// //         abstract testAnyEncoding: tc: T.TestCase -> unit

// module Cache =
//     module List = List

//     type [<AllowNullLiteral>] IExports =
//         abstract Cache: CacheStatic
//         abstract removeStale: cache: Cache<'K, 'V> -> float
//         abstract set: cache: Cache<'K, 'V> * key: 'K * value: 'V -> unit
//         abstract get: cache: Cache<'K, 'V> * key: 'K -> 'V option
//         abstract refreshTimeout: cache: Cache<'K, 'V> * key: 'K -> unit
//         abstract getAsync: cache: Cache<'K, 'V> * key: 'K -> U2<'V, Promise<'V>> option
//         abstract remove: cache: Cache<'K, 'V> * key: 'K -> 'V option
//         abstract setIfUndefined: cache: Cache<'K, 'V> * key: 'K * init: (unit -> Promise<'V>) * ?removeNull: bool -> U2<'V, Promise<'V>>
//         abstract create: timeout: float -> Cache<obj option, obj option>
//         abstract Entry: EntryStatic

//     type [<AllowNullLiteral>] Cache<'K, 'V> =
//         abstract timeout: float with get, set
//         abstract _q: List.List<Entry<'K, 'V>> with get, set
//         abstract _map: Map<'K, Entry<'K, 'V>> with get, set

//     type [<AllowNullLiteral>] CacheStatic =
//         [<Emit "new $0($1...)">] abstract Create: timeout: float -> Cache<'K, 'V>

//     type [<AllowNullLiteral>] Entry<'K, 'V> =
//         inherit List.ListNode
//         abstract prev: Entry<'K, 'V> option with get, set
//         abstract next: Entry<'K, 'V> option with get, set
//         abstract created: float with get, set
//         abstract ``val``: U2<'V, Promise<'V>> with get, set
//         abstract key: 'K with get, set

//     type [<AllowNullLiteral>] EntryStatic =
//         [<Emit "new $0($1...)">] abstract Create: key: 'K * ``val``: U2<'V, Promise<'V>> -> Entry<'K, 'V>

// // module Cache_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testCache: tc: T.TestCase -> Promise<unit>

// module Component =

//     type [<AllowNullLiteral>] IExports =
//         abstract registry: CustomElementRegistry
//         abstract define: name: string * constr: obj option * ?opts: ElementDefinitionOptions -> unit
//         abstract whenDefined: name: string -> Promise<CustomElementConstructor>
//         abstract Lib0Component: Lib0ComponentStatic
//         abstract createComponent: name: string * p1: CONF<'T> -> obj
//         abstract createComponentDefiner: definer: Function -> (unit -> obj option)
//         abstract defineListComponent: unit -> obj option
//         abstract defineLazyLoadingComponent: unit -> obj option

//     type [<AllowNullLiteral>] Lib0Component<'S> =
//         inherit HTMLElement
//         abstract state: 'S option with get, set
//         abstract _internal: obj option with get, set
//         /// <param name="forceStateUpdate">Force that the state is rerendered even if state didn't change</param>
//         abstract setState: state: 'S * ?forceStateUpdate: bool -> unit
//         abstract updateState: stateUpdate: obj option -> unit

//     type [<AllowNullLiteral>] Lib0ComponentStatic =
//         [<Emit "new $0($1...)">] abstract Create: ?state: 'S -> Lib0Component<'S>

//     type [<AllowNullLiteral>] CONF<'S> =
//         /// Template for the shadow dom.
//         abstract template: string option with get, set
//         /// shadow dom style. Is only used when
//         /// `CONF.template` is defined
//         abstract style: string option with get, set
//         /// Initial component state.
//         abstract state: 'S option with get, set
//         /// Called when
//         /// the state changes.
//         abstract onStateChange: ('S -> 'S option -> Lib0Component<'S> -> unit) option with get, set
//         /// maps from
//         /// CSS-selector to transformer function. The first element that matches the
//         /// CSS-selector receives state updates via the transformer function.
//         abstract childStates: CONFChildStates option with get, set
//         /// attrs-keys and state-keys should be camelCase, but the DOM uses kebap-case. I.e.
//         /// `attrs = { myAttr: 4 }` is represeted as `<my-elem my-attr="4" />` in the DOM
//         abstract attrs: CONFAttrs option with get, set
//         /// Maps from dom-event-name
//         /// to event listener.
//         abstract listeners: CONFListeners option with get, set
//         /// Fill slots
//         /// automatically when state changes. Maps from slot-name to slot-html.
//         abstract slots: ('S -> 'S -> Lib0Component<'S> -> CONFSlots) option with get, set

//     type [<AllowNullLiteral>] CONFChildStates =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> (obj option -> obj option -> Object) with get, set

//     type [<StringEnum>] [<RequireQualifiedAccess>] CONFAttrsItem =
//         | String
//         | Number
//         | Json
//         | Bool

//     type [<AllowNullLiteral>] CONFAttrs =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> CONFAttrsItem with get, set

//     type [<AllowNullLiteral>] CONFListeners =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> (CustomEvent -> Lib0Component<obj option> -> U2<bool, unit>) with get, set

//     type [<AllowNullLiteral>] CONFSlots =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> string with get, set

// module Conditions =

//     type [<AllowNullLiteral>] IExports =
//         abstract undefinedToNull: v: 'T option -> 'T option

module Decoding =

    type [<AllowNullLiteral>] IExports =
        abstract Decoder: DecoderStatic
        abstract createDecoder: uint8Array: Uint8Array -> Decoder
        abstract hasContent: decoder: Decoder -> bool
        abstract clone: decoder: Decoder * ?newPos: float -> Decoder
        abstract readUint8Array: decoder: Decoder * len: float -> Uint8Array
        abstract readVarUint8Array: decoder: Decoder -> Uint8Array
        abstract readTailAsUint8Array: decoder: Decoder -> Uint8Array
        abstract skip8: decoder: Decoder -> float
        abstract readUint8: decoder: Decoder -> float
        abstract readUint16: decoder: Decoder -> float
        abstract readUint32: decoder: Decoder -> float
        abstract readUint32BigEndian: decoder: Decoder -> float
        abstract peekUint8: decoder: Decoder -> float
        abstract peekUint16: decoder: Decoder -> float
        abstract peekUint32: decoder: Decoder -> float
        abstract readVarUint: decoder: Decoder -> float
        abstract readVarInt: decoder: Decoder -> float
        abstract peekVarUint: decoder: Decoder -> float
        abstract peekVarInt: decoder: Decoder -> float
        abstract readVarString: decoder: Decoder -> string
        abstract peekVarString: decoder: Decoder -> string
        abstract readFromDataView: decoder: Decoder * len: float -> DataView
        abstract readFloat32: decoder: Decoder -> float
        abstract readFloat64: decoder: Decoder -> float
        abstract readBigInt64: decoder: Decoder -> obj option
        abstract readBigUint64: decoder: Decoder -> obj option
        abstract readAny: decoder: Decoder -> obj option
        abstract RleDecoder: RleDecoderStatic
        abstract IntDiffDecoder: IntDiffDecoderStatic
        abstract RleIntDiffDecoder: RleIntDiffDecoderStatic
        abstract UintOptRleDecoder: UintOptRleDecoderStatic
        abstract IncUintOptRleDecoder: IncUintOptRleDecoderStatic
        abstract IntDiffOptRleDecoder: IntDiffOptRleDecoderStatic
        abstract StringDecoder: StringDecoderStatic

    /// A Decoder handles the decoding of an Uint8Array.
    type [<AllowNullLiteral>] Decoder =
        /// Decoding target.
        abstract arr: Uint8Array with get, set
        /// Current decoding position.
        abstract pos: float with get, set

    /// A Decoder handles the decoding of an Uint8Array.
    type [<AllowNullLiteral>] DecoderStatic =
        /// <param name="uint8Array">Binary data to decode</param>
        [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array -> Decoder

    /// T must not be null.
    type [<AllowNullLiteral>] RleDecoder<'T> =
        inherit Decoder
        /// The reader
        abstract reader: (Decoder -> 'T) with get, set
        /// Current state
        abstract s: 'T option with get, set
        abstract count: float with get, set
        abstract read: unit -> 'T

    /// T must not be null.
    type [<AllowNullLiteral>] RleDecoderStatic =
        [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array * reader: (Decoder -> 'T) -> RleDecoder<'T>

    type [<AllowNullLiteral>] IntDiffDecoder =
        inherit Decoder
        /// Current state
        abstract s: float with get, set
        abstract read: unit -> float

    type [<AllowNullLiteral>] IntDiffDecoderStatic =
        [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array * start: float -> IntDiffDecoder

    type [<AllowNullLiteral>] RleIntDiffDecoder =
        inherit Decoder
        /// Current state
        abstract s: float with get, set
        abstract count: float with get, set
        abstract read: unit -> float

    type [<AllowNullLiteral>] RleIntDiffDecoderStatic =
        [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array * start: float -> RleIntDiffDecoder

    type [<AllowNullLiteral>] UintOptRleDecoder =
        inherit Decoder
        abstract s: float with get, set
        abstract count: float with get, set
        abstract read: unit -> float

    type [<AllowNullLiteral>] UintOptRleDecoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> UintOptRleDecoder

    type [<AllowNullLiteral>] IncUintOptRleDecoder =
        inherit Decoder
        abstract s: float with get, set
        abstract count: float with get, set
        abstract read: unit -> float

    type [<AllowNullLiteral>] IncUintOptRleDecoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> IncUintOptRleDecoder

    type [<AllowNullLiteral>] IntDiffOptRleDecoder =
        inherit Decoder
        abstract s: float with get, set
        abstract count: float with get, set
        abstract diff: float with get, set
        abstract read: unit -> float

    type [<AllowNullLiteral>] IntDiffOptRleDecoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> IntDiffOptRleDecoder

    type [<AllowNullLiteral>] StringDecoder =
        abstract decoder: UintOptRleDecoder with get, set
        abstract str: string with get, set
        abstract spos: float with get, set
        abstract read: unit -> string

    type [<AllowNullLiteral>] StringDecoderStatic =
        [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array -> StringDecoder

// module Diff =

//     type [<AllowNullLiteral>] IExports =
//         abstract simpleDiffString: a: string * b: string -> SimpleDiff<string>
//         abstract simpleDiff: a: string * b: string -> SimpleDiff<string>
//         abstract simpleDiffArray: a: ResizeArray<'T> * b: ResizeArray<'T> * ?compare: ('T -> 'T -> bool) -> SimpleDiff<ResizeArray<'T>>
//         abstract simpleDiffStringWithCursor: a: string * b: string * cursor: float -> SimpleDiffStringWithCursorReturn

//     type [<AllowNullLiteral>] SimpleDiffStringWithCursorReturn =
//         abstract index: float with get, set
//         abstract remove: float with get, set
//         abstract insert: string with get, set

//     type [<AllowNullLiteral>] SimpleDiff<'T> =
//         /// The index where changes were applied
//         abstract index: float with get, set
//         /// The number of characters to delete starting
//         ///         at `index`.
//         abstract remove: float with get, set
//         /// The new text to insert at `index` after applying
//         ///       `delete`
//         abstract insert: 'T with get, set

// // module Diff_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testDiffing: tc: T.TestCase -> unit
// //         abstract testRepeatDiffing: tc: T.TestCase -> unit
// //         abstract testSimpleDiffWithCursor: tc: T.TestCase -> unit
// //         abstract testArrayDiffing: tc: T.TestCase -> unit

// module Dom =
//     module Pair = Pair

//     type [<AllowNullLiteral>] IExports =
//         abstract doc: Document
//         abstract createElement: name: string -> HTMLElement
//         abstract createDocumentFragment: unit -> DocumentFragment
//         abstract createTextNode: text: string -> Text
//         abstract domParser: DOMParser
//         abstract emitCustomEvent: el: HTMLElement * name: string * opts: Object -> bool
//         abstract setAttributes: el: Element * attrs: Array<Pair.Pair<string, U2<string, bool>>> -> Element
//         abstract setAttributesMap: el: Element * attrs: Map<string, string> -> Element
//         abstract fragment: children: U2<Array<Node>, HTMLCollection> -> DocumentFragment
//         abstract append: parent: Element * nodes: Array<Node> -> Element
//         abstract remove: el: HTMLElement -> unit
//         abstract addEventListener: el: EventTarget * name: string * f: EventListener -> unit
//         abstract removeEventListener: el: EventTarget * name: string * f: EventListener -> unit
//         abstract addEventListeners: node: Node * listeners: Array<Pair.Pair<string, EventListener>> -> Node
//         abstract removeEventListeners: node: Node * listeners: Array<Pair.Pair<string, EventListener>> -> Node
//         abstract element: name: string * ?attrs: Array<U2<Pair.Pair<string, string>, Pair.Pair<string, bool>>> * ?children: Array<Node> -> Element
//         abstract canvas: width: float * height: float -> HTMLCanvasElement
//         abstract text: text: string -> Text
//         abstract pairToStyleString: pair: Pair.Pair<string, string> -> string
//         abstract pairsToStyleString: pairs: Array<Pair.Pair<string, string>> -> string
//         abstract mapToStyleString: m: Map<string, string> -> string
//         abstract querySelector: el: U2<HTMLElement, ShadowRoot> * query: string -> HTMLElement option
//         abstract querySelectorAll: el: U2<HTMLElement, ShadowRoot> * query: string -> NodeListOf<HTMLElement>
//         abstract getElementById: id: string -> HTMLElement
//         abstract parseFragment: html: string -> DocumentFragment
//         abstract parseElement: html: string -> HTMLElement
//         abstract replaceWith: oldEl: HTMLElement * newEl: U2<HTMLElement, DocumentFragment> -> unit
//         abstract insertBefore: parent: HTMLElement * el: HTMLElement * ref: Node option -> HTMLElement
//         abstract appendChild: parent: Node * child: Node -> Node
//         abstract ELEMENT_NODE: float
//         abstract TEXT_NODE: float
//         abstract CDATA_SECTION_NODE: float
//         abstract COMMENT_NODE: float
//         abstract DOCUMENT_NODE: float
//         abstract DOCUMENT_TYPE_NODE: float
//         abstract DOCUMENT_FRAGMENT_NODE: float
//         abstract checkNodeType: node: obj option * ``type``: float -> bool
//         abstract isParentOf: parent: Node * child: HTMLElement -> bool

module Encoding =

    type [<AllowNullLiteral>] IExports =
        abstract Encoder: EncoderStatic
        abstract createEncoder: unit -> Encoder
        abstract length: encoder: Encoder -> float
        abstract toUint8Array: encoder: Encoder -> Uint8Array
        abstract write: encoder: Encoder * num: float -> unit
        abstract set: encoder: Encoder * pos: float * num: float -> unit
        abstract writeUint8: encoder: Encoder * num: float -> unit
        abstract setUint8: encoder: Encoder * pos: float * num: float -> unit
        abstract writeUint16: encoder: Encoder * num: float -> unit
        abstract setUint16: encoder: Encoder * pos: float * num: float -> unit
        abstract writeUint32: encoder: Encoder * num: float -> unit
        abstract writeUint32BigEndian: encoder: Encoder * num: float -> unit
        abstract setUint32: encoder: Encoder * pos: float * num: float -> unit
        abstract writeVarUint: encoder: Encoder * num: float -> unit
        abstract writeVarInt: encoder: Encoder * num: float -> unit
        abstract writeVarString: encoder: Encoder * str: string -> unit
        abstract writeBinaryEncoder: encoder: Encoder * append: Encoder -> unit
        abstract writeUint8Array: encoder: Encoder * uint8Array: Uint8Array -> unit
        abstract writeVarUint8Array: encoder: Encoder * uint8Array: Uint8Array -> unit
        abstract writeOnDataView: encoder: Encoder * len: float -> DataView
        abstract writeFloat32: encoder: Encoder * num: float -> unit
        abstract writeFloat64: encoder: Encoder * num: float -> unit
        abstract writeBigInt64: encoder: Encoder * num: obj -> obj option
        abstract writeBigUint64: encoder: Encoder * num: obj -> obj option
        abstract writeAny: encoder: Encoder * data: U7<float, obj, bool, string, IExportsWriteAny, Array<obj option>, Uint8Array> option -> unit
        abstract RleEncoder: RleEncoderStatic
        abstract IntDiffEncoder: IntDiffEncoderStatic
        abstract RleIntDiffEncoder: RleIntDiffEncoderStatic
        abstract UintOptRleEncoder: UintOptRleEncoderStatic
        abstract IncUintOptRleEncoder: IncUintOptRleEncoderStatic
        abstract IntDiffOptRleEncoder: IntDiffOptRleEncoderStatic
        abstract StringEncoder: StringEncoderStatic

    /// A BinaryEncoder handles the encoding to an Uint8Array.
    type [<AllowNullLiteral>] Encoder =
        abstract cpos: float with get, set
        abstract cbuf: Uint8Array with get, set
        abstract bufs: Array<Uint8Array> with get, set

    /// A BinaryEncoder handles the encoding to an Uint8Array.
    type [<AllowNullLiteral>] EncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> Encoder

    /// Basic Run Length Encoder - a basic compression implementation.
    /// 
    /// Encodes [1,1,1,7] to [1,3,7,1] (3 times 1, 1 time 7). This encoder might do more harm than good if there are a lot of values that are not repeated.
    /// 
    /// It was originally used for image compression. Cool .. article http://csbruce.com/cbm/transactor/pdfs/trans_v7_i06.pdf
    type [<AllowNullLiteral>] RleEncoder<'T> =
        inherit Encoder
        /// The writer
        abstract w: (Encoder -> 'T -> unit) with get, set
        /// Current state
        abstract s: 'T option with get, set
        abstract count: float with get, set
        abstract write: v: 'T -> unit

    /// Basic Run Length Encoder - a basic compression implementation.
    /// 
    /// Encodes [1,1,1,7] to [1,3,7,1] (3 times 1, 1 time 7). This encoder might do more harm than good if there are a lot of values that are not repeated.
    /// 
    /// It was originally used for image compression. Cool .. article http://csbruce.com/cbm/transactor/pdfs/trans_v7_i06.pdf
    type [<AllowNullLiteral>] RleEncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: writer: (Encoder -> 'T -> unit) -> RleEncoder<'T>

    /// Basic diff decoder using variable length encoding.
    /// 
    /// Encodes the values [3, 1100, 1101, 1050, 0] to [3, 1097, 1, -51, -1050] using writeVarInt.
    type [<AllowNullLiteral>] IntDiffEncoder =
        inherit Encoder
        /// Current state
        abstract s: float with get, set
        abstract write: v: float -> unit

    /// Basic diff decoder using variable length encoding.
    /// 
    /// Encodes the values [3, 1100, 1101, 1050, 0] to [3, 1097, 1, -51, -1050] using writeVarInt.
    type [<AllowNullLiteral>] IntDiffEncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: start: float -> IntDiffEncoder

    /// A combination of IntDiffEncoder and RleEncoder.
    /// 
    /// Basically first writes the IntDiffEncoder and then counts duplicate diffs using RleEncoding.
    /// 
    /// Encodes the values [1,1,1,2,3,4,5,6] as [1,1,0,2,1,5] (RLE([1,0,0,1,1,1,1,1]) ⇒ RleIntDiff[1,1,0,2,1,5])
    type [<AllowNullLiteral>] RleIntDiffEncoder =
        inherit Encoder
        /// Current state
        abstract s: float with get, set
        abstract count: float with get, set
        abstract write: v: float -> unit

    /// A combination of IntDiffEncoder and RleEncoder.
    /// 
    /// Basically first writes the IntDiffEncoder and then counts duplicate diffs using RleEncoding.
    /// 
    /// Encodes the values [1,1,1,2,3,4,5,6] as [1,1,0,2,1,5] (RLE([1,0,0,1,1,1,1,1]) ⇒ RleIntDiff[1,1,0,2,1,5])
    type [<AllowNullLiteral>] RleIntDiffEncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: start: float -> RleIntDiffEncoder

    /// Optimized Rle encoder that does not suffer from the mentioned problem of the basic Rle encoder.
    /// 
    /// Internally uses VarInt encoder to write unsigned integers. If the input occurs multiple times, we write
    /// write it as a negative number. The UintOptRleDecoder then understands that it needs to read a count.
    /// 
    /// Encodes [1,2,3,3,3] as [1,2,-3,3] (once 1, once 2, three times 3)
    type [<AllowNullLiteral>] UintOptRleEncoder =
        abstract encoder: Encoder with get, set
        abstract s: float with get, set
        abstract count: float with get, set
        abstract write: v: float -> unit
        abstract toUint8Array: unit -> Uint8Array

    /// Optimized Rle encoder that does not suffer from the mentioned problem of the basic Rle encoder.
    /// 
    /// Internally uses VarInt encoder to write unsigned integers. If the input occurs multiple times, we write
    /// write it as a negative number. The UintOptRleDecoder then understands that it needs to read a count.
    /// 
    /// Encodes [1,2,3,3,3] as [1,2,-3,3] (once 1, once 2, three times 3)
    type [<AllowNullLiteral>] UintOptRleEncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> UintOptRleEncoder

    /// Increasing Uint Optimized RLE Encoder
    /// 
    /// The RLE encoder counts the number of same occurences of the same value.
    /// The IncUintOptRle encoder counts if the value increases.
    /// I.e. 7, 8, 9, 10 will be encoded as [-7, 4]. 1, 3, 5 will be encoded
    /// as [1, 3, 5].
    type [<AllowNullLiteral>] IncUintOptRleEncoder =
        abstract encoder: Encoder with get, set
        abstract s: float with get, set
        abstract count: float with get, set
        abstract write: v: float -> unit
        abstract toUint8Array: unit -> Uint8Array

    /// Increasing Uint Optimized RLE Encoder
    /// 
    /// The RLE encoder counts the number of same occurences of the same value.
    /// The IncUintOptRle encoder counts if the value increases.
    /// I.e. 7, 8, 9, 10 will be encoded as [-7, 4]. 1, 3, 5 will be encoded
    /// as [1, 3, 5].
    type [<AllowNullLiteral>] IncUintOptRleEncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> IncUintOptRleEncoder

    /// A combination of the IntDiffEncoder and the UintOptRleEncoder.
    /// 
    /// The count approach is similar to the UintDiffOptRleEncoder, but instead of using the negative bitflag, it encodes
    /// in the LSB whether a count is to be read. Therefore this Encoder only supports 31 bit integers!
    /// 
    /// Encodes [1, 2, 3, 2] as [3, 1, 6, -1] (more specifically [(1 << 1) | 1, (3 << 0) | 0, -1])
    /// 
    /// Internally uses variable length encoding. Contrary to normal UintVar encoding, the first byte contains:
    /// * 1 bit that denotes whether the next value is a count (LSB)
    /// * 1 bit that denotes whether this value is negative (MSB - 1)
    /// * 1 bit that denotes whether to continue reading the variable length integer (MSB)
    /// 
    /// Therefore, only five bits remain to encode diff ranges.
    /// 
    /// Use this Encoder only when appropriate. In most cases, this is probably a bad idea.
    type [<AllowNullLiteral>] IntDiffOptRleEncoder =
        abstract encoder: Encoder with get, set
        abstract s: float with get, set
        abstract count: float with get, set
        abstract diff: float with get, set
        abstract write: v: float -> unit
        abstract toUint8Array: unit -> Uint8Array

    /// A combination of the IntDiffEncoder and the UintOptRleEncoder.
    /// 
    /// The count approach is similar to the UintDiffOptRleEncoder, but instead of using the negative bitflag, it encodes
    /// in the LSB whether a count is to be read. Therefore this Encoder only supports 31 bit integers!
    /// 
    /// Encodes [1, 2, 3, 2] as [3, 1, 6, -1] (more specifically [(1 << 1) | 1, (3 << 0) | 0, -1])
    /// 
    /// Internally uses variable length encoding. Contrary to normal UintVar encoding, the first byte contains:
    /// * 1 bit that denotes whether the next value is a count (LSB)
    /// * 1 bit that denotes whether this value is negative (MSB - 1)
    /// * 1 bit that denotes whether to continue reading the variable length integer (MSB)
    /// 
    /// Therefore, only five bits remain to encode diff ranges.
    /// 
    /// Use this Encoder only when appropriate. In most cases, this is probably a bad idea.
    type [<AllowNullLiteral>] IntDiffOptRleEncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> IntDiffOptRleEncoder

    /// Optimized String Encoder.
    /// 
    /// Encoding many small strings in a simple Encoder is not very efficient. The function call to decode a string takes some time and creates references that must be eventually deleted.
    /// In practice, when decoding several million small strings, the GC will kick in more and more often to collect orphaned string objects (or maybe there is another reason?).
    /// 
    /// This string encoder solves the above problem. All strings are concatenated and written as a single string using a single encoding call.
    /// 
    /// The lengths are encoded using a UintOptRleEncoder.
    type [<AllowNullLiteral>] StringEncoder =
        abstract sarr: Array<string> with get, set
        abstract s: string with get, set
        abstract lensE: UintOptRleEncoder with get, set
        abstract write: string: string -> unit
        abstract toUint8Array: unit -> Uint8Array

    /// Optimized String Encoder.
    /// 
    /// Encoding many small strings in a simple Encoder is not very efficient. The function call to decode a string takes some time and creates references that must be eventually deleted.
    /// In practice, when decoding several million small strings, the GC will kick in more and more often to collect orphaned string objects (or maybe there is another reason?).
    /// 
    /// This string encoder solves the above problem. All strings are concatenated and written as a single string using a single encoding call.
    /// 
    /// The lengths are encoded using a UintOptRleEncoder.
    type [<AllowNullLiteral>] StringEncoderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> StringEncoder

    type [<AllowNullLiteral>] IExportsWriteAny =
        [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

// // module Encoding_test =
// //     module T = __testing_js
// //     module Decoding = __decoding_js
// //     module Encoding = __encoding_js
// //     module Prng = __prng_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testGolangBinaryEncodingCompatibility: unit -> unit
// //         abstract testStringDecodingPerformance: unit -> unit
// //         abstract testAnyEncodeUnknowns: tc: T.TestCase -> unit
// //         abstract testAnyEncodeDate: tc: T.TestCase -> unit
// //         abstract testEncodeMax32bitUint: tc: T.TestCase -> unit
// //         abstract testVarUintEncoding: tc: T.TestCase -> unit
// //         abstract testVarIntEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatVarUintEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatVarIntEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatAnyEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatPeekVarUintEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatPeekVarIntEncoding: tc: T.TestCase -> unit
// //         abstract testAnyVsJsonEncoding: tc: T.TestCase -> unit
// //         abstract testStringEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatStringEncoding: tc: T.TestCase -> unit
// //         abstract testSetMethods: tc: T.TestCase -> unit
// //         abstract testRepeatRandomWrites: tc: T.TestCase -> unit
// //         abstract testWriteUint8ArrayOverflow: tc: T.TestCase -> unit
// //         abstract testSetOnOverflow: tc: T.TestCase -> unit
// //         abstract testCloneDecoder: tc: T.TestCase -> unit
// //         abstract testWriteBinaryEncoder: tc: T.TestCase -> unit
// //         abstract testOverflowStringDecoding: tc: T.TestCase -> unit
// //         abstract testRleEncoder: tc: T.TestCase -> unit
// //         abstract testRleIntDiffEncoder: tc: T.TestCase -> unit
// //         abstract testUintOptRleEncoder: tc: T.TestCase -> unit
// //         abstract testIntDiffRleEncoder: tc: T.TestCase -> unit
// //         abstract testIntEncoders: tc: T.TestCase -> unit
// //         abstract testIntDiffEncoder: tc: T.TestCase -> unit
// //         abstract testStringDecoder: tc: T.TestCase -> unit
// //         abstract testLargeNumberAnyEncoding: tc: T.TestCase -> unit

// //     type [<AllowNullLiteral>] EncodingPair =
// //         abstract read: (Decoding.Decoder -> obj option) with get, set
// //         abstract write: (Encoding.Encoder -> obj option -> unit) with get, set
// //         abstract gen: (Prng.PRNG -> obj option) with get, set
// //         abstract compare: (obj option -> obj option -> bool) with get, set
// //         abstract name: string with get, set

// module Environment =

//     type [<AllowNullLiteral>] IExports =
//         abstract isNode: bool
//         abstract isBrowser: bool
//         abstract isMac: bool
//         abstract hasParam: name: string -> bool
//         abstract getParam: name: string * defaultVal: string -> string
//         abstract getVariable: name: string -> string option
//         abstract getConf: name: string -> string option
//         abstract hasConf: name: string -> bool
//         abstract production: bool

// module Error =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: s: string -> Error
//         abstract methodUnimplemented: unit -> obj
//         abstract unexpectedCase: unit -> obj

// module Eventloop =

//     type [<AllowNullLiteral>] IExports =
//         abstract enqueue: f: (unit -> unit) -> unit
//         abstract timeout: timeout: float * callback: Function -> TimeoutObject
//         abstract interval: timeout: float * callback: Function -> TimeoutObject
//         abstract Animation: IExportsAnimation
//         abstract animationFrame: cb: (float -> unit) -> TimeoutObject
//         abstract idleCallback: cb: Function -> TimeoutObject
//         abstract createDebouncer: timeout: float -> ((unit -> unit) -> unit)

//     type [<AllowNullLiteral>] TimeoutObject =
//         abstract destroy: Function with get, set

//     type [<AllowNullLiteral>] IExportsAnimation =
//         [<Emit "new $0($1...)">] abstract Create: timeoutId: float -> obj

// // module Eventloop_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testEventloopOrder: tc: T.TestCase -> Promise<ResizeArray<unit>>
// //         abstract testTimeout: tc: T.TestCase -> Promise<unit>
// //         abstract testInterval: tc: T.TestCase -> Promise<unit>
// //         abstract testAnimationFrame: tc: T.TestCase -> Promise<unit>
// //         abstract testIdleCallback: tc: T.TestCase -> Promise<unit>

// module Function =

//     type [<AllowNullLiteral>] IExports =
//         abstract callAll: fs: Array<Function> * args: Array<obj option> * ?i: float -> unit
//         abstract nop: unit -> unit
//         abstract apply: f: (unit -> 'T) -> 'T
//         abstract id: a: 'A -> 'A
//         abstract equalityStrict: a: 'T * b: 'T -> bool
//         abstract equalityFlat: a: U2<obj, ResizeArray<'T>> * b: U2<obj, ResizeArray<'T>> -> bool
//         abstract equalityDeep: a: obj option * b: obj option -> bool

// // module Function_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testDeepEquality: tc: T.TestCase -> unit

// module Index =
//     module Array = Lib0.Array
//     module Binary = Lib0.Binary
//     module Broadcastchannel = Lib0.Broadcastchannel
//     module Buffer = Lib0.Buffer
//     module Conditions = Lib0.Conditions
//     module Decoding = Lib0.Decoding
//     module Diff = Lib0.Diff
//     module Dom = Lib0.Dom
//     module Encoding = Lib0.Encoding
//     module Environment = Lib0.Environment
//     module Error = Lib0.Error
//     module Eventloop = Lib0.Eventloop
//     module Func = Lib0.Function
//     module Indexeddb = Lib0.Indexeddb
//     module Iterator = Lib0.Iterator
//     module Json = Lib0.Json
//     module Logging = Lib0.Logging
//     module Map = Lib0.Map
//     module Math = Lib0.Math
//     module Mutex = Lib0.Mutex
//     module Number = Lib0.Number
//     module Object = Lib0.Object
//     module Pair = Lib0.Pair
//     module Prng = Lib0.Prng
//     module Promise = Lib0.Promise
//     module Set = Lib0.Set
//     module Sort = Lib0.Sort
//     module Statistics = Lib0.Statistics
//     module String = Lib0.String
//     module Symbol = Lib0.Symbol
//     module Time = Lib0.Time
//     module Tree = Lib0.Tree
//     module Websocket = Lib0.Websocket

// module Indexeddb =

//     type [<AllowNullLiteral>] IExports =
//         abstract rtop: request: IDBRequest -> Promise<obj option>
//         abstract openDB: name: string * initDB: (IDBDatabase -> obj option) -> Promise<IDBDatabase>
//         abstract deleteDB: name: string -> Promise<obj option>
//         abstract createStores: db: IDBDatabase * definitions: Array<U2<Array<string>, Array<U2<string, IDBObjectStoreParameters> option>>> -> unit
//         abstract transact: db: IDBDatabase * stores: Array<string> * ?access: TransactAccess -> Array<IDBObjectStore>
//         abstract count: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<float>
//         abstract get: store: IDBObjectStore * key: U5<string, float, ArrayBuffer, DateTime, Array<obj option>> -> Promise<U5<string, float, ArrayBuffer, DateTime, Array<obj option>>>
//         abstract del: store: IDBObjectStore * key: U6<string, float, ArrayBuffer, DateTime, IDBKeyRange, Array<obj option>> -> Promise<obj option>
//         abstract put: store: IDBObjectStore * item: U5<string, float, ArrayBuffer, DateTime, bool> * ?key: U5<string, float, ResizeArray<obj option>, DateTime, ArrayBuffer> -> Promise<obj option>
//         abstract add: store: IDBObjectStore * item: U5<string, float, ArrayBuffer, DateTime, bool> * key: U5<string, float, ArrayBuffer, DateTime, Array<obj option>> -> Promise<obj option>
//         abstract addAutoKey: store: IDBObjectStore * item: U4<string, float, ArrayBuffer, DateTime> -> Promise<float>
//         abstract getAll: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<Array<obj option>>
//         abstract getAllKeys: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<Array<obj option>>
//         abstract queryFirst: store: IDBObjectStore * query: IDBKeyRange option * direction: QueryFirstDirection -> Promise<obj option>
//         abstract getLastKey: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<obj option>
//         abstract getFirstKey: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<obj option>
//         abstract getAllKeysValues: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<Array<KeyValuePair>>
//         abstract iterate: store: IDBObjectStore * keyrange: IDBKeyRange option * f: (obj option -> obj option -> U2<unit, bool>) * ?direction: IterateDirection -> Promise<unit>
//         abstract iterateKeys: store: IDBObjectStore * keyrange: IDBKeyRange option * f: (obj option -> U2<unit, bool>) * ?direction: IterateKeysDirection -> Promise<unit>
//         abstract getStore: t: IDBTransaction * store: string -> IDBObjectStore
//         abstract createIDBKeyRangeBound: lower: obj option * upper: obj option * lowerOpen: bool * upperOpen: bool -> IDBKeyRange
//         abstract createIDBKeyRangeUpperBound: upper: obj option * upperOpen: bool -> IDBKeyRange
//         abstract createIDBKeyRangeLowerBound: lower: obj option * lowerOpen: bool -> IDBKeyRange

//     type [<StringEnum>] [<RequireQualifiedAccess>] TransactAccess =
//         | Readonly
//         | Readwrite

//     type [<StringEnum>] [<RequireQualifiedAccess>] QueryFirstDirection =
//         | Next
//         | Prev
//         | Nextunique
//         | Prevunique

//     type [<StringEnum>] [<RequireQualifiedAccess>] IterateDirection =
//         | Next
//         | Prev
//         | Nextunique
//         | Prevunique

//     type [<StringEnum>] [<RequireQualifiedAccess>] IterateKeysDirection =
//         | Next
//         | Prev
//         | Nextunique
//         | Prevunique

//     type [<AllowNullLiteral>] KeyValuePair =
//         /// key
//         abstract k: obj option with get, set
//         /// Value
//         abstract v: obj option with get, set

// module Indexeddb_test =

//     type [<AllowNullLiteral>] IExports =
//         abstract testRetrieveElements: unit -> Promise<unit>

// module Iterator =

//     type [<AllowNullLiteral>] IExports =
//         abstract mapIterator: iterator: Iterator<'T, obj option, obj> * f: ('T -> 'R) -> IterableIterator<'R>
//         abstract createIterator: next: (unit -> IteratorResult<'T, obj option>) -> IterableIterator<'T>
//         abstract iteratorFilter: iterator: Iterator<'T, obj option, obj> * filter: ('T -> bool) -> IterableIterator<'T>
//         abstract iteratorMap: iterator: Iterator<'T, obj option, obj> * fmap: ('T -> 'M) -> IterableIterator<'M option>

// module Json =

//     type [<AllowNullLiteral>] IExports =
//         abstract stringify: IExportsStringify
//         abstract parse: (string -> (obj option -> string -> obj option -> obj option) -> obj option)

//     type [<AllowNullLiteral>] IExportsStringify =
//         [<Emit "$0($1...)">] abstract Invoke: value: obj option * ?replacer: (obj option -> string -> obj option -> obj option) * ?space: U2<string, float> -> string
//         [<Emit "$0($1...)">] abstract Invoke: value: obj option * ?replacer: ResizeArray<U2<string, float>> * ?space: U2<string, float> -> string

// module List =

//     type [<AllowNullLiteral>] IExports =
//         abstract ListNode: ListNodeStatic
//         abstract List: ListStatic
//         abstract create: unit -> List<'N>
//         abstract isEmpty: queue: List<'N> -> bool
//         abstract removeNode: queue: List<'N> * node: 'N -> 'N
//         abstract insertBetween: queue: List<'N> * left: 'N option * right: 'N option * node: 'N -> unit
//         abstract pushEnd: queue: List<'N> * n: 'N -> unit
//         abstract pushFront: queue: List<'N> * n: 'N -> unit
//         abstract popFront: list: List<'N> -> 'N option
//         abstract popEnd: list: List<'N> -> 'N option
//         abstract map: list: List<'N> * f: ('N -> 'M) -> ResizeArray<'M>

//     type [<AllowNullLiteral>] ListNode =
//         abstract next: ListNode option with get, set
//         abstract prev: ListNode option with get, set

//     type [<AllowNullLiteral>] ListNodeStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> ListNode

//     type [<AllowNullLiteral>] List<'N> =
//         abstract start: 'N option with get, set
//         abstract ``end``: 'N option with get, set

//     type [<AllowNullLiteral>] ListStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> List<'N>

// // module List_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testEnqueueDequeue: tc: T.TestCase -> unit
// //         abstract testSelectivePop: tc: T.TestCase -> unit

// module Logging =

//     type [<AllowNullLiteral>] IExports =
//         abstract BOLD: Symbol
//         abstract UNBOLD: Symbol
//         abstract BLUE: Symbol
//         abstract GREY: Symbol
//         abstract GREEN: Symbol
//         abstract RED: Symbol
//         abstract PURPLE: Symbol
//         abstract ORANGE: Symbol
//         abstract UNCOLOR: Symbol
//         abstract print: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract warn: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract printError: err: Error -> unit
//         abstract printImg: url: string * height: float -> unit
//         abstract printImgBase64: base64: string * height: float -> unit
//         abstract group: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract groupCollapsed: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract groupEnd: unit -> unit
//         abstract printDom: createNode: (unit -> Node) -> unit
//         abstract printCanvas: canvas: HTMLCanvasElement * height: float -> unit
//         abstract vconsoles: Set<obj option>
//         abstract VConsole: VConsoleStatic
//         abstract createVConsole: dom: Element -> VConsole
//         abstract createModuleLogger: moduleName: string -> (ResizeArray<obj option> -> unit)

//     type [<AllowNullLiteral>] VConsole =
//         abstract dom: Element with get, set
//         abstract ccontainer: Element with get, set
//         abstract depth: float with get, set
//         abstract group: args: Array<U4<string, Symbol, Object, float>> * ?collapsed: bool -> unit
//         abstract groupCollapsed: args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract groupEnd: unit -> unit
//         abstract print: args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract printError: err: Error -> unit
//         abstract printImg: url: string * height: float -> unit
//         abstract printDom: node: Node -> unit
//         abstract destroy: unit -> unit

//     type [<AllowNullLiteral>] VConsoleStatic =
//         [<Emit "new $0($1...)">] abstract Create: dom: Element -> VConsole

// module Logging_test =

//     type [<AllowNullLiteral>] IExports =
//         abstract testLogging: unit -> unit

// module Map =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: unit -> Map<obj option, obj option>
//         abstract copy: m: Map<'X, 'Y> -> Map<'X, 'Y>
//         abstract setIfUndefined: map: Map<'K, 'T> * key: 'K * createT: (unit -> 'T) -> 'T
//         abstract map: m: Map<'K, 'V> * f: ('V -> 'K -> 'R) -> ResizeArray<'R>
//         abstract any: m: Map<'K, 'V> * f: ('V -> 'K -> bool) -> bool
//         abstract all: m: Map<'K, 'V> * f: ('V -> 'K -> bool) -> bool

// // module Map_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testMap: tc: T.TestCase -> unit

// module Math =

//     type [<AllowNullLiteral>] IExports =
//         abstract floor: (float -> float)
//         abstract ceil: (float -> float)
//         abstract abs: (float -> float)
//         abstract imul: (float -> float -> float)
//         abstract round: (float -> float)
//         abstract log10: (float -> float)
//         abstract log2: (float -> float)
//         abstract log: (float -> float)
//         abstract sqrt: (float -> float)
//         abstract add: a: float * b: float -> float
//         abstract min: a: float * b: float -> float
//         abstract max: a: float * b: float -> float
//         abstract isNaN: (obj -> bool)
//         abstract pow: (float -> float -> float)
//         abstract exp10: exp: float -> float
//         abstract sign: (float -> float)
//         abstract isNegativeZero: n: float -> bool

// // module Math_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testMath: tc: T.TestCase -> unit

// module Metric =

//     type [<AllowNullLiteral>] IExports =
//         abstract yotta: obj
//         abstract zetta: obj
//         abstract exa: obj
//         abstract peta: obj
//         abstract tera: obj
//         abstract giga: obj
//         abstract mega: obj
//         abstract kilo: obj
//         abstract hecto: obj
//         abstract deca: obj
//         abstract deci: obj
//         abstract centi: obj
//         abstract milli: obj
//         abstract micro: obj
//         abstract nano: obj
//         abstract pico: obj
//         abstract femto: obj
//         abstract atto: obj
//         abstract zepto: obj
//         abstract yocto: obj
//         abstract prefix: n: float * ?baseMultiplier: float -> PrefixReturn

//     type [<AllowNullLiteral>] PrefixReturn =
//         abstract n: float with get, set
//         abstract prefix: string with get, set

// // module Metric_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testMetricPrefix: tc: T.TestCase -> unit

// module Mutex =

//     type [<AllowNullLiteral>] IExports =
//         abstract createMutex: unit -> mutex

//     type [<AllowNullLiteral>] mutex =
//         [<Emit "$0($1...)">] abstract Invoke: cb: (unit -> unit) * ?elseCb: (unit -> unit) -> obj option

// module Number =

//     type [<AllowNullLiteral>] IExports =
//         abstract MAX_SAFE_INTEGER: float
//         abstract MIN_SAFE_INTEGER: float
//         abstract LOWEST_INT32: float
//         abstract HIGHEST_INT32: float
//         abstract isInteger: (obj -> bool)
//         abstract isNaN: (obj -> bool)
//         abstract parseInt: (string -> float -> float)

// // module Number_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testNumber: tc: T.TestCase -> unit

// module Object =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: unit -> CreateReturn
//         abstract assign: IExportsAssign
//         abstract keys: IExportsKeys
//         abstract forEach: obj: ForEachObj * f: (obj option -> string -> obj option) -> unit
//         abstract map: obj: MapObj * f: (obj option -> string -> 'R) -> ResizeArray<'R>
//         abstract length: obj: LengthObj -> float
//         abstract some: obj: SomeObj * f: (obj option -> string -> bool) -> bool
//         abstract every: obj: EveryObj * f: (obj option -> string -> bool) -> bool
//         abstract hasProperty: obj: obj option * key: U2<string, Symbol> -> bool
//         abstract equalFlat: a: EqualFlatA * b: EqualFlatB -> bool

//     type [<AllowNullLiteral>] CreateReturn =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] ForEachObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] MapObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] LengthObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] SomeObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] EveryObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] EqualFlatA =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] EqualFlatB =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] IExportsAssign =
//         [<Emit "$0($1...)">] abstract Invoke: target: T * source: U -> obj
//         [<Emit "$0($1...)">] abstract Invoke: target: T_1 * source1: U_1 * source2: V -> obj
//         [<Emit "$0($1...)">] abstract Invoke: target: T_2 * source1: U_2 * source2: V_1 * source3: W -> obj
//         [<Emit "$0($1...)">] abstract Invoke: target: obj * [<ParamArray>] sources: ResizeArray<obj option> -> obj option

//     type [<AllowNullLiteral>] IExportsKeysInvoke =
//         interface end

//     type [<AllowNullLiteral>] IExportsKeys =
//         [<Emit "$0($1...)">] abstract Invoke: o: obj -> ResizeArray<string>
//         [<Emit "$0($1...)">] abstract Invoke: o: IExportsKeysInvoke -> ResizeArray<string>

// // module Object_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testObject: tc: T.TestCase -> unit

// module Observable =

//     type [<AllowNullLiteral>] IExports =
//         abstract Observable: ObservableStatic

//     /// Handles named events.
//     type [<AllowNullLiteral>] Observable<'N> =
//         /// Some desc.
//         abstract _observers: Map<'N, obj option> with get, set
//         abstract on: name: 'N * f: Function -> unit
//         abstract once: name: 'N * f: Function -> unit
//         abstract off: name: 'N * f: Function -> unit
//         /// <summary>Emit a named event. All registered event listeners that listen to the
//         /// specified name will receive the event.</summary>
//         /// <param name="name">The event name.</param>
//         /// <param name="args">The arguments that are applied to the event listener.</param>
//         abstract emit: name: 'N * args: Array<obj option> -> unit
//         abstract destroy: unit -> unit

//     /// Handles named events.
//     type [<AllowNullLiteral>] ObservableStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> Observable<'N>

// module Pair =

//     type [<AllowNullLiteral>] IExports =
//         abstract Pair: PairStatic
//         abstract create: left: 'L * right: 'R -> Pair<'L, 'R>
//         abstract createReversed: right: 'R * left: 'L -> Pair<'L, 'R>
//         abstract forEach: arr: ResizeArray<Pair<'L, 'R>> * f: ('L -> 'R -> obj option) -> unit
//         abstract map: arr: ResizeArray<Pair<'L, 'R>> * f: ('L -> 'R -> 'X) -> ResizeArray<'X>

//     type [<AllowNullLiteral>] Pair<'L, 'R> =
//         abstract left: 'L with get, set
//         abstract right: 'R with get, set

//     type [<AllowNullLiteral>] PairStatic =
//         [<Emit "new $0($1...)">] abstract Create: left: 'L * right: 'R -> Pair<'L, 'R>

// // module Pair_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testPair: tc: T.TestCase -> unit

// module Prng =
//     type Xoroshiro128plus = __prng_Xoroshiro128plus_js.Xoroshiro128plus

//     type [<AllowNullLiteral>] IExports =
//         abstract DefaultPRNG: obj
//         abstract create: seed: float -> PRNG
//         abstract bool: gen: PRNG -> bool
//         abstract int53: gen: PRNG * min: float * max: float -> float
//         abstract uint53: gen: PRNG * min: float * max: float -> float
//         abstract int32: gen: PRNG * min: float * max: float -> float
//         abstract uint32: gen: PRNG * min: float * max: float -> float
//         abstract int31: gen: PRNG * min: float * max: float -> float
//         abstract real53: gen: PRNG -> float
//         abstract char: gen: PRNG -> string
//         abstract letter: gen: PRNG -> string
//         abstract word: gen: PRNG * ?minLen: float * ?maxLen: float -> string
//         abstract utf16Rune: gen: PRNG -> string
//         abstract utf16String: gen: PRNG * ?maxlen: float -> string
//         abstract oneOf: gen: PRNG * array: ResizeArray<'T> -> 'T
//         abstract uint8Array: gen: PRNG * len: float -> Uint8Array
//         abstract uint16Array: gen: PRNG * len: float -> Uint16Array
//         abstract uint32Array: gen: PRNG * len: float -> Uint32Array

//     type [<AllowNullLiteral>] generatorNext =
//         [<Emit "$0($1...)">] abstract Invoke: unit -> float

//     type [<AllowNullLiteral>] PRNG =
//         /// Generate new number
//         abstract next: generatorNext with get, set

// // module Prng_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testGeneratorXoroshiro128plus: tc: T.TestCase -> unit
// //         abstract testGeneratorXorshift32: tc: T.TestCase -> unit
// //         abstract testGeneratorMt19937: tc: T.TestCase -> unit
// //         abstract testNumberDistributions: tc: T.TestCase -> unit

// module Promise =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: f: (PromiseResolve<'T> -> (Error -> unit) -> obj option) -> Promise<'T>
//         abstract createEmpty: f: ((unit -> unit) -> (Error -> unit) -> unit) -> Promise<unit>
//         abstract all: arrp: ResizeArray<Promise<'T>> -> Promise<ResizeArray<'T>>
//         abstract reject: ?reason: Error -> Promise<obj>
//         abstract resolve: res: U2<unit, 'T> -> Promise<U2<unit, 'T>>
//         abstract resolveWith: res: 'T -> Promise<'T>
//         abstract until: timeout: float * check: (unit -> bool) * ?intervalResolution: float -> Promise<unit>
//         abstract wait: timeout: float -> Promise<obj>
//         abstract isPromise: p: obj option -> bool

//     type [<AllowNullLiteral>] PromiseResolve<'T> =
//         [<Emit "$0($1...)">] abstract Invoke: ?result: U2<'T, PromiseLike<'T>> -> obj option

// // module Promise_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testRepeatPromise: tc: T.TestCase -> Promise<unit>
// //         abstract testispromise: tc: T.TestCase -> unit

// module Queue =

//     type [<AllowNullLiteral>] IExports =
//         abstract QueueNode: QueueNodeStatic
//         abstract Queue: QueueStatic
//         abstract create: unit -> Queue
//         abstract isEmpty: queue: Queue -> bool
//         abstract enqueue: queue: Queue * n: QueueNode -> unit
//         abstract dequeue: queue: Queue -> QueueNode option

//     type [<AllowNullLiteral>] QueueNode =
//         abstract next: QueueNode option with get, set

//     type [<AllowNullLiteral>] QueueNodeStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> QueueNode

//     type [<AllowNullLiteral>] Queue =
//         abstract start: QueueNode option with get, set
//         abstract ``end``: QueueNode option with get, set

//     type [<AllowNullLiteral>] QueueStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> Queue

// // module Queue_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testEnqueueDequeue: tc: T.TestCase -> unit

// module Random =

//     type [<AllowNullLiteral>] IExports =
//         abstract rand: (unit -> float)
//         abstract uint32: unit -> float
//         abstract uint53: unit -> float
//         abstract oneOf: arr: ResizeArray<'T> -> 'T
//         abstract uuidv4: unit -> obj option

// // module Random_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testUint32: tc: T.TestCase -> unit
// //         abstract testUint53: tc: T.TestCase -> unit
// //         abstract testUuidv4: tc: T.TestCase -> unit
// //         abstract testUuidv4Overlaps: tc: T.TestCase -> unit

// module Rollup_config =

//     type [<AllowNullLiteral>] IExports =
//         abstract _default: ResizeArray<U3<IExports_default, IExports_default2, IExports_default3>>

//     type [<AllowNullLiteral>] IExports_defaultOutput =
//         abstract file: string with get, set
//         abstract format: string with get, set
//         abstract sourcemap: bool with get, set
//         abstract dir: obj option with get, set
//         abstract entryFileNames: obj option with get, set
//         abstract chunkFileNames: obj option with get, set

//     type [<AllowNullLiteral>] IExports_default =
//         abstract input: string with get, set
//         abstract output: IExports_defaultOutput with get, set
//         abstract plugins: ResizeArray<obj> with get, set
//         abstract ``external``: obj option with get, set

//     type [<AllowNullLiteral>] IExports_defaultOutput2 =
//         abstract dir: string with get, set
//         abstract format: string with get, set
//         abstract sourcemap: bool with get, set
//         abstract entryFileNames: string with get, set
//         abstract chunkFileNames: string with get, set
//         abstract file: obj option with get, set

//     type [<AllowNullLiteral>] IExports_default2 =
//         abstract input: ResizeArray<string> with get, set
//         abstract output: IExports_defaultOutput2 with get, set
//         abstract ``external``: ResizeArray<string> with get, set
//         abstract plugins: obj option with get, set

//     type [<AllowNullLiteral>] IExports_default3 =
//         abstract input: string with get, set
//         abstract output: IExports_defaultOutput2 with get, set
//         abstract ``external``: ResizeArray<string> with get, set
//         abstract plugins: obj option with get, set

// module Set =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: unit -> Set<obj option>
//         abstract toArray: set: Set<'T> -> ResizeArray<'T>
//         abstract first: set: Set<'T> -> 'T
//         abstract from: entries: Iterable<'T> -> Set<'T>

// // module Set_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testFirst: tc: T.TestCase -> unit

// module Sort =

//     type [<AllowNullLiteral>] IExports =
//         abstract _insertionSort: arr: ResizeArray<'T> * lo: float * hi: float * compare: ('T -> 'T -> float) -> unit
//         abstract insertionSort: arr: ResizeArray<'T> * compare: ('T -> 'T -> float) -> unit
//         abstract quicksort: arr: ResizeArray<'T> * compare: ('T -> 'T -> float) -> unit

// // module Sort_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testSortUint16: tc: T.TestCase -> unit
// //         abstract testSortUint32: tc: T.TestCase -> unit
// //         abstract testSortObjectUint32: tc: T.TestCase -> unit
// //         abstract testListVsArrayPerformance: tc: T.TestCase -> unit

// module Statistics =

//     type [<AllowNullLiteral>] IExports =
//         abstract median: arr: Array<float> -> float
//         abstract average: arr: Array<float> -> float

// // module Statistics_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testMedian: tc: T.TestCase -> unit

// module Storage =

//     type [<AllowNullLiteral>] IExports =
//         abstract varStorage: obj option
//         abstract onChange: eventHandler: (IExportsOnChange -> unit) -> unit

//     type [<AllowNullLiteral>] IExportsOnChange =
//         abstract key: string with get, set
//         abstract newValue: string with get, set
//         abstract oldValue: string with get, set

// // module Storage_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testStorageModule: tc: T.TestCase -> unit

// module String =

//     type [<AllowNullLiteral>] IExports =
//         abstract fromCharCode: (ResizeArray<float> -> string)
//         abstract fromCodePoint: (ResizeArray<float> -> string)
//         abstract trimLeft: s: string -> string
//         abstract fromCamelCase: s: string * separator: string -> string
//         abstract utf8ByteLength: str: string -> float
//         abstract _encodeUtf8Polyfill: str: string -> Uint8Array
//         abstract utf8TextEncoder: TextEncoder
//         abstract _encodeUtf8Native: str: string -> Uint8Array
//         abstract encodeUtf8: str: string -> Uint8Array
//         abstract _decodeUtf8Polyfill: buf: Uint8Array -> string
//         abstract utf8TextDecoder: TextDecoder option
//         abstract _decodeUtf8Native: buf: Uint8Array -> string
//         abstract decodeUtf8: buf: Uint8Array -> string
//         abstract splice: str: string * index: float * remove: float * ?insert: string -> string

// // module String_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testLowercaseTransformation: tc: T.TestCase -> unit
// //         abstract testRepeatStringUtf8Encoding: tc: T.TestCase -> unit
// //         abstract testRepeatStringUtf8Decoding: tc: T.TestCase -> unit
// //         abstract testBomEncodingDecoding: tc: T.TestCase -> unit
// //         abstract testSplice: tc: T.TestCase -> unit

// module Symbol =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: SymbolConstructor
//         abstract isSymbol: s: obj option -> bool

// // module Testing =
// //     module Prng = __prng_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract extensive: bool
// //         abstract envSeed: float option
// //         abstract TestCase: TestCaseStatic
// //         abstract repetitionTime: float
// //         abstract run: moduleName: string * name: string * f: (TestCase -> U2<unit, Promise<obj option>>) * i: float * numberOfTests: float -> Promise<bool>
// //         abstract describe: description: string * ?info: string -> unit
// //         abstract info: info: string -> unit
// //         abstract printDom: ((unit -> Node) -> unit)
// //         abstract printCanvas: (HTMLCanvasElement -> float -> unit)
// //         abstract group: description: string * f: (unit -> unit) -> unit
// //         abstract groupAsync: description: string * f: (unit -> Promise<obj option>) -> Promise<unit>
// //         abstract measureTime: message: string * f: (unit -> unit) -> float
// //         abstract measureTimeAsync: message: string * f: (unit -> Promise<obj option>) -> Promise<float>
// //         abstract compareArrays: ``as``: ResizeArray<'T> * bs: ResizeArray<'T> * ?m: string -> bool
// //         abstract compareStrings: a: string * b: string * ?m: string -> unit
// //         abstract compareObjects: a: obj option * b: obj option * ?m: string -> unit
// //         abstract compare: a: 'T * b: 'T * ?message: string * ?customCompare: (obj option -> 'T -> 'T -> string -> obj option -> bool) -> bool
// //         abstract ``assert``: condition: bool * ?message: string -> obj
// //         abstract fails: f: (unit -> unit) -> unit
// //         abstract runTests: tests: RunTestsTests -> Promise<bool>
// //         abstract fail: reason: string -> obj
// //         abstract skip: ?cond: bool -> unit

// //     type [<AllowNullLiteral>] RunTestsTests =
// //         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> RunTestsTestsItem with get, set

// //     type [<AllowNullLiteral>] TestCase =
// //         abstract moduleName: string with get, set
// //         abstract testName: string with get, set
// //         abstract _seed: float option with get, set
// //         abstract _prng: Prng.PRNG option with get, set
// //         abstract resetSeed: unit -> unit
// // //        obj
// // //        obj

// //     type [<AllowNullLiteral>] TestCaseStatic =
// //         [<Emit "new $0($1...)">] abstract Create: moduleName: string * testName: string -> TestCase

// //     type [<AllowNullLiteral>] RunTestsTestsItem =
// //         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> (TestCase -> U2<unit, Promise<obj option>>) with get, set

// // module Testing_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testComparing: tc: T.TestCase -> unit
// //         abstract testFailing: unit -> unit
// //         abstract testSkipping: unit -> unit
// //         abstract testAsync: unit -> Promise<unit>
// //         abstract testRepeatRepetition: unit -> unit

// module Time =

//     type [<AllowNullLiteral>] IExports =
//         abstract getDate: unit -> DateTime
//         abstract getUnixTime: (unit -> float)
//         abstract humanizeDuration: d: float -> string

// // module Time_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testTime: tc: T.TestCase -> unit
// //         abstract testHumanDuration: tc: T.TestCase -> unit

// module Tree =

//     type [<AllowNullLiteral>] IExports =
//         abstract Tree: TreeStatic
//         abstract N: NStatic

//     /// This is a Red Black Tree implementation
//     type [<AllowNullLiteral>] Tree<'K, 'V> =
//         abstract root: obj option with get, set
//         abstract length: float with get, set
//         abstract findNext: id: 'K -> 'V
//         abstract findPrev: id: 'K -> 'V
//         abstract findNodeWithLowerBound: from: 'K -> obj option
//         abstract findNodeWithUpperBound: ``to``: 'K -> obj option
//         abstract findSmallestNode: unit -> 'V
//         abstract findWithLowerBound: from: 'K -> 'V
//         abstract findWithUpperBound: ``to``: 'K -> 'V
//         abstract iterate: from: 'K * ``to``: obj option * f: ('V -> unit) -> unit
//         abstract find: id: 'K -> 'V option
//         abstract findNode: id: 'K -> N<'V> option
//         abstract delete: id: 'K -> unit
//         abstract _fixDelete: n: obj option -> unit
//         abstract put: v: obj option -> obj option
//         abstract _fixInsert: n: obj option -> unit

//     /// This is a Red Black Tree implementation
//     type [<AllowNullLiteral>] TreeStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> Tree<'K, 'V>

//     type [<AllowNullLiteral>] N<'V> =
//         abstract ``val``: 'V with get, set
//         abstract color: bool with get, set
//         abstract _left: obj option with get, set
//         abstract _right: obj option with get, set
//         abstract _parent: obj option with get, set
//         abstract isRed: unit -> bool
//         abstract isBlack: unit -> bool
//         abstract redden: unit -> N<'V>
//         abstract blacken: unit -> N<'V>
// //        obj
// //        obj
// //        obj
// //        obj
// //        obj
// //        obj
// //        obj
//         abstract rotateLeft: tree: obj option -> unit
//         abstract next: unit -> obj option
//         abstract prev: unit -> obj option
//         abstract rotateRight: tree: obj option -> unit
//         abstract getUncle: unit -> obj option

//     type [<AllowNullLiteral>] NStatic =
//         /// A created node is always red!
//         [<Emit "new $0($1...)">] abstract Create: ``val``: 'V -> N<'V>

// module Url =

//     type [<AllowNullLiteral>] IExports =
//         abstract decodeQueryParams: url: string -> DecodeQueryParamsReturn
//         abstract encodeQueryParams: ``params``: EncodeQueryParamsParams -> string

//     type [<AllowNullLiteral>] DecodeQueryParamsReturn =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> string with get, set

//     type [<AllowNullLiteral>] EncodeQueryParamsParams =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> string with get, set

// // module Url_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testUrlParamQuery: tc: T.TestCase -> unit

// module Websocket =
//     type Observable = Observable_js.Observable

//     type [<AllowNullLiteral>] IExports =
//         abstract WebsocketClient: WebsocketClientStatic

//     type [<AllowNullLiteral>] WebsocketClient =
//         inherit Observable<string>
//         abstract url: string with get, set
//         abstract ws: WebSocket option with get, set
//         abstract binaryType: WebsocketClientBinaryType with get, set
//         abstract connected: bool with get, set
//         abstract connecting: bool with get, set
//         abstract unsuccessfulReconnects: float with get, set
//         abstract lastMessageReceived: float with get, set
//         /// Whether to connect to other peers or not
//         abstract shouldConnect: bool with get, set
//         abstract _checkInterval: NodeJS.Timer with get, set
//         abstract send: message: obj option -> unit
//         abstract disconnect: unit -> unit
//         abstract connect: unit -> unit

//     type [<AllowNullLiteral>] WebsocketClientStatic =
//         [<Emit "new $0($1...)">] abstract Create: url: string * ?p1: WebsocketClientStaticCreate -> WebsocketClient

//     type [<StringEnum>] [<RequireQualifiedAccess>] WebsocketClientBinaryType =
//         | Arraybuffer
//         | Blob

//     type [<AllowNullLiteral>] WebsocketClientStaticCreate =
//         abstract binaryType: WebsocketClientBinaryType option with get, set

// module __dist_array =

//     type [<AllowNullLiteral>] IExports =
//         abstract last: arr: ResizeArray<'L> -> 'L
//         abstract create: unit -> ResizeArray<'C>
//         abstract copy: a: ResizeArray<'D> -> ResizeArray<'D>
//         abstract appendTo: dest: ResizeArray<'M> * src: ResizeArray<'M> -> unit
//         abstract from: IExportsFrom
//         abstract every: arr: ResizeArray<'ITEM> * f: ('ITEM -> float -> ResizeArray<'ITEM> -> bool) -> bool
//         abstract some: arr: ResizeArray<'S> * f: ('S -> float -> ResizeArray<'S> -> bool) -> bool
//         abstract equalFlat: a: ResizeArray<'ELEM> * b: ResizeArray<'ELEM> -> bool
//         abstract flatten: arr: ResizeArray<ResizeArray<'ELEM>> -> ResizeArray<'ELEM>
//         abstract isArray: (obj option -> bool)

//     type [<AllowNullLiteral>] IExportsFrom =
//         [<Emit "$0($1...)">] abstract Invoke: arrayLike: ArrayLike<T_1> -> ResizeArray<T_1>
//         [<Emit "$0($1...)">] abstract Invoke: arrayLike: ArrayLike<T_2> * mapfn: (T_2 -> float -> U) * ?thisArg: obj -> ResizeArray<U>
//         [<Emit "$0($1...)">] abstract Invoke: iterable: U2<Iterable<T_3>, ArrayLike<T_3>> -> ResizeArray<T_3>
//         [<Emit "$0($1...)">] abstract Invoke: iterable: U2<Iterable<T_4>, ArrayLike<T_4>> * mapfn: (T_4 -> float -> U_1) * ?thisArg: obj -> ResizeArray<U_1>

// // module __dist_array_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testAppend: tc: T.TestCase -> unit
// //         abstract testflatten: tc: T.TestCase -> unit
// //         abstract testIsArray: tc: T.TestCase -> unit

// module __dist_binary =

//     type [<AllowNullLiteral>] IExports =
//         abstract BIT1: float
//         abstract BIT2: obj
//         abstract BIT3: obj
//         abstract BIT4: obj
//         abstract BIT5: obj
//         abstract BIT6: obj
//         abstract BIT7: obj
//         abstract BIT8: obj
//         abstract BIT9: obj
//         abstract BIT10: obj
//         abstract BIT11: obj
//         abstract BIT12: obj
//         abstract BIT13: obj
//         abstract BIT14: obj
//         abstract BIT15: obj
//         abstract BIT16: obj
//         abstract BIT17: obj
//         abstract BIT18: float
//         abstract BIT19: float
//         abstract BIT20: float
//         abstract BIT21: float
//         abstract BIT22: float
//         abstract BIT23: float
//         abstract BIT24: float
//         abstract BIT25: float
//         abstract BIT26: float
//         abstract BIT27: float
//         abstract BIT28: float
//         abstract BIT29: float
//         abstract BIT30: float
//         abstract BIT31: float
//         abstract BIT32: float
//         abstract BITS0: float
//         abstract BITS1: obj
//         abstract BITS2: obj
//         abstract BITS3: obj
//         abstract BITS4: obj
//         abstract BITS5: obj
//         abstract BITS6: obj
//         abstract BITS7: obj
//         abstract BITS8: obj
//         abstract BITS9: obj
//         abstract BITS10: obj
//         abstract BITS11: obj
//         abstract BITS12: obj
//         abstract BITS13: obj
//         abstract BITS14: obj
//         abstract BITS15: obj
//         abstract BITS16: obj
//         abstract BITS17: float
//         abstract BITS18: float
//         abstract BITS19: float
//         abstract BITS20: float
//         abstract BITS21: float
//         abstract BITS22: float
//         abstract BITS23: float
//         abstract BITS24: float
//         abstract BITS25: float
//         abstract BITS26: float
//         abstract BITS27: float
//         abstract BITS28: float
//         abstract BITS29: float
//         abstract BITS30: float
//         abstract BITS31: float
//         abstract BITS32: float

// // module __dist_binary_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testBitx: tc: T.TestCase -> unit
// //         abstract testBitsx: tc: T.TestCase -> unit

// module __dist_broadcastchannel =

//     type [<AllowNullLiteral>] IExports =
//         abstract subscribe: room: string * f: (obj option -> obj option -> obj option) -> Set<(obj option -> obj option -> obj option)>
//         abstract unsubscribe: room: string * f: (obj option -> obj option -> obj option) -> bool
//         abstract publish: room: string * data: obj option * ?origin: obj -> unit

//     type [<AllowNullLiteral>] Channel =
//         abstract subs: Set<(obj option -> obj option -> obj option)> with get, set
//         abstract bc: obj option with get, set

// module __dist_buffer =

//     type [<AllowNullLiteral>] IExports =
//         abstract createUint8ArrayFromLen: len: float -> Uint8Array
//         abstract createUint8ArrayViewFromArrayBuffer: buffer: ArrayBuffer * byteOffset: float * length: float -> Uint8Array
//         abstract createUint8ArrayFromArrayBuffer: buffer: ArrayBuffer -> Uint8Array
//         abstract toBase64: bytes: Uint8Array -> string
//         abstract fromBase64: s: string -> Uint8Array
//         abstract copyUint8Array: uint8Array: Uint8Array -> Uint8Array
//         abstract encodeAny: data: obj option -> Uint8Array
//         abstract decodeAny: buf: Uint8Array -> obj option

// // module __dist_buffer_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testRepeatBase64Encoding: tc: T.TestCase -> unit
// //         abstract testAnyEncoding: tc: T.TestCase -> unit

// module __dist_cache =
//     module List = List

//     type [<AllowNullLiteral>] IExports =
//         abstract Cache: CacheStatic
//         abstract removeStale: cache: Cache<'K, 'V> -> float
//         abstract set: cache: Cache<'K, 'V> * key: 'K * value: 'V -> unit
//         abstract get: cache: Cache<'K, 'V> * key: 'K -> 'V option
//         abstract refreshTimeout: cache: Cache<'K, 'V> * key: 'K -> unit
//         abstract getAsync: cache: Cache<'K, 'V> * key: 'K -> U2<'V, Promise<'V>> option
//         abstract remove: cache: Cache<'K, 'V> * key: 'K -> 'V option
//         abstract setIfUndefined: cache: Cache<'K, 'V> * key: 'K * init: (unit -> Promise<'V>) * ?removeNull: bool -> U2<'V, Promise<'V>>
//         abstract create: timeout: float -> Cache<obj option, obj option>
//         abstract Entry: EntryStatic

//     type [<AllowNullLiteral>] Cache<'K, 'V> =
//         abstract timeout: float with get, set
//         abstract _q: List.List<Entry<'K, 'V>> with get, set
//         abstract _map: Map<'K, Entry<'K, 'V>> with get, set

//     type [<AllowNullLiteral>] CacheStatic =
//         [<Emit "new $0($1...)">] abstract Create: timeout: float -> Cache<'K, 'V>

//     type [<AllowNullLiteral>] Entry<'K, 'V> =
//         inherit List.ListNode
//         abstract prev: Entry<'K, 'V> option with get, set
//         abstract next: Entry<'K, 'V> option with get, set
//         abstract created: float with get, set
//         abstract ``val``: U2<'V, Promise<'V>> with get, set
//         abstract key: 'K with get, set

//     type [<AllowNullLiteral>] EntryStatic =
//         [<Emit "new $0($1...)">] abstract Create: key: 'K * ``val``: U2<'V, Promise<'V>> -> Entry<'K, 'V>

// // module __dist_cache_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testCache: tc: T.TestCase -> Promise<unit>

// module __dist_component =

//     type [<AllowNullLiteral>] IExports =
//         abstract registry: CustomElementRegistry
//         abstract define: name: string * constr: obj option * ?opts: ElementDefinitionOptions -> unit
//         abstract whenDefined: name: string -> Promise<CustomElementConstructor>
//         abstract Lib0Component: Lib0ComponentStatic
//         abstract createComponent: name: string * p1: CONF<'T> -> obj
//         abstract createComponentDefiner: definer: Function -> (unit -> obj option)
//         abstract defineListComponent: unit -> obj option
//         abstract defineLazyLoadingComponent: unit -> obj option

//     type [<AllowNullLiteral>] Lib0Component<'S> =
//         inherit HTMLElement
//         abstract state: 'S option with get, set
//         abstract _internal: obj option with get, set
//         /// <param name="forceStateUpdate">Force that the state is rerendered even if state didn't change</param>
//         abstract setState: state: 'S * ?forceStateUpdate: bool -> unit
//         abstract updateState: stateUpdate: obj option -> unit

//     type [<AllowNullLiteral>] Lib0ComponentStatic =
//         [<Emit "new $0($1...)">] abstract Create: ?state: 'S -> Lib0Component<'S>

//     type [<AllowNullLiteral>] CONF<'S> =
//         /// Template for the shadow dom.
//         abstract template: string option with get, set
//         /// shadow dom style. Is only used when
//         /// `CONF.template` is defined
//         abstract style: string option with get, set
//         /// Initial component state.
//         abstract state: 'S option with get, set
//         /// Called when
//         /// the state changes.
//         abstract onStateChange: ('S -> 'S option -> Lib0Component<'S> -> unit) option with get, set
//         /// maps from
//         /// CSS-selector to transformer function. The first element that matches the
//         /// CSS-selector receives state updates via the transformer function.
//         abstract childStates: CONFChildStates option with get, set
//         /// attrs-keys and state-keys should be camelCase, but the DOM uses kebap-case. I.e.
//         /// `attrs = { myAttr: 4 }` is represeted as `<my-elem my-attr="4" />` in the DOM
//         abstract attrs: CONFAttrs option with get, set
//         /// Maps from dom-event-name
//         /// to event listener.
//         abstract listeners: CONFListeners option with get, set
//         /// Fill slots
//         /// automatically when state changes. Maps from slot-name to slot-html.
//         abstract slots: ('S -> 'S -> Lib0Component<'S> -> CONFSlots) option with get, set

//     type [<AllowNullLiteral>] CONFChildStates =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> (obj option -> obj option -> Object) with get, set

//     type [<StringEnum>] [<RequireQualifiedAccess>] CONFAttrsItem =
//         | String
//         | Number
//         | Json
//         | Bool

//     type [<AllowNullLiteral>] CONFAttrs =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> CONFAttrsItem with get, set

//     type [<AllowNullLiteral>] CONFListeners =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> (CustomEvent -> Lib0Component<obj option> -> U2<bool, unit>) with get, set

//     type [<AllowNullLiteral>] CONFSlots =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> string with get, set

// module __dist_conditions =

//     type [<AllowNullLiteral>] IExports =
//         abstract undefinedToNull: v: 'T option -> 'T option

// module __dist_decoding =

//     type [<AllowNullLiteral>] IExports =
//         abstract Decoder: DecoderStatic
//         abstract createDecoder: uint8Array: Uint8Array -> Decoder
//         abstract hasContent: decoder: Decoder -> bool
//         abstract clone: decoder: Decoder * ?newPos: float -> Decoder
//         abstract readUint8Array: decoder: Decoder * len: float -> Uint8Array
//         abstract readVarUint8Array: decoder: Decoder -> Uint8Array
//         abstract readTailAsUint8Array: decoder: Decoder -> Uint8Array
//         abstract skip8: decoder: Decoder -> float
//         abstract readUint8: decoder: Decoder -> float
//         abstract readUint16: decoder: Decoder -> float
//         abstract readUint32: decoder: Decoder -> float
//         abstract readUint32BigEndian: decoder: Decoder -> float
//         abstract peekUint8: decoder: Decoder -> float
//         abstract peekUint16: decoder: Decoder -> float
//         abstract peekUint32: decoder: Decoder -> float
//         abstract readVarUint: decoder: Decoder -> float
//         abstract readVarInt: decoder: Decoder -> float
//         abstract peekVarUint: decoder: Decoder -> float
//         abstract peekVarInt: decoder: Decoder -> float
//         abstract readVarString: decoder: Decoder -> string
//         abstract peekVarString: decoder: Decoder -> string
//         abstract readFromDataView: decoder: Decoder * len: float -> DataView
//         abstract readFloat32: decoder: Decoder -> float
//         abstract readFloat64: decoder: Decoder -> float
//         abstract readBigInt64: decoder: Decoder -> obj option
//         abstract readBigUint64: decoder: Decoder -> obj option
//         abstract readAny: decoder: Decoder -> obj option
//         abstract RleDecoder: RleDecoderStatic
//         abstract IntDiffDecoder: IntDiffDecoderStatic
//         abstract RleIntDiffDecoder: RleIntDiffDecoderStatic
//         abstract UintOptRleDecoder: UintOptRleDecoderStatic
//         abstract IncUintOptRleDecoder: IncUintOptRleDecoderStatic
//         abstract IntDiffOptRleDecoder: IntDiffOptRleDecoderStatic
//         abstract StringDecoder: StringDecoderStatic

//     /// A Decoder handles the decoding of an Uint8Array.
//     type [<AllowNullLiteral>] Decoder =
//         /// Decoding target.
//         abstract arr: Uint8Array with get, set
//         /// Current decoding position.
//         abstract pos: float with get, set

//     /// A Decoder handles the decoding of an Uint8Array.
//     type [<AllowNullLiteral>] DecoderStatic =
//         /// <param name="uint8Array">Binary data to decode</param>
//         [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array -> Decoder

//     /// T must not be null.
//     type [<AllowNullLiteral>] RleDecoder<'T> =
//         inherit Decoder
//         /// The reader
//         abstract reader: (Decoder -> 'T) with get, set
//         /// Current state
//         abstract s: 'T option with get, set
//         abstract count: float with get, set
//         abstract read: unit -> 'T

//     /// T must not be null.
//     type [<AllowNullLiteral>] RleDecoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array * reader: (Decoder -> 'T) -> RleDecoder<'T>

//     type [<AllowNullLiteral>] IntDiffDecoder =
//         inherit Decoder
//         /// Current state
//         abstract s: float with get, set
//         abstract read: unit -> float

//     type [<AllowNullLiteral>] IntDiffDecoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array * start: float -> IntDiffDecoder

//     type [<AllowNullLiteral>] RleIntDiffDecoder =
//         inherit Decoder
//         /// Current state
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract read: unit -> float

//     type [<AllowNullLiteral>] RleIntDiffDecoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array * start: float -> RleIntDiffDecoder

//     type [<AllowNullLiteral>] UintOptRleDecoder =
//         inherit Decoder
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract read: unit -> float

//     type [<AllowNullLiteral>] UintOptRleDecoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> UintOptRleDecoder

//     type [<AllowNullLiteral>] IncUintOptRleDecoder =
//         inherit Decoder
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract read: unit -> float

//     type [<AllowNullLiteral>] IncUintOptRleDecoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> IncUintOptRleDecoder

//     type [<AllowNullLiteral>] IntDiffOptRleDecoder =
//         inherit Decoder
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract diff: float with get, set
//         abstract read: unit -> float

//     type [<AllowNullLiteral>] IntDiffOptRleDecoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> IntDiffOptRleDecoder

//     type [<AllowNullLiteral>] StringDecoder =
//         abstract decoder: UintOptRleDecoder with get, set
//         abstract str: string with get, set
//         abstract spos: float with get, set
//         abstract read: unit -> string

//     type [<AllowNullLiteral>] StringDecoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: uint8Array: Uint8Array -> StringDecoder

// module __dist_diff =

//     type [<AllowNullLiteral>] IExports =
//         abstract simpleDiffString: a: string * b: string -> SimpleDiff<string>
//         abstract simpleDiff: a: string * b: string -> SimpleDiff<string>
//         abstract simpleDiffArray: a: ResizeArray<'T> * b: ResizeArray<'T> * ?compare: ('T -> 'T -> bool) -> SimpleDiff<ResizeArray<'T>>
//         abstract simpleDiffStringWithCursor: a: string * b: string * cursor: float -> SimpleDiffStringWithCursorReturn

//     type [<AllowNullLiteral>] SimpleDiffStringWithCursorReturn =
//         abstract index: float with get, set
//         abstract remove: float with get, set
//         abstract insert: string with get, set

//     type [<AllowNullLiteral>] SimpleDiff<'T> =
//         /// The index where changes were applied
//         abstract index: float with get, set
//         /// The number of characters to delete starting
//         ///         at `index`.
//         abstract remove: float with get, set
//         /// The new text to insert at `index` after applying
//         ///       `delete`
//         abstract insert: 'T with get, set

// // module __dist_diff_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testDiffing: tc: T.TestCase -> unit
// //         abstract testRepeatDiffing: tc: T.TestCase -> unit
// //         abstract testSimpleDiffWithCursor: tc: T.TestCase -> unit
// //         abstract testArrayDiffing: tc: T.TestCase -> unit

// module __dist_dom =
//     module Pair = Pair

//     type [<AllowNullLiteral>] IExports =
//         abstract doc: Document
//         abstract createElement: name: string -> HTMLElement
//         abstract createDocumentFragment: unit -> DocumentFragment
//         abstract createTextNode: text: string -> Text
//         abstract domParser: DOMParser
//         abstract emitCustomEvent: el: HTMLElement * name: string * opts: Object -> bool
//         abstract setAttributes: el: Element * attrs: Array<Pair.Pair<string, U2<string, bool>>> -> Element
//         abstract setAttributesMap: el: Element * attrs: Map<string, string> -> Element
//         abstract fragment: children: U2<Array<Node>, HTMLCollection> -> DocumentFragment
//         abstract append: parent: Element * nodes: Array<Node> -> Element
//         abstract remove: el: HTMLElement -> unit
//         abstract addEventListener: el: EventTarget * name: string * f: EventListener -> unit
//         abstract removeEventListener: el: EventTarget * name: string * f: EventListener -> unit
//         abstract addEventListeners: node: Node * listeners: Array<Pair.Pair<string, EventListener>> -> Node
//         abstract removeEventListeners: node: Node * listeners: Array<Pair.Pair<string, EventListener>> -> Node
//         abstract element: name: string * ?attrs: Array<U2<Pair.Pair<string, string>, Pair.Pair<string, bool>>> * ?children: Array<Node> -> Element
//         abstract canvas: width: float * height: float -> HTMLCanvasElement
//         abstract text: text: string -> Text
//         abstract pairToStyleString: pair: Pair.Pair<string, string> -> string
//         abstract pairsToStyleString: pairs: Array<Pair.Pair<string, string>> -> string
//         abstract mapToStyleString: m: Map<string, string> -> string
//         abstract querySelector: el: U2<HTMLElement, ShadowRoot> * query: string -> HTMLElement option
//         abstract querySelectorAll: el: U2<HTMLElement, ShadowRoot> * query: string -> NodeListOf<HTMLElement>
//         abstract getElementById: id: string -> HTMLElement
//         abstract parseFragment: html: string -> DocumentFragment
//         abstract parseElement: html: string -> HTMLElement
//         abstract replaceWith: oldEl: HTMLElement * newEl: U2<HTMLElement, DocumentFragment> -> unit
//         abstract insertBefore: parent: HTMLElement * el: HTMLElement * ref: Node option -> HTMLElement
//         abstract appendChild: parent: Node * child: Node -> Node
//         abstract ELEMENT_NODE: float
//         abstract TEXT_NODE: float
//         abstract CDATA_SECTION_NODE: float
//         abstract COMMENT_NODE: float
//         abstract DOCUMENT_NODE: float
//         abstract DOCUMENT_TYPE_NODE: float
//         abstract DOCUMENT_FRAGMENT_NODE: float
//         abstract checkNodeType: node: obj option * ``type``: float -> bool
//         abstract isParentOf: parent: Node * child: HTMLElement -> bool

// module __dist_encoding =

//     type [<AllowNullLiteral>] IExports =
//         abstract Encoder: EncoderStatic
//         abstract createEncoder: unit -> Encoder
//         abstract length: encoder: Encoder -> float
//         abstract toUint8Array: encoder: Encoder -> Uint8Array
//         abstract write: encoder: Encoder * num: float -> unit
//         abstract set: encoder: Encoder * pos: float * num: float -> unit
//         abstract writeUint8: encoder: Encoder * num: float -> unit
//         abstract setUint8: encoder: Encoder * pos: float * num: float -> unit
//         abstract writeUint16: encoder: Encoder * num: float -> unit
//         abstract setUint16: encoder: Encoder * pos: float * num: float -> unit
//         abstract writeUint32: encoder: Encoder * num: float -> unit
//         abstract writeUint32BigEndian: encoder: Encoder * num: float -> unit
//         abstract setUint32: encoder: Encoder * pos: float * num: float -> unit
//         abstract writeVarUint: encoder: Encoder * num: float -> unit
//         abstract writeVarInt: encoder: Encoder * num: float -> unit
//         abstract writeVarString: encoder: Encoder * str: string -> unit
//         abstract writeBinaryEncoder: encoder: Encoder * append: Encoder -> unit
//         abstract writeUint8Array: encoder: Encoder * uint8Array: Uint8Array -> unit
//         abstract writeVarUint8Array: encoder: Encoder * uint8Array: Uint8Array -> unit
//         abstract writeOnDataView: encoder: Encoder * len: float -> DataView
//         abstract writeFloat32: encoder: Encoder * num: float -> unit
//         abstract writeFloat64: encoder: Encoder * num: float -> unit
//         abstract writeBigInt64: encoder: Encoder * num: obj -> obj option
//         abstract writeBigUint64: encoder: Encoder * num: obj -> obj option
//         abstract writeAny: encoder: Encoder * data: U7<float, obj, bool, string, IExportsWriteAny, Array<obj option>, Uint8Array> option -> unit
//         abstract RleEncoder: RleEncoderStatic
//         abstract IntDiffEncoder: IntDiffEncoderStatic
//         abstract RleIntDiffEncoder: RleIntDiffEncoderStatic
//         abstract UintOptRleEncoder: UintOptRleEncoderStatic
//         abstract IncUintOptRleEncoder: IncUintOptRleEncoderStatic
//         abstract IntDiffOptRleEncoder: IntDiffOptRleEncoderStatic
//         abstract StringEncoder: StringEncoderStatic

//     /// A BinaryEncoder handles the encoding to an Uint8Array.
//     type [<AllowNullLiteral>] Encoder =
//         abstract cpos: float with get, set
//         abstract cbuf: Uint8Array with get, set
//         abstract bufs: Array<Uint8Array> with get, set

//     /// A BinaryEncoder handles the encoding to an Uint8Array.
//     type [<AllowNullLiteral>] EncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> Encoder

//     /// Basic Run Length Encoder - a basic compression implementation.
//     /// 
//     /// Encodes [1,1,1,7] to [1,3,7,1] (3 times 1, 1 time 7). This encoder might do more harm than good if there are a lot of values that are not repeated.
//     /// 
//     /// It was originally used for image compression. Cool .. article http://csbruce.com/cbm/transactor/pdfs/trans_v7_i06.pdf
//     type [<AllowNullLiteral>] RleEncoder<'T> =
//         inherit Encoder
//         /// The writer
//         abstract w: (Encoder -> 'T -> unit) with get, set
//         /// Current state
//         abstract s: 'T option with get, set
//         abstract count: float with get, set
//         abstract write: v: 'T -> unit

//     /// Basic Run Length Encoder - a basic compression implementation.
//     /// 
//     /// Encodes [1,1,1,7] to [1,3,7,1] (3 times 1, 1 time 7). This encoder might do more harm than good if there are a lot of values that are not repeated.
//     /// 
//     /// It was originally used for image compression. Cool .. article http://csbruce.com/cbm/transactor/pdfs/trans_v7_i06.pdf
//     type [<AllowNullLiteral>] RleEncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: writer: (Encoder -> 'T -> unit) -> RleEncoder<'T>

//     /// Basic diff decoder using variable length encoding.
//     /// 
//     /// Encodes the values [3, 1100, 1101, 1050, 0] to [3, 1097, 1, -51, -1050] using writeVarInt.
//     type [<AllowNullLiteral>] IntDiffEncoder =
//         inherit Encoder
//         /// Current state
//         abstract s: float with get, set
//         abstract write: v: float -> unit

//     /// Basic diff decoder using variable length encoding.
//     /// 
//     /// Encodes the values [3, 1100, 1101, 1050, 0] to [3, 1097, 1, -51, -1050] using writeVarInt.
//     type [<AllowNullLiteral>] IntDiffEncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: start: float -> IntDiffEncoder

//     /// A combination of IntDiffEncoder and RleEncoder.
//     /// 
//     /// Basically first writes the IntDiffEncoder and then counts duplicate diffs using RleEncoding.
//     /// 
//     /// Encodes the values [1,1,1,2,3,4,5,6] as [1,1,0,2,1,5] (RLE([1,0,0,1,1,1,1,1]) ⇒ RleIntDiff[1,1,0,2,1,5])
//     type [<AllowNullLiteral>] RleIntDiffEncoder =
//         inherit Encoder
//         /// Current state
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract write: v: float -> unit

//     /// A combination of IntDiffEncoder and RleEncoder.
//     /// 
//     /// Basically first writes the IntDiffEncoder and then counts duplicate diffs using RleEncoding.
//     /// 
//     /// Encodes the values [1,1,1,2,3,4,5,6] as [1,1,0,2,1,5] (RLE([1,0,0,1,1,1,1,1]) ⇒ RleIntDiff[1,1,0,2,1,5])
//     type [<AllowNullLiteral>] RleIntDiffEncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: start: float -> RleIntDiffEncoder

//     /// Optimized Rle encoder that does not suffer from the mentioned problem of the basic Rle encoder.
//     /// 
//     /// Internally uses VarInt encoder to write unsigned integers. If the input occurs multiple times, we write
//     /// write it as a negative number. The UintOptRleDecoder then understands that it needs to read a count.
//     /// 
//     /// Encodes [1,2,3,3,3] as [1,2,-3,3] (once 1, once 2, three times 3)
//     type [<AllowNullLiteral>] UintOptRleEncoder =
//         abstract encoder: Encoder with get, set
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract write: v: float -> unit
//         abstract toUint8Array: unit -> Uint8Array

//     /// Optimized Rle encoder that does not suffer from the mentioned problem of the basic Rle encoder.
//     /// 
//     /// Internally uses VarInt encoder to write unsigned integers. If the input occurs multiple times, we write
//     /// write it as a negative number. The UintOptRleDecoder then understands that it needs to read a count.
//     /// 
//     /// Encodes [1,2,3,3,3] as [1,2,-3,3] (once 1, once 2, three times 3)
//     type [<AllowNullLiteral>] UintOptRleEncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> UintOptRleEncoder

//     /// Increasing Uint Optimized RLE Encoder
//     /// 
//     /// The RLE encoder counts the number of same occurences of the same value.
//     /// The IncUintOptRle encoder counts if the value increases.
//     /// I.e. 7, 8, 9, 10 will be encoded as [-7, 4]. 1, 3, 5 will be encoded
//     /// as [1, 3, 5].
//     type [<AllowNullLiteral>] IncUintOptRleEncoder =
//         abstract encoder: Encoder with get, set
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract write: v: float -> unit
//         abstract toUint8Array: unit -> Uint8Array

//     /// Increasing Uint Optimized RLE Encoder
//     /// 
//     /// The RLE encoder counts the number of same occurences of the same value.
//     /// The IncUintOptRle encoder counts if the value increases.
//     /// I.e. 7, 8, 9, 10 will be encoded as [-7, 4]. 1, 3, 5 will be encoded
//     /// as [1, 3, 5].
//     type [<AllowNullLiteral>] IncUintOptRleEncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> IncUintOptRleEncoder

//     /// A combination of the IntDiffEncoder and the UintOptRleEncoder.
//     /// 
//     /// The count approach is similar to the UintDiffOptRleEncoder, but instead of using the negative bitflag, it encodes
//     /// in the LSB whether a count is to be read. Therefore this Encoder only supports 31 bit integers!
//     /// 
//     /// Encodes [1, 2, 3, 2] as [3, 1, 6, -1] (more specifically [(1 << 1) | 1, (3 << 0) | 0, -1])
//     /// 
//     /// Internally uses variable length encoding. Contrary to normal UintVar encoding, the first byte contains:
//     /// * 1 bit that denotes whether the next value is a count (LSB)
//     /// * 1 bit that denotes whether this value is negative (MSB - 1)
//     /// * 1 bit that denotes whether to continue reading the variable length integer (MSB)
//     /// 
//     /// Therefore, only five bits remain to encode diff ranges.
//     /// 
//     /// Use this Encoder only when appropriate. In most cases, this is probably a bad idea.
//     type [<AllowNullLiteral>] IntDiffOptRleEncoder =
//         abstract encoder: Encoder with get, set
//         abstract s: float with get, set
//         abstract count: float with get, set
//         abstract diff: float with get, set
//         abstract write: v: float -> unit
//         abstract toUint8Array: unit -> Uint8Array

//     /// A combination of the IntDiffEncoder and the UintOptRleEncoder.
//     /// 
//     /// The count approach is similar to the UintDiffOptRleEncoder, but instead of using the negative bitflag, it encodes
//     /// in the LSB whether a count is to be read. Therefore this Encoder only supports 31 bit integers!
//     /// 
//     /// Encodes [1, 2, 3, 2] as [3, 1, 6, -1] (more specifically [(1 << 1) | 1, (3 << 0) | 0, -1])
//     /// 
//     /// Internally uses variable length encoding. Contrary to normal UintVar encoding, the first byte contains:
//     /// * 1 bit that denotes whether the next value is a count (LSB)
//     /// * 1 bit that denotes whether this value is negative (MSB - 1)
//     /// * 1 bit that denotes whether to continue reading the variable length integer (MSB)
//     /// 
//     /// Therefore, only five bits remain to encode diff ranges.
//     /// 
//     /// Use this Encoder only when appropriate. In most cases, this is probably a bad idea.
//     type [<AllowNullLiteral>] IntDiffOptRleEncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> IntDiffOptRleEncoder

//     /// Optimized String Encoder.
//     /// 
//     /// Encoding many small strings in a simple Encoder is not very efficient. The function call to decode a string takes some time and creates references that must be eventually deleted.
//     /// In practice, when decoding several million small strings, the GC will kick in more and more often to collect orphaned string objects (or maybe there is another reason?).
//     /// 
//     /// This string encoder solves the above problem. All strings are concatenated and written as a single string using a single encoding call.
//     /// 
//     /// The lengths are encoded using a UintOptRleEncoder.
//     type [<AllowNullLiteral>] StringEncoder =
//         abstract sarr: Array<string> with get, set
//         abstract s: string with get, set
//         abstract lensE: UintOptRleEncoder with get, set
//         abstract write: string: string -> unit
//         abstract toUint8Array: unit -> Uint8Array

//     /// Optimized String Encoder.
//     /// 
//     /// Encoding many small strings in a simple Encoder is not very efficient. The function call to decode a string takes some time and creates references that must be eventually deleted.
//     /// In practice, when decoding several million small strings, the GC will kick in more and more often to collect orphaned string objects (or maybe there is another reason?).
//     /// 
//     /// This string encoder solves the above problem. All strings are concatenated and written as a single string using a single encoding call.
//     /// 
//     /// The lengths are encoded using a UintOptRleEncoder.
//     type [<AllowNullLiteral>] StringEncoderStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> StringEncoder

//     type [<AllowNullLiteral>] IExportsWriteAny =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

// // module __dist_encoding_test =
// //     module T = __testing_js
// //     module Decoding = __decoding_js
// //     module Encoding = __encoding_js
// //     module Prng = __prng_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testGolangBinaryEncodingCompatibility: unit -> unit
// //         abstract testStringDecodingPerformance: unit -> unit
// //         abstract testAnyEncodeUnknowns: tc: T.TestCase -> unit
// //         abstract testAnyEncodeDate: tc: T.TestCase -> unit
// //         abstract testEncodeMax32bitUint: tc: T.TestCase -> unit
// //         abstract testVarUintEncoding: tc: T.TestCase -> unit
// //         abstract testVarIntEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatVarUintEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatVarIntEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatAnyEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatPeekVarUintEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatPeekVarIntEncoding: tc: T.TestCase -> unit
// //         abstract testAnyVsJsonEncoding: tc: T.TestCase -> unit
// //         abstract testStringEncoding: tc: T.TestCase -> unit
// //         abstract testRepeatStringEncoding: tc: T.TestCase -> unit
// //         abstract testSetMethods: tc: T.TestCase -> unit
// //         abstract testRepeatRandomWrites: tc: T.TestCase -> unit
// //         abstract testWriteUint8ArrayOverflow: tc: T.TestCase -> unit
// //         abstract testSetOnOverflow: tc: T.TestCase -> unit
// //         abstract testCloneDecoder: tc: T.TestCase -> unit
// //         abstract testWriteBinaryEncoder: tc: T.TestCase -> unit
// //         abstract testOverflowStringDecoding: tc: T.TestCase -> unit
// //         abstract testRleEncoder: tc: T.TestCase -> unit
// //         abstract testRleIntDiffEncoder: tc: T.TestCase -> unit
// //         abstract testUintOptRleEncoder: tc: T.TestCase -> unit
// //         abstract testIntDiffRleEncoder: tc: T.TestCase -> unit
// //         abstract testIntEncoders: tc: T.TestCase -> unit
// //         abstract testIntDiffEncoder: tc: T.TestCase -> unit
// //         abstract testStringDecoder: tc: T.TestCase -> unit
// //         abstract testLargeNumberAnyEncoding: tc: T.TestCase -> unit

// //     type [<AllowNullLiteral>] EncodingPair =
// //         abstract read: (Decoding.Decoder -> obj option) with get, set
// //         abstract write: (Encoding.Encoder -> obj option -> unit) with get, set
// //         abstract gen: (Prng.PRNG -> obj option) with get, set
// //         abstract compare: (obj option -> obj option -> bool) with get, set
// //         abstract name: string with get, set

// module __dist_environment =

//     type [<AllowNullLiteral>] IExports =
//         abstract isNode: bool
//         abstract isBrowser: bool
//         abstract isMac: bool
//         abstract hasParam: name: string -> bool
//         abstract getParam: name: string * defaultVal: string -> string
//         abstract getVariable: name: string -> string option
//         abstract getConf: name: string -> string option
//         abstract hasConf: name: string -> bool
//         abstract production: bool

// module __dist_error =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: s: string -> Error
//         abstract methodUnimplemented: unit -> obj
//         abstract unexpectedCase: unit -> obj

// module __dist_eventloop =

//     type [<AllowNullLiteral>] IExports =
//         abstract enqueue: f: (unit -> unit) -> unit
//         abstract timeout: timeout: float * callback: Function -> TimeoutObject
//         abstract interval: timeout: float * callback: Function -> TimeoutObject
//         abstract Animation: IExportsAnimation
//         abstract animationFrame: cb: (float -> unit) -> TimeoutObject
//         abstract idleCallback: cb: Function -> TimeoutObject
//         abstract createDebouncer: timeout: float -> ((unit -> unit) -> unit)

//     type [<AllowNullLiteral>] TimeoutObject =
//         abstract destroy: Function with get, set

//     type [<AllowNullLiteral>] IExportsAnimation =
//         [<Emit "new $0($1...)">] abstract Create: timeoutId: float -> obj

// // module __dist_eventloop_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testEventloopOrder: tc: T.TestCase -> Promise<ResizeArray<unit>>
// //         abstract testTimeout: tc: T.TestCase -> Promise<unit>
// //         abstract testInterval: tc: T.TestCase -> Promise<unit>
// //         abstract testAnimationFrame: tc: T.TestCase -> Promise<unit>
// //         abstract testIdleCallback: tc: T.TestCase -> Promise<unit>

// module __dist_function =

//     type [<AllowNullLiteral>] IExports =
//         abstract callAll: fs: Array<Function> * args: Array<obj option> * ?i: float -> unit
//         abstract nop: unit -> unit
//         abstract apply: f: (unit -> 'T) -> 'T
//         abstract id: a: 'A -> 'A
//         abstract equalityStrict: a: 'T * b: 'T -> bool
//         abstract equalityFlat: a: U2<obj, ResizeArray<'T>> * b: U2<obj, ResizeArray<'T>> -> bool
//         abstract equalityDeep: a: obj option * b: obj option -> bool

// // module __dist_function_test =
// //     module T = __testing_js

// //     type [<AllowNullLiteral>] IExports =
// //         abstract testDeepEquality: tc: T.TestCase -> unit

// module __dist_index =
//     module Array = __array_js
//     module Binary = __binary_js
//     module Broadcastchannel = __broadcastchannel_js
//     module Buffer = __buffer_js
//     module Conditions = __conditions_js
//     module Decoding = __decoding_js
//     module Diff = __diff_js
//     module Dom = __dom_js
//     module Encoding = __encoding_js
//     module Environment = __environment_js
//     module Error = __error_js
//     module Eventloop = __eventloop_js
//     module Func = __function_js
//     module Indexeddb = __indexeddb_js
//     module Iterator = __iterator_js
//     module Json = __json_js
//     module Logging = __logging_js
//     module Map = __map_js
//     module Math = __math_js
//     module Mutex = __mutex_js
//     module Number = __number_js
//     module Object = __object_js
//     module Pair = __pair_js
//     module Prng = __prng_js
//     module Promise = __promise_js
//     module Set = __set_js
//     module Sort = __sort_js
//     module Statistics = __statistics_js
//     module String = __string_js
//     module Symbol = __symbol_js
//     module Time = __time_js
//     module Tree = __tree_js
//     module Websocket = __websocket_js

// module __dist_indexeddb =

//     type [<AllowNullLiteral>] IExports =
//         abstract rtop: request: IDBRequest -> Promise<obj option>
//         abstract openDB: name: string * initDB: (IDBDatabase -> obj option) -> Promise<IDBDatabase>
//         abstract deleteDB: name: string -> Promise<obj option>
//         abstract createStores: db: IDBDatabase * definitions: Array<U2<Array<string>, Array<U2<string, IDBObjectStoreParameters> option>>> -> unit
//         abstract transact: db: IDBDatabase * stores: Array<string> * ?access: TransactAccess -> Array<IDBObjectStore>
//         abstract count: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<float>
//         abstract get: store: IDBObjectStore * key: U5<string, float, ArrayBuffer, DateTime, Array<obj option>> -> Promise<U5<string, float, ArrayBuffer, DateTime, Array<obj option>>>
//         abstract del: store: IDBObjectStore * key: U6<string, float, ArrayBuffer, DateTime, IDBKeyRange, Array<obj option>> -> Promise<obj option>
//         abstract put: store: IDBObjectStore * item: U5<string, float, ArrayBuffer, DateTime, bool> * ?key: U5<string, float, ResizeArray<obj option>, DateTime, ArrayBuffer> -> Promise<obj option>
//         abstract add: store: IDBObjectStore * item: U5<string, float, ArrayBuffer, DateTime, bool> * key: U5<string, float, ArrayBuffer, DateTime, Array<obj option>> -> Promise<obj option>
//         abstract addAutoKey: store: IDBObjectStore * item: U4<string, float, ArrayBuffer, DateTime> -> Promise<float>
//         abstract getAll: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<Array<obj option>>
//         abstract getAllKeys: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<Array<obj option>>
//         abstract queryFirst: store: IDBObjectStore * query: IDBKeyRange option * direction: QueryFirstDirection -> Promise<obj option>
//         abstract getLastKey: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<obj option>
//         abstract getFirstKey: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<obj option>
//         abstract getAllKeysValues: store: IDBObjectStore * ?range: IDBKeyRange -> Promise<Array<KeyValuePair>>
//         abstract iterate: store: IDBObjectStore * keyrange: IDBKeyRange option * f: (obj option -> obj option -> U2<unit, bool>) * ?direction: IterateDirection -> Promise<unit>
//         abstract iterateKeys: store: IDBObjectStore * keyrange: IDBKeyRange option * f: (obj option -> U2<unit, bool>) * ?direction: IterateKeysDirection -> Promise<unit>
//         abstract getStore: t: IDBTransaction * store: string -> IDBObjectStore
//         abstract createIDBKeyRangeBound: lower: obj option * upper: obj option * lowerOpen: bool * upperOpen: bool -> IDBKeyRange
//         abstract createIDBKeyRangeUpperBound: upper: obj option * upperOpen: bool -> IDBKeyRange
//         abstract createIDBKeyRangeLowerBound: lower: obj option * lowerOpen: bool -> IDBKeyRange

//     type [<StringEnum>] [<RequireQualifiedAccess>] TransactAccess =
//         | Readonly
//         | Readwrite

//     type [<StringEnum>] [<RequireQualifiedAccess>] QueryFirstDirection =
//         | Next
//         | Prev
//         | Nextunique
//         | Prevunique

//     type [<StringEnum>] [<RequireQualifiedAccess>] IterateDirection =
//         | Next
//         | Prev
//         | Nextunique
//         | Prevunique

//     type [<StringEnum>] [<RequireQualifiedAccess>] IterateKeysDirection =
//         | Next
//         | Prev
//         | Nextunique
//         | Prevunique

//     type [<AllowNullLiteral>] KeyValuePair =
//         /// key
//         abstract k: obj option with get, set
//         /// Value
//         abstract v: obj option with get, set

// module __dist_indexeddb_test =

//     type [<AllowNullLiteral>] IExports =
//         abstract testRetrieveElements: unit -> Promise<unit>

// module __dist_iterator =

//     type [<AllowNullLiteral>] IExports =
//         abstract mapIterator: iterator: Iterator<'T, obj option, obj> * f: ('T -> 'R) -> IterableIterator<'R>
//         abstract createIterator: next: (unit -> IteratorResult<'T, obj option>) -> IterableIterator<'T>
//         abstract iteratorFilter: iterator: Iterator<'T, obj option, obj> * filter: ('T -> bool) -> IterableIterator<'T>
//         abstract iteratorMap: iterator: Iterator<'T, obj option, obj> * fmap: ('T -> 'M) -> IterableIterator<'M option>

// module __dist_json =

//     type [<AllowNullLiteral>] IExports =
//         abstract stringify: IExportsStringify
//         abstract parse: (string -> (obj option -> string -> obj option -> obj option) -> obj option)

//     type [<AllowNullLiteral>] IExportsStringify =
//         [<Emit "$0($1...)">] abstract Invoke: value: obj option * ?replacer: (obj option -> string -> obj option -> obj option) * ?space: U2<string, float> -> string
//         [<Emit "$0($1...)">] abstract Invoke: value: obj option * ?replacer: ResizeArray<U2<string, float>> * ?space: U2<string, float> -> string

// module __dist_list =

//     type [<AllowNullLiteral>] IExports =
//         abstract ListNode: ListNodeStatic
//         abstract List: ListStatic
//         abstract create: unit -> List<'N>
//         abstract isEmpty: queue: List<'N> -> bool
//         abstract removeNode: queue: List<'N> * node: 'N -> 'N
//         abstract insertBetween: queue: List<'N> * left: 'N option * right: 'N option * node: 'N -> unit
//         abstract pushEnd: queue: List<'N> * n: 'N -> unit
//         abstract pushFront: queue: List<'N> * n: 'N -> unit
//         abstract popFront: list: List<'N> -> 'N option
//         abstract popEnd: list: List<'N> -> 'N option
//         abstract map: list: List<'N> * f: ('N -> 'M) -> ResizeArray<'M>

//     type [<AllowNullLiteral>] ListNode =
//         abstract next: ListNode option with get, set
//         abstract prev: ListNode option with get, set

//     type [<AllowNullLiteral>] ListNodeStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> ListNode

//     type [<AllowNullLiteral>] List<'N> =
//         abstract start: 'N option with get, set
//         abstract ``end``: 'N option with get, set

//     type [<AllowNullLiteral>] ListStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> List<'N>

// module __dist_list_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testEnqueueDequeue: tc: T.TestCase -> unit
//         abstract testSelectivePop: tc: T.TestCase -> unit

// module __dist_logging =

//     type [<AllowNullLiteral>] IExports =
//         abstract BOLD: Symbol
//         abstract UNBOLD: Symbol
//         abstract BLUE: Symbol
//         abstract GREY: Symbol
//         abstract GREEN: Symbol
//         abstract RED: Symbol
//         abstract PURPLE: Symbol
//         abstract ORANGE: Symbol
//         abstract UNCOLOR: Symbol
//         abstract print: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract warn: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract printError: err: Error -> unit
//         abstract printImg: url: string * height: float -> unit
//         abstract printImgBase64: base64: string * height: float -> unit
//         abstract group: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract groupCollapsed: [<ParamArray>] args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract groupEnd: unit -> unit
//         abstract printDom: createNode: (unit -> Node) -> unit
//         abstract printCanvas: canvas: HTMLCanvasElement * height: float -> unit
//         abstract vconsoles: Set<obj option>
//         abstract VConsole: VConsoleStatic
//         abstract createVConsole: dom: Element -> VConsole
//         abstract createModuleLogger: moduleName: string -> (ResizeArray<obj option> -> unit)

//     type [<AllowNullLiteral>] VConsole =
//         abstract dom: Element with get, set
//         abstract ccontainer: Element with get, set
//         abstract depth: float with get, set
//         abstract group: args: Array<U4<string, Symbol, Object, float>> * ?collapsed: bool -> unit
//         abstract groupCollapsed: args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract groupEnd: unit -> unit
//         abstract print: args: Array<U4<string, Symbol, Object, float>> -> unit
//         abstract printError: err: Error -> unit
//         abstract printImg: url: string * height: float -> unit
//         abstract printDom: node: Node -> unit
//         abstract destroy: unit -> unit

//     type [<AllowNullLiteral>] VConsoleStatic =
//         [<Emit "new $0($1...)">] abstract Create: dom: Element -> VConsole

// module __dist_logging_test =

//     type [<AllowNullLiteral>] IExports =
//         abstract testLogging: unit -> unit

// module __dist_map =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: unit -> Map<obj option, obj option>
//         abstract copy: m: Map<'X, 'Y> -> Map<'X, 'Y>
//         abstract setIfUndefined: map: Map<'K, 'T> * key: 'K * createT: (unit -> 'T) -> 'T
//         abstract map: m: Map<'K, 'V> * f: ('V -> 'K -> 'R) -> ResizeArray<'R>
//         abstract any: m: Map<'K, 'V> * f: ('V -> 'K -> bool) -> bool
//         abstract all: m: Map<'K, 'V> * f: ('V -> 'K -> bool) -> bool

// module __dist_map_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testMap: tc: T.TestCase -> unit

// module __dist_math =

//     type [<AllowNullLiteral>] IExports =
//         abstract floor: (float -> float)
//         abstract ceil: (float -> float)
//         abstract abs: (float -> float)
//         abstract imul: (float -> float -> float)
//         abstract round: (float -> float)
//         abstract log10: (float -> float)
//         abstract log2: (float -> float)
//         abstract log: (float -> float)
//         abstract sqrt: (float -> float)
//         abstract add: a: float * b: float -> float
//         abstract min: a: float * b: float -> float
//         abstract max: a: float * b: float -> float
//         abstract isNaN: (obj -> bool)
//         abstract pow: (float -> float -> float)
//         abstract exp10: exp: float -> float
//         abstract sign: (float -> float)
//         abstract isNegativeZero: n: float -> bool

// module __dist_math_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testMath: tc: T.TestCase -> unit

// module __dist_metric =

//     type [<AllowNullLiteral>] IExports =
//         abstract yotta: obj
//         abstract zetta: obj
//         abstract exa: obj
//         abstract peta: obj
//         abstract tera: obj
//         abstract giga: obj
//         abstract mega: obj
//         abstract kilo: obj
//         abstract hecto: obj
//         abstract deca: obj
//         abstract deci: obj
//         abstract centi: obj
//         abstract milli: obj
//         abstract micro: obj
//         abstract nano: obj
//         abstract pico: obj
//         abstract femto: obj
//         abstract atto: obj
//         abstract zepto: obj
//         abstract yocto: obj
//         abstract prefix: n: float * ?baseMultiplier: float -> PrefixReturn

//     type [<AllowNullLiteral>] PrefixReturn =
//         abstract n: float with get, set
//         abstract prefix: string with get, set

// module __dist_metric_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testMetricPrefix: tc: T.TestCase -> unit

// module __dist_mutex =

//     type [<AllowNullLiteral>] IExports =
//         abstract createMutex: unit -> mutex

//     type [<AllowNullLiteral>] mutex =
//         [<Emit "$0($1...)">] abstract Invoke: cb: (unit -> unit) * ?elseCb: (unit -> unit) -> obj option

// module __dist_number =

//     type [<AllowNullLiteral>] IExports =
//         abstract MAX_SAFE_INTEGER: float
//         abstract MIN_SAFE_INTEGER: float
//         abstract LOWEST_INT32: float
//         abstract HIGHEST_INT32: float
//         abstract isInteger: (obj -> bool)
//         abstract isNaN: (obj -> bool)
//         abstract parseInt: (string -> float -> float)

// module __dist_number_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testNumber: tc: T.TestCase -> unit

// module __dist_object =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: unit -> CreateReturn
//         abstract assign: IExportsAssign
//         abstract keys: IExportsKeys
//         abstract forEach: obj: ForEachObj * f: (obj option -> string -> obj option) -> unit
//         abstract map: obj: MapObj * f: (obj option -> string -> 'R) -> ResizeArray<'R>
//         abstract length: obj: LengthObj -> float
//         abstract some: obj: SomeObj * f: (obj option -> string -> bool) -> bool
//         abstract every: obj: EveryObj * f: (obj option -> string -> bool) -> bool
//         abstract hasProperty: obj: obj option * key: U2<string, Symbol> -> bool
//         abstract equalFlat: a: EqualFlatA * b: EqualFlatB -> bool

//     type [<AllowNullLiteral>] CreateReturn =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] ForEachObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] MapObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] LengthObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] SomeObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] EveryObj =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] EqualFlatA =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] EqualFlatB =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

//     type [<AllowNullLiteral>] IExportsAssign =
//         [<Emit "$0($1...)">] abstract Invoke: target: T * source: U -> obj
//         [<Emit "$0($1...)">] abstract Invoke: target: T_1 * source1: U_1 * source2: V -> obj
//         [<Emit "$0($1...)">] abstract Invoke: target: T_2 * source1: U_2 * source2: V_1 * source3: W -> obj
//         [<Emit "$0($1...)">] abstract Invoke: target: obj * [<ParamArray>] sources: ResizeArray<obj option> -> obj option

//     type [<AllowNullLiteral>] IExportsKeysInvoke =
//         interface end

//     type [<AllowNullLiteral>] IExportsKeys =
//         [<Emit "$0($1...)">] abstract Invoke: o: obj -> ResizeArray<string>
//         [<Emit "$0($1...)">] abstract Invoke: o: IExportsKeysInvoke -> ResizeArray<string>

// module __dist_object_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testObject: tc: T.TestCase -> unit

// module __dist_observable =

//     type [<AllowNullLiteral>] IExports =
//         abstract Observable: ObservableStatic

//     /// Handles named events.
//     type [<AllowNullLiteral>] Observable<'N> =
//         /// Some desc.
//         abstract _observers: Map<'N, obj option> with get, set
//         abstract on: name: 'N * f: Function -> unit
//         abstract once: name: 'N * f: Function -> unit
//         abstract off: name: 'N * f: Function -> unit
//         /// <summary>Emit a named event. All registered event listeners that listen to the
//         /// specified name will receive the event.</summary>
//         /// <param name="name">The event name.</param>
//         /// <param name="args">The arguments that are applied to the event listener.</param>
//         abstract emit: name: 'N * args: Array<obj option> -> unit
//         abstract destroy: unit -> unit

//     /// Handles named events.
//     type [<AllowNullLiteral>] ObservableStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> Observable<'N>

// module __dist_pair =

//     type [<AllowNullLiteral>] IExports =
//         abstract Pair: PairStatic
//         abstract create: left: 'L * right: 'R -> Pair<'L, 'R>
//         abstract createReversed: right: 'R * left: 'L -> Pair<'L, 'R>
//         abstract forEach: arr: ResizeArray<Pair<'L, 'R>> * f: ('L -> 'R -> obj option) -> unit
//         abstract map: arr: ResizeArray<Pair<'L, 'R>> * f: ('L -> 'R -> 'X) -> ResizeArray<'X>

//     type [<AllowNullLiteral>] Pair<'L, 'R> =
//         abstract left: 'L with get, set
//         abstract right: 'R with get, set

//     type [<AllowNullLiteral>] PairStatic =
//         [<Emit "new $0($1...)">] abstract Create: left: 'L * right: 'R -> Pair<'L, 'R>

// module __dist_pair_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testPair: tc: T.TestCase -> unit

// module __dist_prng =
//     type Xoroshiro128plus = __dist_prng_Xoroshiro128plus_js.Xoroshiro128plus

//     type [<AllowNullLiteral>] IExports =
//         abstract DefaultPRNG: obj
//         abstract create: seed: float -> PRNG
//         abstract bool: gen: PRNG -> bool
//         abstract int53: gen: PRNG * min: float * max: float -> float
//         abstract uint53: gen: PRNG * min: float * max: float -> float
//         abstract int32: gen: PRNG * min: float * max: float -> float
//         abstract uint32: gen: PRNG * min: float * max: float -> float
//         abstract int31: gen: PRNG * min: float * max: float -> float
//         abstract real53: gen: PRNG -> float
//         abstract char: gen: PRNG -> string
//         abstract letter: gen: PRNG -> string
//         abstract word: gen: PRNG * ?minLen: float * ?maxLen: float -> string
//         abstract utf16Rune: gen: PRNG -> string
//         abstract utf16String: gen: PRNG * ?maxlen: float -> string
//         abstract oneOf: gen: PRNG * array: ResizeArray<'T> -> 'T
//         abstract uint8Array: gen: PRNG * len: float -> Uint8Array
//         abstract uint16Array: gen: PRNG * len: float -> Uint16Array
//         abstract uint32Array: gen: PRNG * len: float -> Uint32Array

//     type [<AllowNullLiteral>] generatorNext =
//         [<Emit "$0($1...)">] abstract Invoke: unit -> float

//     type [<AllowNullLiteral>] PRNG =
//         /// Generate new number
//         abstract next: generatorNext with get, set

// module __dist_prng_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testGeneratorXoroshiro128plus: tc: T.TestCase -> unit
//         abstract testGeneratorXorshift32: tc: T.TestCase -> unit
//         abstract testGeneratorMt19937: tc: T.TestCase -> unit
//         abstract testNumberDistributions: tc: T.TestCase -> unit

// module __dist_promise =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: f: (PromiseResolve<'T> -> (Error -> unit) -> obj option) -> Promise<'T>
//         abstract createEmpty: f: ((unit -> unit) -> (Error -> unit) -> unit) -> Promise<unit>
//         abstract all: arrp: ResizeArray<Promise<'T>> -> Promise<ResizeArray<'T>>
//         abstract reject: ?reason: Error -> Promise<obj>
//         abstract resolve: res: U2<unit, 'T> -> Promise<U2<unit, 'T>>
//         abstract resolveWith: res: 'T -> Promise<'T>
//         abstract until: timeout: float * check: (unit -> bool) * ?intervalResolution: float -> Promise<unit>
//         abstract wait: timeout: float -> Promise<obj>
//         abstract isPromise: p: obj option -> bool

//     type [<AllowNullLiteral>] PromiseResolve<'T> =
//         [<Emit "$0($1...)">] abstract Invoke: ?result: U2<'T, PromiseLike<'T>> -> obj option

// module __dist_promise_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testRepeatPromise: tc: T.TestCase -> Promise<unit>
//         abstract testispromise: tc: T.TestCase -> unit

// module __dist_queue =

//     type [<AllowNullLiteral>] IExports =
//         abstract QueueNode: QueueNodeStatic
//         abstract Queue: QueueStatic
//         abstract create: unit -> Queue
//         abstract isEmpty: queue: Queue -> bool
//         abstract enqueue: queue: Queue * n: QueueNode -> unit
//         abstract dequeue: queue: Queue -> QueueNode option

//     type [<AllowNullLiteral>] QueueNode =
//         abstract next: QueueNode option with get, set

//     type [<AllowNullLiteral>] QueueNodeStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> QueueNode

//     type [<AllowNullLiteral>] Queue =
//         abstract start: QueueNode option with get, set
//         abstract ``end``: QueueNode option with get, set

//     type [<AllowNullLiteral>] QueueStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> Queue

// module __dist_queue_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testEnqueueDequeue: tc: T.TestCase -> unit

// module __dist_random =

//     type [<AllowNullLiteral>] IExports =
//         abstract rand: (unit -> float)
//         abstract uint32: unit -> float
//         abstract uint53: unit -> float
//         abstract oneOf: arr: ResizeArray<'T> -> 'T
//         abstract uuidv4: unit -> obj option

// module __dist_random_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testUint32: tc: T.TestCase -> unit
//         abstract testUint53: tc: T.TestCase -> unit
//         abstract testUuidv4: tc: T.TestCase -> unit
//         abstract testUuidv4Overlaps: tc: T.TestCase -> unit

// module __dist_rollup_config =

//     type [<AllowNullLiteral>] IExports =
//         abstract _default: ResizeArray<U3<IExports_default, IExports_default2, IExports_default3>>

//     type [<AllowNullLiteral>] IExports_defaultOutput =
//         abstract file: string with get, set
//         abstract format: string with get, set
//         abstract sourcemap: bool with get, set
//         abstract dir: obj option with get, set
//         abstract entryFileNames: obj option with get, set
//         abstract chunkFileNames: obj option with get, set

//     type [<AllowNullLiteral>] IExports_default =
//         abstract input: string with get, set
//         abstract output: IExports_defaultOutput with get, set
//         abstract plugins: ResizeArray<obj> with get, set
//         abstract ``external``: obj option with get, set

//     type [<AllowNullLiteral>] IExports_defaultOutput2 =
//         abstract dir: string with get, set
//         abstract format: string with get, set
//         abstract sourcemap: bool with get, set
//         abstract entryFileNames: string with get, set
//         abstract chunkFileNames: string with get, set
//         abstract file: obj option with get, set

//     type [<AllowNullLiteral>] IExports_default2 =
//         abstract input: ResizeArray<string> with get, set
//         abstract output: IExports_defaultOutput2 with get, set
//         abstract ``external``: ResizeArray<string> with get, set
//         abstract plugins: obj option with get, set

//     type [<AllowNullLiteral>] IExports_default3 =
//         abstract input: string with get, set
//         abstract output: IExports_defaultOutput2 with get, set
//         abstract ``external``: ResizeArray<string> with get, set
//         abstract plugins: obj option with get, set

// module __dist_set =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: unit -> Set<obj option>
//         abstract toArray: set: Set<'T> -> ResizeArray<'T>
//         abstract first: set: Set<'T> -> 'T
//         abstract from: entries: Iterable<'T> -> Set<'T>

// module __dist_set_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testFirst: tc: T.TestCase -> unit

// module __dist_sort =

//     type [<AllowNullLiteral>] IExports =
//         abstract _insertionSort: arr: ResizeArray<'T> * lo: float * hi: float * compare: ('T -> 'T -> float) -> unit
//         abstract insertionSort: arr: ResizeArray<'T> * compare: ('T -> 'T -> float) -> unit
//         abstract quicksort: arr: ResizeArray<'T> * compare: ('T -> 'T -> float) -> unit

// module __dist_sort_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testSortUint16: tc: T.TestCase -> unit
//         abstract testSortUint32: tc: T.TestCase -> unit
//         abstract testSortObjectUint32: tc: T.TestCase -> unit
//         abstract testListVsArrayPerformance: tc: T.TestCase -> unit

// module __dist_statistics =

//     type [<AllowNullLiteral>] IExports =
//         abstract median: arr: Array<float> -> float
//         abstract average: arr: Array<float> -> float

// module __dist_statistics_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testMedian: tc: T.TestCase -> unit

// module __dist_storage =

//     type [<AllowNullLiteral>] IExports =
//         abstract varStorage: obj option
//         abstract onChange: eventHandler: (IExportsOnChange -> unit) -> unit

//     type [<AllowNullLiteral>] IExportsOnChange =
//         abstract key: string with get, set
//         abstract newValue: string with get, set
//         abstract oldValue: string with get, set

// module __dist_storage_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testStorageModule: tc: T.TestCase -> unit

// module __dist_string =

//     type [<AllowNullLiteral>] IExports =
//         abstract fromCharCode: (ResizeArray<float> -> string)
//         abstract fromCodePoint: (ResizeArray<float> -> string)
//         abstract trimLeft: s: string -> string
//         abstract fromCamelCase: s: string * separator: string -> string
//         abstract utf8ByteLength: str: string -> float
//         abstract _encodeUtf8Polyfill: str: string -> Uint8Array
//         abstract utf8TextEncoder: TextEncoder
//         abstract _encodeUtf8Native: str: string -> Uint8Array
//         abstract encodeUtf8: str: string -> Uint8Array
//         abstract _decodeUtf8Polyfill: buf: Uint8Array -> string
//         abstract utf8TextDecoder: TextDecoder option
//         abstract _decodeUtf8Native: buf: Uint8Array -> string
//         abstract decodeUtf8: buf: Uint8Array -> string
//         abstract splice: str: string * index: float * remove: float * ?insert: string -> string

// module __dist_string_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testLowercaseTransformation: tc: T.TestCase -> unit
//         abstract testRepeatStringUtf8Encoding: tc: T.TestCase -> unit
//         abstract testRepeatStringUtf8Decoding: tc: T.TestCase -> unit
//         abstract testBomEncodingDecoding: tc: T.TestCase -> unit
//         abstract testSplice: tc: T.TestCase -> unit

// module __dist_symbol =

//     type [<AllowNullLiteral>] IExports =
//         abstract create: SymbolConstructor
//         abstract isSymbol: s: obj option -> bool

// module __dist_testing =
//     module Prng = __prng_js

//     type [<AllowNullLiteral>] IExports =
//         abstract extensive: bool
//         abstract envSeed: float option
//         abstract TestCase: TestCaseStatic
//         abstract repetitionTime: float
//         abstract run: moduleName: string * name: string * f: (TestCase -> U2<unit, Promise<obj option>>) * i: float * numberOfTests: float -> Promise<bool>
//         abstract describe: description: string * ?info: string -> unit
//         abstract info: info: string -> unit
//         abstract printDom: ((unit -> Node) -> unit)
//         abstract printCanvas: (HTMLCanvasElement -> float -> unit)
//         abstract group: description: string * f: (unit -> unit) -> unit
//         abstract groupAsync: description: string * f: (unit -> Promise<obj option>) -> Promise<unit>
//         abstract measureTime: message: string * f: (unit -> unit) -> float
//         abstract measureTimeAsync: message: string * f: (unit -> Promise<obj option>) -> Promise<float>
//         abstract compareArrays: ``as``: ResizeArray<'T> * bs: ResizeArray<'T> * ?m: string -> bool
//         abstract compareStrings: a: string * b: string * ?m: string -> unit
//         abstract compareObjects: a: obj option * b: obj option * ?m: string -> unit
//         abstract compare: a: 'T * b: 'T * ?message: string * ?customCompare: (obj option -> 'T -> 'T -> string -> obj option -> bool) -> bool
//         abstract ``assert``: condition: bool * ?message: string -> obj
//         abstract fails: f: (unit -> unit) -> unit
//         abstract runTests: tests: RunTestsTests -> Promise<bool>
//         abstract fail: reason: string -> obj
//         abstract skip: ?cond: bool -> unit

//     type [<AllowNullLiteral>] RunTestsTests =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> RunTestsTestsItem with get, set

//     type [<AllowNullLiteral>] TestCase =
//         abstract moduleName: string with get, set
//         abstract testName: string with get, set
//         abstract _seed: float option with get, set
//         abstract _prng: Prng.PRNG option with get, set
//         abstract resetSeed: unit -> unit
// //        obj
// //        obj

//     type [<AllowNullLiteral>] TestCaseStatic =
//         [<Emit "new $0($1...)">] abstract Create: moduleName: string * testName: string -> TestCase

//     type [<AllowNullLiteral>] RunTestsTestsItem =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> (TestCase -> U2<unit, Promise<obj option>>) with get, set

// module __dist_testing_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testComparing: tc: T.TestCase -> unit
//         abstract testFailing: unit -> unit
//         abstract testSkipping: unit -> unit
//         abstract testAsync: unit -> Promise<unit>
//         abstract testRepeatRepetition: unit -> unit

// module __dist_time =

//     type [<AllowNullLiteral>] IExports =
//         abstract getDate: unit -> DateTime
//         abstract getUnixTime: (unit -> float)
//         abstract humanizeDuration: d: float -> string

// module __dist_time_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testTime: tc: T.TestCase -> unit
//         abstract testHumanDuration: tc: T.TestCase -> unit

// module __dist_tree =

//     type [<AllowNullLiteral>] IExports =
//         abstract Tree: TreeStatic
//         abstract N: NStatic

//     /// This is a Red Black Tree implementation
//     type [<AllowNullLiteral>] Tree<'K, 'V> =
//         abstract root: obj option with get, set
//         abstract length: float with get, set
//         abstract findNext: id: 'K -> 'V
//         abstract findPrev: id: 'K -> 'V
//         abstract findNodeWithLowerBound: from: 'K -> obj option
//         abstract findNodeWithUpperBound: ``to``: 'K -> obj option
//         abstract findSmallestNode: unit -> 'V
//         abstract findWithLowerBound: from: 'K -> 'V
//         abstract findWithUpperBound: ``to``: 'K -> 'V
//         abstract iterate: from: 'K * ``to``: obj option * f: ('V -> unit) -> unit
//         abstract find: id: 'K -> 'V option
//         abstract findNode: id: 'K -> N<'V> option
//         abstract delete: id: 'K -> unit
//         abstract _fixDelete: n: obj option -> unit
//         abstract put: v: obj option -> obj option
//         abstract _fixInsert: n: obj option -> unit

//     /// This is a Red Black Tree implementation
//     type [<AllowNullLiteral>] TreeStatic =
//         [<Emit "new $0($1...)">] abstract Create: unit -> Tree<'K, 'V>

//     type [<AllowNullLiteral>] N<'V> =
//         abstract ``val``: 'V with get, set
//         abstract color: bool with get, set
//         abstract _left: obj option with get, set
//         abstract _right: obj option with get, set
//         abstract _parent: obj option with get, set
//         abstract isRed: unit -> bool
//         abstract isBlack: unit -> bool
//         abstract redden: unit -> N<'V>
//         abstract blacken: unit -> N<'V>
// //        obj
// //        obj
// //        obj
// //        obj
// //        obj
// //        obj
// //        obj
//         abstract rotateLeft: tree: obj option -> unit
//         abstract next: unit -> obj option
//         abstract prev: unit -> obj option
//         abstract rotateRight: tree: obj option -> unit
//         abstract getUncle: unit -> obj option

//     type [<AllowNullLiteral>] NStatic =
//         /// A created node is always red!
//         [<Emit "new $0($1...)">] abstract Create: ``val``: 'V -> N<'V>

// module __dist_url =

//     type [<AllowNullLiteral>] IExports =
//         abstract decodeQueryParams: url: string -> DecodeQueryParamsReturn
//         abstract encodeQueryParams: ``params``: EncodeQueryParamsParams -> string

//     type [<AllowNullLiteral>] DecodeQueryParamsReturn =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> string with get, set

//     type [<AllowNullLiteral>] EncodeQueryParamsParams =
//         [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> string with get, set

// module __dist_url_test =
//     module T = __testing_js

//     type [<AllowNullLiteral>] IExports =
//         abstract testUrlParamQuery: tc: T.TestCase -> unit

// module __dist_websocket =
//     type Observable = __dist_observable_js.Observable

//     type [<AllowNullLiteral>] IExports =
//         abstract WebsocketClient: WebsocketClientStatic

//     type [<AllowNullLiteral>] WebsocketClient =
//         inherit Observable<string>
//         abstract url: string with get, set
//         abstract ws: WebSocket option with get, set
//         abstract binaryType: WebsocketClientBinaryType with get, set
//         abstract connected: bool with get, set
//         abstract connecting: bool with get, set
//         abstract unsuccessfulReconnects: float with get, set
//         abstract lastMessageReceived: float with get, set
//         /// Whether to connect to other peers or not
//         abstract shouldConnect: bool with get, set
//         abstract _checkInterval: NodeJS.Timer with get, set
//         abstract send: message: obj option -> unit
//         abstract disconnect: unit -> unit
//         abstract connect: unit -> unit

//     type [<AllowNullLiteral>] WebsocketClientStatic =
//         [<Emit "new $0($1...)">] abstract Create: url: string * ?p1: WebsocketClientStaticCreate -> WebsocketClient

//     type [<StringEnum>] [<RequireQualifiedAccess>] WebsocketClientBinaryType =
//         | Arraybuffer
//         | Blob

//     type [<AllowNullLiteral>] WebsocketClientStaticCreate =
//         abstract binaryType: WebsocketClientBinaryType option with get, set

// module __prng_Mt19937 =

//     type [<AllowNullLiteral>] IExports =
//         abstract Mt19937: Mt19937Static

//     /// This is a port of Shawn Cokus's implementation of the original Mersenne Twister algorithm (http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/MTARCOK/mt19937ar-cok.c).
//     /// MT has a very high period of 2^19937. Though the authors of xorshift describe that a high period is not
//     /// very relevant (http://vigna.di.unimi.it/xorshift/). It is four times slower than xoroshiro128plus and
//     /// needs to recompute its state after generating 624 numbers.
//     /// 
//     /// ```js
//     /// const gen = new Mt19937(new Date().getTime())
//     /// console.log(gen.next())
//     /// ```
//     type [<AllowNullLiteral>] Mt19937 =
//         abstract seed: float with get, set
//         abstract _state: Uint32Array with get, set
//         abstract _i: float with get, set
//         /// Generate a random signed integer.
//         abstract next: unit -> float

//     /// This is a port of Shawn Cokus's implementation of the original Mersenne Twister algorithm (http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/MTARCOK/mt19937ar-cok.c).
//     /// MT has a very high period of 2^19937. Though the authors of xorshift describe that a high period is not
//     /// very relevant (http://vigna.di.unimi.it/xorshift/). It is four times slower than xoroshiro128plus and
//     /// needs to recompute its state after generating 624 numbers.
//     /// 
//     /// ```js
//     /// const gen = new Mt19937(new Date().getTime())
//     /// console.log(gen.next())
//     /// ```
//     type [<AllowNullLiteral>] Mt19937Static =
//         /// <param name="seed">Unsigned 32 bit number</param>
//         [<Emit "new $0($1...)">] abstract Create: seed: float -> Mt19937

// module __prng_Xoroshiro128plus =

//     type [<AllowNullLiteral>] IExports =
//         abstract Xoroshiro128plus: Xoroshiro128plusStatic

//     /// This is a variant of xoroshiro128plus - the fastest full-period generator passing BigCrush without systematic failures.
//     /// 
//     /// This implementation follows the idea of the original xoroshiro128plus implementation,
//     /// but is optimized for the JavaScript runtime. I.e.
//     /// * The operations are performed on 32bit integers (the original implementation works with 64bit values).
//     /// * The initial 128bit state is computed based on a 32bit seed and Xorshift32.
//     /// * This implementation returns two 32bit values based on the 64bit value that is computed by xoroshiro128plus.
//     ///    Caution: The last addition step works slightly different than in the original implementation - the add carry of the
//     ///    first 32bit addition is not carried over to the last 32bit.
//     /// 
//     /// [Reference implementation](http://vigna.di.unimi.it/xorshift/xoroshiro128plus.c)
//     type [<AllowNullLiteral>] Xoroshiro128plus =
//         abstract seed: float with get, set
//         abstract state: Uint32Array with get, set
//         abstract _fresh: bool with get, set
//         abstract next: unit -> float

//     /// This is a variant of xoroshiro128plus - the fastest full-period generator passing BigCrush without systematic failures.
//     /// 
//     /// This implementation follows the idea of the original xoroshiro128plus implementation,
//     /// but is optimized for the JavaScript runtime. I.e.
//     /// * The operations are performed on 32bit integers (the original implementation works with 64bit values).
//     /// * The initial 128bit state is computed based on a 32bit seed and Xorshift32.
//     /// * This implementation returns two 32bit values based on the 64bit value that is computed by xoroshiro128plus.
//     ///    Caution: The last addition step works slightly different than in the original implementation - the add carry of the
//     ///    first 32bit addition is not carried over to the last 32bit.
//     /// 
//     /// [Reference implementation](http://vigna.di.unimi.it/xorshift/xoroshiro128plus.c)
//     type [<AllowNullLiteral>] Xoroshiro128plusStatic =
//         /// <param name="seed">Unsigned 32 bit number</param>
//         [<Emit "new $0($1...)">] abstract Create: seed: float -> Xoroshiro128plus

// module __prng_Xorshift32 =

//     type [<AllowNullLiteral>] IExports =
//         abstract Xorshift32: Xorshift32Static

//     /// Xorshift32 is a very simple but elegang PRNG with a period of `2^32-1`.
//     type [<AllowNullLiteral>] Xorshift32 =
//         abstract seed: float with get, set
//         abstract _state: float with get, set
//         /// Generate a random signed integer.
//         abstract next: unit -> float

//     /// Xorshift32 is a very simple but elegang PRNG with a period of `2^32-1`.
//     type [<AllowNullLiteral>] Xorshift32Static =
//         /// <param name="seed">Unsigned 32 bit number</param>
//         [<Emit "new $0($1...)">] abstract Create: seed: float -> Xorshift32

// module __dist_prng_Mt19937 =

//     type [<AllowNullLiteral>] IExports =
//         abstract Mt19937: Mt19937Static

//     /// This is a port of Shawn Cokus's implementation of the original Mersenne Twister algorithm (http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/MTARCOK/mt19937ar-cok.c).
//     /// MT has a very high period of 2^19937. Though the authors of xorshift describe that a high period is not
//     /// very relevant (http://vigna.di.unimi.it/xorshift/). It is four times slower than xoroshiro128plus and
//     /// needs to recompute its state after generating 624 numbers.
//     /// 
//     /// ```js
//     /// const gen = new Mt19937(new Date().getTime())
//     /// console.log(gen.next())
//     /// ```
//     type [<AllowNullLiteral>] Mt19937 =
//         abstract seed: float with get, set
//         abstract _state: Uint32Array with get, set
//         abstract _i: float with get, set
//         /// Generate a random signed integer.
//         abstract next: unit -> float

//     /// This is a port of Shawn Cokus's implementation of the original Mersenne Twister algorithm (http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/MTARCOK/mt19937ar-cok.c).
//     /// MT has a very high period of 2^19937. Though the authors of xorshift describe that a high period is not
//     /// very relevant (http://vigna.di.unimi.it/xorshift/). It is four times slower than xoroshiro128plus and
//     /// needs to recompute its state after generating 624 numbers.
//     /// 
//     /// ```js
//     /// const gen = new Mt19937(new Date().getTime())
//     /// console.log(gen.next())
//     /// ```
//     type [<AllowNullLiteral>] Mt19937Static =
//         /// <param name="seed">Unsigned 32 bit number</param>
//         [<Emit "new $0($1...)">] abstract Create: seed: float -> Mt19937

// module __dist_prng_Xoroshiro128plus =

//     type [<AllowNullLiteral>] IExports =
//         abstract Xoroshiro128plus: Xoroshiro128plusStatic

//     /// This is a variant of xoroshiro128plus - the fastest full-period generator passing BigCrush without systematic failures.
//     /// 
//     /// This implementation follows the idea of the original xoroshiro128plus implementation,
//     /// but is optimized for the JavaScript runtime. I.e.
//     /// * The operations are performed on 32bit integers (the original implementation works with 64bit values).
//     /// * The initial 128bit state is computed based on a 32bit seed and Xorshift32.
//     /// * This implementation returns two 32bit values based on the 64bit value that is computed by xoroshiro128plus.
//     ///    Caution: The last addition step works slightly different than in the original implementation - the add carry of the
//     ///    first 32bit addition is not carried over to the last 32bit.
//     /// 
//     /// [Reference implementation](http://vigna.di.unimi.it/xorshift/xoroshiro128plus.c)
//     type [<AllowNullLiteral>] Xoroshiro128plus =
//         abstract seed: float with get, set
//         abstract state: Uint32Array with get, set
//         abstract _fresh: bool with get, set
//         abstract next: unit -> float

//     /// This is a variant of xoroshiro128plus - the fastest full-period generator passing BigCrush without systematic failures.
//     /// 
//     /// This implementation follows the idea of the original xoroshiro128plus implementation,
//     /// but is optimized for the JavaScript runtime. I.e.
//     /// * The operations are performed on 32bit integers (the original implementation works with 64bit values).
//     /// * The initial 128bit state is computed based on a 32bit seed and Xorshift32.
//     /// * This implementation returns two 32bit values based on the 64bit value that is computed by xoroshiro128plus.
//     ///    Caution: The last addition step works slightly different than in the original implementation - the add carry of the
//     ///    first 32bit addition is not carried over to the last 32bit.
//     /// 
//     /// [Reference implementation](http://vigna.di.unimi.it/xorshift/xoroshiro128plus.c)
//     type [<AllowNullLiteral>] Xoroshiro128plusStatic =
//         /// <param name="seed">Unsigned 32 bit number</param>
//         [<Emit "new $0($1...)">] abstract Create: seed: float -> Xoroshiro128plus

// module __dist_prng_Xorshift32 =

//     type [<AllowNullLiteral>] IExports =
//         abstract Xorshift32: Xorshift32Static

//     /// Xorshift32 is a very simple but elegang PRNG with a period of `2^32-1`.
//     type [<AllowNullLiteral>] Xorshift32 =
//         abstract seed: float with get, set
//         abstract _state: float with get, set
//         /// Generate a random signed integer.
//         abstract next: unit -> float

//     /// Xorshift32 is a very simple but elegang PRNG with a period of `2^32-1`.
//     type [<AllowNullLiteral>] Xorshift32Static =
//         /// <param name="seed">Unsigned 32 bit number</param>
//         [<Emit "new $0($1...)">] abstract Create: seed: float -> Xorshift32
