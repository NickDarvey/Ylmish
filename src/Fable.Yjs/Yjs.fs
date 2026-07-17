module rec Yjs

#nowarn "1182"

open System
open Fable.Core
open Fable.Core.JS

open Browser.Types

type Array<'T> = System.Collections.Generic.IList<'T>
type Function = System.Action
type IterableIterator<'T> = 'T seq
type Iterable<'T> = 'T seq

type [<AllowNullLiteral>] IteratorYieldResult<'TYield> =
    abstract ``done``: bool option with get, set
    abstract value: 'TYield with get, set

type [<AllowNullLiteral>] IteratorReturnResult<'TReturn> =
    abstract ``done``: bool with get, set
    abstract value: 'TReturn with get, set

type IteratorResult<'T> =
    IteratorResult<'T, obj option>

type IteratorResult<'T, 'TReturn> =
    U2<IteratorYieldResult<'T>, IteratorReturnResult<'TReturn>>

type [<AllowNullLiteral>] Iterator<'T, 'TReturn, 'TNext> =
    abstract next: 'TNext -> IteratorResult<'T, 'TReturn>
    abstract ``return``: ?value: 'TReturn -> IteratorResult<'T, 'TReturn>
    abstract throw: ?e: obj -> IteratorResult<'T, 'TReturn>

type [<AllowNullLiteral>] Generator<'T, 'TReturn, 'TNext> =
    inherit Iterator<'T, 'TReturn, 'TNext>
    abstract next: 'TNext -> IteratorResult<'T, 'TReturn>
    abstract ``return``: value: 'TReturn -> IteratorResult<'T, 'TReturn>
    abstract throw: e: obj option -> IteratorResult<'T, 'TReturn>
    abstract ``[Symbol.iterator]``: unit -> Generator<'T, 'TReturn, 'TNext>

module Structs =
    module AbstractStruct =
        type ID = Utils.ID.ID
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type Transaction = Utils.Transaction.Transaction

        type [<AllowNullLiteral>] IExports =
            abstract AbstractStruct: AbstractStructStatic

        type [<AllowNullLiteral>] AbstractStruct =
            abstract id: ID with get, set
            abstract length: float with get, set
    //        obj
            /// Merge this struct with the item to the right.
            /// This method is already assuming that `this.id.clock + this.length === this.id.clock`.
            /// Also this method does *not* remove right from StructStore!
            abstract mergeWith: right: AbstractStruct -> bool
            /// <param name="encoder">The encoder to write data to.</param>
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float * encodingRef: float -> unit
            abstract integrate: transaction: Transaction * offset: float -> unit

        type [<AllowNullLiteral>] AbstractStructStatic =
            [<Emit "new $0($1...)">] abstract Create: id: ID * length: float -> AbstractStruct

    module ContentAny =
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentAny: ContentAnyStatic
            abstract readContentAny: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentAny

        type [<AllowNullLiteral>] ContentAny =
            abstract arr: Array<obj option> with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentAny
            abstract splice: offset: float -> ContentAny
            abstract mergeWith: right: ContentAny -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentAnyStatic =
            [<Emit "new $0($1...)">] abstract Create: arr: Array<obj option> -> ContentAny

    module ContentBinary =
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentBinary: ContentBinaryStatic
            abstract readContentBinary: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentBinary

        type [<AllowNullLiteral>] ContentBinary =
            abstract content: Uint8Array with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentBinary
            abstract splice: offset: float -> ContentBinary
            abstract mergeWith: right: ContentBinary -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentBinaryStatic =
            [<Emit "new $0($1...)">] abstract Create: content: Uint8Array -> ContentBinary

    module ContentDeleted =
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentDeleted: ContentDeletedStatic
            abstract readContentDeleted: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentDeleted

        type [<AllowNullLiteral>] ContentDeleted =
            abstract len: float with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentDeleted
            abstract splice: offset: float -> ContentDeleted
            abstract mergeWith: right: ContentDeleted -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentDeletedStatic =
            [<Emit "new $0($1...)">] abstract Create: len: float -> ContentDeleted

    module ContentDoc =
        type Doc = Utils.Doc.Doc
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentDoc: ContentDocStatic
            abstract readContentDoc: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentDoc

        type [<AllowNullLiteral>] ContentDoc =
            abstract doc: Doc with get, set
            abstract opts: obj option with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentDoc
            abstract splice: offset: float -> ContentDoc
            abstract mergeWith: right: ContentDoc -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentDocStatic =
            [<Emit "new $0($1...)">] abstract Create: doc: Doc -> ContentDoc

    module ContentEmbed =
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentEmbed: ContentEmbedStatic
            abstract readContentEmbed: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentEmbed

        type [<AllowNullLiteral>] ContentEmbed =
            abstract embed: Object with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentEmbed
            abstract splice: offset: float -> ContentEmbed
            abstract mergeWith: right: ContentEmbed -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentEmbedStatic =
            [<Emit "new $0($1...)">] abstract Create: embed: Object -> ContentEmbed

    module ContentFormat =
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentFormat: ContentFormatStatic
            abstract readContentFormat: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentFormat

        type [<AllowNullLiteral>] ContentFormat =
            abstract key: string with get, set
            abstract value: Object with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentFormat
            abstract splice: offset: float -> ContentFormat
            abstract mergeWith: right: ContentFormat -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentFormatStatic =
            [<Emit "new $0($1...)">] abstract Create: key: string * value: Object -> ContentFormat

    module ContentJSON =
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentJSON: ContentJSONStatic
            abstract readContentJSON: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentJSON

        type [<AllowNullLiteral>] ContentJSON =
            abstract arr: Array<obj option> with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentJSON
            abstract splice: offset: float -> ContentJSON
            abstract mergeWith: right: ContentJSON -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentJSONStatic =
            [<Emit "new $0($1...)">] abstract Create: arr: Array<obj option> -> ContentJSON

    module ContentString =
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ContentString: ContentStringStatic
            abstract readContentString: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentString

        type [<AllowNullLiteral>] ContentString =
            abstract str: string with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentString
            abstract splice: offset: float -> ContentString
            abstract mergeWith: right: ContentString -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentStringStatic =
            [<Emit "new $0($1...)">] abstract Create: str: string -> ContentString

    module ContentType =
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Transaction = Utils.Transaction.Transaction
        type Item = Structs.Item.Item
        type StructStore = Utils.StructStore.StructStore
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2

        type [<AllowNullLiteral>] IExports =
            abstract typeRefs: Array<(U2<UpdateDecoderV1, UpdateDecoderV2> -> AbstractType<obj option>)>
            abstract YArrayRefID: obj
            abstract YMapRefID: obj
            abstract YTextRefID: obj
            abstract YXmlElementRefID: obj
            abstract YXmlFragmentRefID: obj
            abstract YXmlHookRefID: obj
            abstract YXmlTextRefID: obj
            abstract ContentType: ContentTypeStatic
            abstract readContentType: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> ContentType

        type [<AllowNullLiteral>] ContentType =
            abstract ``type``: AbstractType<obj option> with get, set
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            abstract isCountable: unit -> bool
            abstract copy: unit -> ContentType
            abstract splice: offset: float -> ContentType
            abstract mergeWith: right: ContentType -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        type [<AllowNullLiteral>] ContentTypeStatic =
            [<Emit "new $0($1...)">] abstract Create: ``type``: AbstractType<obj option> -> ContentType

    module GC =
        type AbstractStruct = Structs.AbstractStruct.AbstractStruct
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type Transaction = Utils.Transaction.Transaction
        type StructStore = Utils.StructStore.StructStore

        type [<AllowNullLiteral>] IExports =
            abstract structGCRefNumber: obj
            abstract GC: GCStatic

        type [<AllowNullLiteral>] GC =
            inherit AbstractStruct
            abstract delete: unit -> unit
            abstract mergeWith: right: GC -> bool
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getMissing: transaction: Transaction * store: StructStore -> float option

        type [<AllowNullLiteral>] GCStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> GC

    module Item =
        type StructStore = Utils.StructStore.StructStore
        type ID = Utils.ID.ID
        type Transaction = Utils.Transaction.Transaction
        type DeleteSet = Utils.DeleteSet.DeleteSet
        type AbstractStruct = Structs.AbstractStruct.AbstractStruct
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract followRedone: store: StructStore * id: ID -> FollowRedoneReturn
            abstract keepItem: item: Item option * keep: bool -> unit
            abstract splitItem: transaction: Transaction * leftItem: Item * diff: float -> Item
            abstract redoItem: transaction: Transaction * item: Item * redoitems: Set<Item> * itemsToDelete: DeleteSet * ignoreRemoteMapChanges: bool -> Item option
            abstract Item: ItemStatic
            abstract readItemContent: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> * info: float -> AbstractContent
            abstract contentRefs: Array<(U2<UpdateDecoderV1, UpdateDecoderV2> -> AbstractContent)>
            abstract AbstractContent: AbstractContentStatic

        type [<AllowNullLiteral>] FollowRedoneReturn =
            abstract item: Item with get, set
            abstract diff: float with get, set

        /// Abstract class that represents any content.
        type [<AllowNullLiteral>] Item =
            inherit AbstractStruct
            /// The item that was originally to the left of this item.
            abstract origin: ID option with get, set
            /// The item that is currently to the left of this item.
            abstract left: Item option with get, set
            /// The item that is currently to the right of this item.
            abstract right: Item option with get, set
            /// The item that was originally to the right of this item.
            abstract rightOrigin: ID option with get, set
            abstract parent: U2<AbstractType<obj option>, ID> option with get, set
            /// If the parent refers to this item with some kind of key (e.g. YMap, the
            /// key is specified here. The key is then used to refer to the list in which
            /// to insert this item. If `parentSub = null` type._start is the list in
            /// which to insert to. Otherwise it is `parent._map`.
            abstract parentSub: string option with get, set
            /// If this type's effect is redone this type refers to the type that undid
            /// this operation.
            abstract redone: ID option with get, set
            abstract content: AbstractContent with get, set
            /// bit1: keep
            /// bit2: countable
            /// bit3: deleted
            /// bit4: mark - mark node as fast-search-marker
            abstract info: float with get, set
    //        obj
    //        obj
    //        obj
    //        obj
    //        obj
    //        obj
    //        obj
            abstract markDeleted: unit -> unit
            /// Return the creator clientID of the missing op or define missing items and return null.
            abstract getMissing: transaction: Transaction * store: StructStore -> float option
    //        obj
    //        obj
    //        obj
            /// Try to merge two items
            abstract mergeWith: right: Item -> bool
            /// Mark this Item as deleted.
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore * parentGCd: bool -> unit
            /// <summary>Transform the properties of this type to binary and write it to an
            /// BinaryEncoder.
            /// 
            /// This is called when this Item is sent to a remote peer.</summary>
            /// <param name="encoder">The encoder to write data to.</param>
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit

        /// Abstract class that represents any content.
        type [<AllowNullLiteral>] ItemStatic =
            /// <param name="parent">Is a type if integrated, is null if it is possible to copy parent from left or right, is ID before integration to search for it.</param>
            [<Emit "new $0($1...)">] abstract Create: id: ID * left: Item option * origin: ID option * right: Item option * rightOrigin: ID option * parent: U2<AbstractType<obj option>, ID> option * parentSub: string option * content: AbstractContent -> Item

        /// Do not implement this class!
        type [<AllowNullLiteral>] AbstractContent =
            abstract getLength: unit -> float
            abstract getContent: unit -> Array<obj option>
            /// Should return false if this Item is some kind of meta information
            /// (e.g. format information).
            /// 
            /// * Whether this Item should be addressable via `yarray.get(i)`
            /// * Whether this Item should be counted when computing yarray.length
            abstract isCountable: unit -> bool
            abstract copy: unit -> AbstractContent
            abstract splice: offset: float -> AbstractContent
            abstract mergeWith: right: AbstractContent -> bool
            abstract integrate: transaction: Transaction * item: Item -> unit
            abstract delete: transaction: Transaction -> unit
            abstract gc: store: StructStore -> unit
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getRef: unit -> float

        /// Do not implement this class!
        type [<AllowNullLiteral>] AbstractContentStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> AbstractContent

    module Skip =
        type AbstractStruct = Structs.AbstractStruct.AbstractStruct
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type Transaction = Utils.Transaction.Transaction
        type StructStore = Utils.StructStore.StructStore

        type [<AllowNullLiteral>] IExports =
            abstract structSkipRefNumber: obj
            abstract Skip: SkipStatic

        type [<AllowNullLiteral>] Skip =
            inherit AbstractStruct
            abstract delete: unit -> unit
            abstract mergeWith: right: Skip -> bool
            abstract write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * offset: float -> unit
            abstract getMissing: transaction: Transaction * store: StructStore -> float option

        type [<AllowNullLiteral>] SkipStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> Skip

module Types =
    module AbstractType =
        type Item = Structs.Item.Item
        type Transaction = Utils.Transaction.Transaction
        type Doc = Utils.Doc.Doc
        type EventHandler<'ARG0, 'ARG1> = Utils.EventHandler.EventHandler<'ARG0, 'ARG1>
        type YEvent<'T, 'E> = Utils.YEvent.YEvent<'T, 'E>
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type Snapshot = Utils.Snapshot.Snapshot

        type [<AllowNullLiteral>] IExports =
            abstract ArraySearchMarker: ArraySearchMarkerStatic
            abstract findMarker: yarray: AbstractType<obj option> * index: float -> ArraySearchMarker option
            abstract updateMarkerChanges: searchMarker: Array<ArraySearchMarker> * index: float * len: float -> unit
            abstract getTypeChildren: t: AbstractType<obj option> -> Array<Item>
            abstract callTypeObservers: ``type``: AbstractType<'EventType> * transaction: Transaction * ``event``: 'EventType -> unit
            abstract AbstractType: AbstractTypeStatic
            abstract typeListSlice: ``type``: AbstractType<obj option> * start: float * ``end``: float -> Array<obj option>
            abstract typeListToArray: ``type``: AbstractType<obj option> -> Array<obj option>
            abstract typeListToArraySnapshot: ``type``: AbstractType<obj option> * snapshot: Snapshot -> Array<obj option>
            abstract typeListForEach: ``type``: AbstractType<obj option> * f: (obj option -> float -> obj option -> unit) -> unit
            abstract typeListMap: ``type``: AbstractType<obj option> * f: ('C -> float -> AbstractType<obj option> -> 'R) -> ResizeArray<'R>
            abstract typeListCreateIterator: ``type``: AbstractType<obj option> -> IterableIterator<obj option>
            abstract typeListForEachSnapshot: ``type``: AbstractType<obj option> * f: (obj option -> float -> AbstractType<obj option> -> unit) * snapshot: Snapshot -> unit
            abstract typeListGet: ``type``: AbstractType<obj option> * index: float -> obj option
            abstract typeListInsertGenericsAfter: transaction: Transaction * parent: AbstractType<obj option> * referenceItem: Item option * content: Array<U6<IExportsTypeListInsertGenericsAfterArray, Array<obj option>, bool, float, string, Uint8Array> option> -> unit
            abstract typeListInsertGenerics: transaction: Transaction * parent: AbstractType<obj option> * index: float * content: Array<U5<IExportsTypeListInsertGenericsAfterArray, Array<obj option>, float, string, Uint8Array> option> -> unit
            abstract typeListPushGenerics: transaction: Transaction * parent: AbstractType<obj option> * content: Array<U5<IExportsTypeListInsertGenericsAfterArray, Array<obj option>, float, string, Uint8Array> option> -> unit
            abstract typeListDelete: transaction: Transaction * parent: AbstractType<obj option> * index: float * length: float -> unit
            abstract typeMapDelete: transaction: Transaction * parent: AbstractType<obj option> * key: string -> unit
            abstract typeMapSet: transaction: Transaction * parent: AbstractType<obj option> * key: string * value: U6<Object, float, Array<obj option>, string, Uint8Array, AbstractType<obj option>> option -> unit
            abstract typeMapGet: parent: AbstractType<obj option> * key: string -> U6<IExportsTypeListInsertGenericsAfterArray, float, Array<obj option>, string, Uint8Array, AbstractType<obj option>> option
            abstract typeMapGetAll: parent: AbstractType<obj option> -> TypeMapGetAllReturn
            abstract typeMapHas: parent: AbstractType<obj option> * key: string -> bool
            abstract typeMapGetSnapshot: parent: AbstractType<obj option> * key: string * snapshot: Snapshot -> U6<IExportsTypeListInsertGenericsAfterArray, float, Array<obj option>, string, Uint8Array, AbstractType<obj option>> option
            abstract createMapIterator: map: Map<string, Item> -> IterableIterator<Array<obj option>>

        type [<AllowNullLiteral>] TypeMapGetAllReturn =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> U6<IExportsTypeListInsertGenericsAfterArray, float, Array<obj option>, string, Uint8Array, AbstractType<obj option>> option with get, set

        type [<AllowNullLiteral>] ArraySearchMarker =
            abstract p: Item with get, set
            abstract index: float with get, set
            abstract timestamp: float with get, set

        type [<AllowNullLiteral>] ArraySearchMarkerStatic =
            [<Emit "new $0($1...)">] abstract Create: p: Item * index: float -> ArraySearchMarker

        type [<AllowNullLiteral>] AbstractType<'EventType> =
            abstract _item: Item option with get, set
            abstract _map: Map<string, Item> with get, set
            abstract _start: Item option with get, set
            abstract doc: Doc option with get, set
            abstract _length: float with get, set
            /// Event handlers
            abstract _eH: EventHandler<'EventType, Transaction> with get, set
            /// Deep event handlers
            abstract _dEH: EventHandler<Array<YEvent<obj option, obj>>, Transaction> with get, set
            abstract _searchMarker: Array<ArraySearchMarker> option with get, set
    //        obj
            /// <summary>Integrate this type into the Yjs instance.
            /// 
            /// * Save this struct in the os
            /// * This type is sent to other client
            /// * Observer functions are fired</summary>
            /// <param name="y">The Yjs instance</param>
            abstract _integrate: y: Doc * item: Item option -> unit
            abstract _copy: unit -> AbstractType<'EventType>
            abstract clone: unit -> AbstractType<'EventType>
            abstract _write: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> -> unit
    //        obj
            /// <summary>Creates YEvent and calls all type observers.
            /// Must be implemented by each type.</summary>
            /// <param name="parentSubs">Keys changed on this type. `null` if list was modified.</param>
            abstract _callObserver: transaction: Transaction * parentSubs: Set<string option> -> unit
            /// <summary>Observe all events that are created on this type.</summary>
            /// <param name="f">Observer function</param>
            abstract observe: f: ('EventType -> Transaction -> unit) -> unit
            /// <summary>Observe all events that are created by this type and its children.</summary>
            /// <param name="f">Observer function</param>
            abstract observeDeep: f: (Array<YEvent<obj option, obj>> -> Transaction -> unit) -> unit
            /// <summary>Unregister an observer function.</summary>
            /// <param name="f">Observer function</param>
            abstract unobserve: f: ('EventType -> Transaction -> unit) -> unit
            /// <summary>Unregister an observer function.</summary>
            /// <param name="f">Observer function</param>
            abstract unobserveDeep: f: (Array<YEvent<obj option, obj>> -> Transaction -> unit) -> unit
            abstract toJSON: unit -> obj option

        type [<AllowNullLiteral>] AbstractTypeStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> AbstractType<'EventType>

        type [<AllowNullLiteral>] IExportsTypeListInsertGenericsAfterArray =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

    module YArray =
        type YEvent<'T, 'E> = Utils.YEvent.YEvent<'T, 'E>
        type Transaction = Utils.Transaction.Transaction
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type ArraySearchMarker = Types.AbstractType.ArraySearchMarker
        type Doc = Utils.Doc.Doc
        type Item = Structs.Item.Item
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract YArrayEvent: YArrayEventStatic
            abstract YArray: YArrayStatic
            abstract readYArray: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> YArray<obj option>

        /// Event that describes the changes on a YArray
        type [<AllowNullLiteral>] YArrayEvent<'T> =
            inherit YEvent<YArray<'T>, Array<'T>>
            abstract _transaction: Transaction with get, set

        /// Event that describes the changes on a YArray
        type [<AllowNullLiteral>] YArrayEventStatic =
            /// <param name="yarray">The changed type</param>
            /// <param name="transaction">The transaction object</param>
            [<Emit "new $0($1...)">] abstract Create: yarray: YArray<'T> * transaction: Transaction -> YArrayEvent<'T>

        /// A shared Array implementation.
        type [<AllowNullLiteral>] YArray<'T> =
            inherit AbstractType<YArrayEvent<'T>>
            inherit Iterable<'T>
            abstract _searchMarker: Array<ArraySearchMarker> with get, set
            /// <summary>Integrate this type into the Yjs instance.
            /// 
            /// * Save this struct in the os
            /// * This type is sent to other client
            /// * Observer functions are fired</summary>
            /// <param name="y">The Yjs instance</param>
            abstract _integrate: y: Doc * item: Item -> unit
            abstract _copy: unit -> YArray<obj option>
            abstract clone: unit -> YArray<'T>
    //        obj
            /// <summary>Inserts new content at an index.
            /// 
            /// Important: This function expects an array of content. Not just a content
            /// object. The reason for this "weirdness" is that inserting several elements
            /// is very efficient when it is done as a single operation.</summary>
            /// <param name="index">The index to insert content at.</param>
            /// <param name="content">The array of content</param>
            abstract insert: index: float * content: Array<'T> -> unit
            /// <summary>Appends content to this YArray.</summary>
            /// <param name="content">Array of content to append.</param>
            abstract push: content: Array<'T> -> unit
            /// <summary>Preppends content to this YArray.</summary>
            /// <param name="content">Array of content to preppend.</param>
            abstract unshift: content: Array<'T> -> unit
            /// <summary>Deletes elements starting from an index.</summary>
            /// <param name="index">Index at which to start deleting elements</param>
            /// <param name="length">The number of elements to remove. Defaults to 1.</param>
            abstract delete: index: float * ?length: float -> unit
            /// <summary>Returns the i-th element from a YArray.</summary>
            /// <param name="index">The index of the element to return from the YArray</param>
            abstract get: index: float -> 'T
            /// Transforms this YArray to a JavaScript Array.
            abstract toArray: unit -> Array<'T>
            /// Transforms this YArray to a JavaScript Array.
            abstract slice: ?start: float * ?``end``: float -> Array<'T>
            /// Transforms this Shared Type to a JSON object.
            abstract toJSON: unit -> Array<obj option>
            /// <summary>Returns an Array with the result of calling a provided function on every
            /// element of this YArray.</summary>
            /// <param name="f">Function that produces an element of the new Array</param>
            abstract map: f: ('T -> float -> YArray<'T> -> 'M) -> ResizeArray<'M>
            /// <summary>Executes a provided function on once on overy element of this YArray.</summary>
            /// <param name="f">A function to execute on every element of this YArray.</param>
            abstract forEach: f: ('T -> float -> YArray<'T> -> unit) -> unit
            abstract ``[Symbol.iterator]``: unit -> IterableIterator<'T>

        /// A shared Array implementation.
        type [<AllowNullLiteral>] YArrayStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> YArray<'T>
            /// Construct a new YArray containing the specified items.
            abstract from: items: ResizeArray<'T_1> -> YArray<'T_1>

    module YMap =
        type YEvent<'T, 'E> = Utils.YEvent.YEvent<'T, 'E>
        type Transaction = Utils.Transaction.Transaction
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Doc = Utils.Doc.Doc
        type Item = Structs.Item.Item
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract YMapEvent: YMapEventStatic
            abstract YMap: YMapStatic
            abstract readYMap: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> YMap<obj option>

        type [<AllowNullLiteral>] YMapEvent<'T> =
            inherit YEvent<YMap<'T>, obj>
            abstract keysChanged: Set<obj option> with get, set

        type [<AllowNullLiteral>] YMapEventStatic =
            /// <param name="ymap">The YArray that changed.</param>
            /// <param name="subs">The keys that changed.</param>
            [<Emit "new $0($1...)">] abstract Create: ymap: YMap<'T> * transaction: Transaction * subs: Set<obj option> -> YMapEvent<'T>

        type [<AllowNullLiteral>] YMap<'MapType> =
            inherit AbstractType<YMapEvent<'MapType>>
            inherit Iterable<'MapType>
            /// <summary>Integrate this type into the Yjs instance.
            /// 
            /// * Save this struct in the os
            /// * This type is sent to other client
            /// * Observer functions are fired</summary>
            /// <param name="y">The Yjs instance</param>
            abstract _integrate: y: Doc * item: Item -> unit
            abstract _copy: unit -> YMap<obj option>
            abstract clone: unit -> YMap<'MapType>
            /// Transforms this Shared Type to a JSON object.
            abstract toJSON: unit -> YMapToJSONReturn
    //        obj
            /// Returns the keys for each element in the YMap Type.
            abstract keys: unit -> IterableIterator<string>
            /// Returns the values for each element in the YMap Type.
            abstract values: unit -> IterableIterator<obj option>
            /// Returns an Iterator of [key, value] pairs
            abstract entries: unit -> IterableIterator<(string * obj option)>
            /// <summary>Executes a provided function on once on every key-value pair.</summary>
            /// <param name="f">A function to execute on every element of this YArray.</param>
            abstract forEach: f: ('MapType -> string -> YMap<'MapType> -> unit) -> YMapForEachReturn<'MapType>
            /// <summary>Remove a specified element from this YMap.</summary>
            /// <param name="key">The key of the element to remove.</param>
            abstract delete: key: string -> unit
            /// <summary>Adds or updates an element with a specified key and value.</summary>
            /// <param name="key">The key of the element to add to this YMap</param>
            /// <param name="value">The value of the element to add</param>
            abstract set: key: string * value: 'MapType -> 'MapType
            /// Returns a specified element from this YMap.
            abstract get: key: string -> 'MapType option
            /// <summary>Returns a boolean indicating whether the specified key exists or not.</summary>
            /// <param name="key">The key to test.</param>
            abstract has: key: string -> bool
            /// Removes all elements from this YMap.
            abstract clear: unit -> unit
            /// Returns an Iterator of [key, value] pairs
            abstract ``[Symbol.iterator]``: unit -> IterableIterator<obj option>

        type [<AllowNullLiteral>] YMapToJSONReturn =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

        type [<AllowNullLiteral>] YMapForEachReturn<'MapType> =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> 'MapType with get, set

        type [<AllowNullLiteral>] YMapStatic =
            /// <param name="entries">- an optional iterable to initialize the YMap</param>
            [<Emit "new $0($1...)">] abstract Create: ?entries: Iterable<string * obj option> -> YMap<'MapType>

    module YText =
        type Item = Structs.Item.Item
        type YEvent<'T, 'E> = Utils.YEvent.YEvent<'T, 'E>
        type Delta<'insert> = Utils.YEvent.Delta<'insert>
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Transaction = Utils.Transaction.Transaction
        type ArraySearchMarker = Types.AbstractType.ArraySearchMarker
        type Doc = Utils.Doc.Doc
        type Snapshot = Utils.Snapshot.Snapshot
        type ID = Utils.ID.ID
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract ItemTextListPosition: ItemTextListPositionStatic
            abstract cleanupYTextFormatting: ``type``: YText -> float
            abstract YTextEvent: YTextEventStatic
            abstract YText: YTextStatic
            abstract readYText: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> YText

        type [<AllowNullLiteral>] ItemTextListPosition =
            abstract left: Item option with get, set
            abstract right: Item option with get, set
            abstract index: float with get, set
            abstract currentAttributes: Map<string, obj option> with get, set
            /// Only call this if you know that this.right is defined
            abstract forward: unit -> unit

        type [<AllowNullLiteral>] ItemTextListPositionStatic =
            [<Emit "new $0($1...)">] abstract Create: left: Item option * right: Item option * index: float * currentAttributes: Map<string, obj option> -> ItemTextListPosition

        type [<AllowNullLiteral>] YTextEvent =
            inherit YEvent<YText, string>
            /// Set of all changed attributes.
            abstract keysChanged: Set<string> with get, set
    //        obj

        type [<AllowNullLiteral>] YTextEventStatic =
            /// <param name="subs">The keys that changed</param>
            [<Emit "new $0($1...)">] abstract Create: ytext: YText * transaction: Transaction * subs: Set<obj option> -> YTextEvent

        /// Type that represents text with formatting information.
        /// 
        /// This type replaces y-richtext as this implementation is able to handle
        /// block formats (format information on a paragraph), embeds (complex elements
        /// like pictures and videos), and text formats (**bold**, *italic*).
        type [<AllowNullLiteral>] YText =
            inherit AbstractType<YTextEvent>
            /// Array of pending operations on this type
            abstract _pending: ResizeArray<(unit -> unit)> option with get, set
            abstract _searchMarker: Array<ArraySearchMarker> with get, set
    //        obj
            abstract _integrate: y: Doc * item: Item -> unit
            abstract _copy: unit -> YText
            abstract clone: unit -> YText
            /// Returns the unformatted string representation of this YText type.
            abstract toJSON: unit -> string
            /// <summary>Apply a {@link Delta} on this shared YText type.</summary>
            /// <param name="delta">The changes to apply on this element.</param>
            abstract applyDelta: delta: ResizeArray<Delta<string>> * ?p1: YTextApplyDelta -> unit
            /// Returns the Delta representation of this YText type.
            abstract toDelta: ?snapshot: Snapshot * ?prevSnapshot: Snapshot * ?computeYChange: (YTextToDelta -> ID -> obj option) -> obj option
            /// <summary>Insert text at a given index.</summary>
            /// <param name="index">The index at which to start inserting.</param>
            /// <param name="text">The text to insert at the specified position.</param>
            /// <param name="attributes">Optionally define some formatting
            /// information to apply on the inserted
            /// Text.</param>
            abstract insert: index: int * text: string * ?attributes: Object -> unit
            /// <summary>Inserts an embed at a index.</summary>
            /// <param name="index">The index to insert the embed at.</param>
            /// <param name="embed">The Object that represents the embed.</param>
            /// <param name="attributes">Attribute information to apply on the
            /// embed</param>
            abstract insertEmbed: index: int * embed: U2<Object, AbstractType<obj option>> * ?attributes: TextAttributes -> unit
            /// <summary>Deletes text starting from an index.</summary>
            /// <param name="index">Index at which to start deleting.</param>
            /// <param name="length">The number of characters to remove. Defaults to 1.</param>
            abstract delete: index: int * length: int -> unit
            /// <summary>Assigns properties to a range of text.</summary>
            /// <param name="index">The position where to start formatting.</param>
            /// <param name="length">The amount of characters to assign properties to.</param>
            /// <param name="attributes">Attribute information to apply on the
            /// text.</param>
            abstract format: index: int * length: int * attributes: TextAttributes -> unit
            /// <summary>Removes an attribute.</summary>
            /// <param name="attributeName">The attribute name that is to be removed.</param>
            abstract removeAttribute: attributeName: string -> unit
            /// <summary>Sets or updates an attribute.</summary>
            /// <param name="attributeName">The attribute name that is to be set.</param>
            /// <param name="attributeValue">The attribute value that is to be set.</param>
            abstract setAttribute: attributeName: string * attributeValue: obj option -> unit
            /// <summary>Returns an attribute value that belongs to the attribute name.</summary>
            /// <param name="attributeName">The attribute name that identifies the
            /// queried value.</param>
            abstract getAttribute: attributeName: string -> obj option
            /// Returns all attribute name/value pairs in a JSON Object.
            abstract getAttributes: ?snapshot: Snapshot -> YTextGetAttributesReturn
            /// Number of characters of this text type.
            abstract length: int with get

            abstract toString: unit -> string
            

        type [<AllowNullLiteral>] YTextGetAttributesReturn =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

        /// Type that represents text with formatting information.
        /// 
        /// This type replaces y-richtext as this implementation is able to handle
        /// block formats (format information on a paragraph), embeds (complex elements
        /// like pictures and videos), and text formats (**bold**, *italic*).
        type [<AllowNullLiteral>] YTextStatic =
            /// <param name="string">The initial value of the YText.</param>
            [<Emit "new $0($1...)">] abstract Create: ?string: string -> YText

        type TextAttributes =
            Object

        type [<AllowNullLiteral>] YTextApplyDelta =
            abstract sanitize: bool option with get, set

        type [<StringEnum>] [<RequireQualifiedAccess>] YTextToDelta =
            | Removed
            | Added

    module YXmlElement =
        type YXmlFragment = Types.YXmlFragment.YXmlFragment
        type YXmlText = Types.YXmlText.YXmlText
        type Snapshot = Utils.Snapshot.Snapshot
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract YXmlElement: YXmlElementStatic
            abstract readYXmlElement: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> YXmlElement

        /// An YXmlElement imitates the behavior of a
        /// {@link https://developer.mozilla.org/en-US/docs/Web/API/Element|Dom Element}.
        /// 
        /// * An YXmlElement has attributes (key value pairs)
        /// * An YXmlElement has childElements that must inherit from YXmlElement
        type [<AllowNullLiteral>] YXmlElement =
            inherit YXmlFragment
            abstract nodeName: string with get, set
            abstract _prelimAttrs: Map<string, obj option> option with get, set
    //        obj
    //        obj
            /// Creates an Item with the same effect as this Item (without position effect)
            abstract _copy: unit -> YXmlElement
            abstract clone: unit -> YXmlElement
            /// <summary>Removes an attribute from this YXmlElement.</summary>
            /// <param name="attributeName">The attribute name that is to be removed.</param>
            abstract removeAttribute: attributeName: string -> unit
            /// <summary>Sets or updates an attribute.</summary>
            /// <param name="attributeName">The attribute name that is to be set.</param>
            /// <param name="attributeValue">The attribute value that is to be set.</param>
            abstract setAttribute: attributeName: string * attributeValue: string -> unit
            /// <summary>Returns an attribute value that belongs to the attribute name.</summary>
            /// <param name="attributeName">The attribute name that identifies the
            /// queried value.</param>
            abstract getAttribute: attributeName: string -> string
            /// <summary>Returns whether an attribute exists</summary>
            /// <param name="attributeName">The attribute name to check for existence.</param>
            abstract hasAttribute: attributeName: string -> bool
            /// Returns all attribute name/value pairs in a JSON Object.
            abstract getAttributes: ?snapshot: Snapshot -> YXmlElementGetAttributesReturn

        type [<AllowNullLiteral>] YXmlElementGetAttributesReturn =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

        /// An YXmlElement imitates the behavior of a
        /// {@link https://developer.mozilla.org/en-US/docs/Web/API/Element|Dom Element}.
        /// 
        /// * An YXmlElement has attributes (key value pairs)
        /// * An YXmlElement has childElements that must inherit from YXmlElement
        type [<AllowNullLiteral>] YXmlElementStatic =
            [<Emit "new $0($1...)">] abstract Create: ?nodeName: string -> YXmlElement

    module YXmlEvent =
        type YXmlFragment = Types.YXmlFragment.YXmlFragment
        type YXmlElement = Types.YXmlElement.YXmlElement
        type YXmlText = Types.YXmlText.YXmlText
        type YEvent<'T, 'E> = Utils.YEvent.YEvent<'T, 'E>
        type Transaction = Utils.Transaction.Transaction

        type [<AllowNullLiteral>] IExports =
            abstract YXmlEvent: YXmlEventStatic

        type [<AllowNullLiteral>] YXmlEvent =
            inherit YEvent<U3<YXmlFragment, YXmlElement, YXmlText>, obj>
            /// Set of all changed attributes.
            abstract attributesChanged: Set<string> with get, set

        type [<AllowNullLiteral>] YXmlEventStatic =
            /// <param name="target">The target on which the event is created.</param>
            /// <param name="subs">The set of changed attributes. `null` is included if the
            /// child list changed.</param>
            /// <param name="transaction">The transaction instance with wich the
            /// change was created.</param>
            [<Emit "new $0($1...)">] abstract Create: target: U3<YXmlElement, YXmlText, YXmlFragment> * subs: Set<string option> * transaction: Transaction -> YXmlEvent

    module YXmlFragment =
        type YXmlElement = Types.YXmlElement.YXmlElement
        type YXmlText = Types.YXmlText.YXmlText
        type YXmlHook = Types.YXmlHook.YXmlHook
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Item = Structs.Item.Item
        type YXmlEvent = Types.YXmlEvent.YXmlEvent
        type Doc = Utils.Doc.Doc
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract YXmlTreeWalker: YXmlTreeWalkerStatic
            abstract YXmlFragment: YXmlFragmentStatic
            abstract readYXmlFragment: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> YXmlFragment

        /// Represents a subset of the nodes of a YXmlElement / YXmlFragment and a
        /// position within them.
        /// 
        /// Can be created with {@link YXmlFragment#createTreeWalker}
        type [<AllowNullLiteral>] YXmlTreeWalker =
            inherit Iterable<U3<YXmlElement, YXmlText, YXmlHook>>
            abstract _filter: (AbstractType<obj option> -> bool) with get, set
            abstract _root: U2<YXmlFragment, YXmlElement> with get, set
            abstract _currentNode: Item with get, set
            abstract _firstCall: bool with get, set
            /// Get the next node.
            abstract next: unit -> IteratorResult<U3<YXmlElement, YXmlText, YXmlHook>>
            abstract ``[Symbol.iterator]``: unit -> YXmlTreeWalker

        /// Represents a subset of the nodes of a YXmlElement / YXmlFragment and a
        /// position within them.
        /// 
        /// Can be created with {@link YXmlFragment#createTreeWalker}
        type [<AllowNullLiteral>] YXmlTreeWalkerStatic =
            [<Emit "new $0($1...)">] abstract Create: root: U2<YXmlFragment, YXmlElement> * ?f: (AbstractType<obj option> -> bool) -> YXmlTreeWalker

        /// Represents a list of {@link YXmlElement}.and {@link YXmlText} types.
        /// A YxmlFragment is similar to a {@link YXmlElement}, but it does not have a
        /// nodeName and it does not have attributes. Though it can be bound to a DOM
        /// element - in this case the attributes and the nodeName are not shared.
        type [<AllowNullLiteral>] YXmlFragment =
            inherit AbstractType<YXmlEvent>
            abstract _prelimContent: Array<obj option> option with get, set
    //        obj
            /// <summary>Integrate this type into the Yjs instance.
            /// 
            /// * Save this struct in the os
            /// * This type is sent to other client
            /// * Observer functions are fired</summary>
            /// <param name="y">The Yjs instance</param>
            abstract _integrate: y: Doc * item: Item -> unit
            abstract _copy: unit -> YXmlFragment
            abstract clone: unit -> YXmlFragment
    //        obj
            /// <summary>Create a subtree of childNodes.</summary>
            /// <param name="filter">Function that is called on each child element and
            /// returns a Boolean indicating whether the child
            /// is to be included in the subtree.</param>
            abstract createTreeWalker: filter: (AbstractType<obj option> -> bool) -> YXmlTreeWalker
            /// <summary>Returns the first YXmlElement that matches the query.
            /// Similar to DOM's {@link querySelector}.
            /// 
            /// Query support:
            ///    - tagname
            /// TODO:
            ///    - id
            ///    - attribute</summary>
            /// <param name="query">The query on the children.</param>
            abstract querySelector: query: CSS_Selector -> U3<YXmlElement, YXmlText, YXmlHook> option
            /// <summary>Returns all YXmlElements that match the query.
            /// Similar to Dom's {@link querySelectorAll}.</summary>
            /// <param name="query">The query on the children</param>
            abstract querySelectorAll: query: CSS_Selector -> Array<U3<YXmlElement, YXmlText, YXmlHook> option>
            abstract toJSON: unit -> string
            /// <summary>Creates a Dom Element that mirrors this YXmlElement.</summary>
            /// <param name="_document">The document object (you must define
            ///  this when calling this method in
            ///  nodejs)</param>
            /// <param name="hooks">Optional property to customize how hooks
            ///      are presented in the DOM</param>
            /// <param name="binding">You should not set this property. This is
            ///         used if DomBinding wants to create a
            ///         association to the created DOM type.</param>
            abstract toDOM: ?_document: Document * ?hooks: YXmlFragmentToDOM * ?binding: obj -> Node
            /// <summary>Inserts new content at an index.</summary>
            /// <param name="index">The index to insert content at</param>
            /// <param name="content">The array of content</param>
            abstract insert: index: float * content: Array<U2<YXmlElement, YXmlText>> -> unit
            /// <summary>Inserts new content at an index.</summary>
            /// <param name="ref">The index to insert content at</param>
            /// <param name="content">The array of content</param>
            abstract insertAfter: ref: U3<Item, YXmlElement, YXmlText> option * content: Array<U2<YXmlElement, YXmlText>> -> unit
            /// <summary>Deletes elements starting from an index.</summary>
            /// <param name="index">Index at which to start deleting elements</param>
            /// <param name="length">The number of elements to remove. Defaults to 1.</param>
            abstract delete: index: float * ?length: float -> unit
            /// Transforms this YArray to a JavaScript Array.
            abstract toArray: unit -> Array<U3<YXmlElement, YXmlText, YXmlHook>>
            /// <summary>Appends content to this YArray.</summary>
            /// <param name="content">Array of content to append.</param>
            abstract push: content: Array<U2<YXmlElement, YXmlText>> -> unit
            /// <summary>Preppends content to this YArray.</summary>
            /// <param name="content">Array of content to preppend.</param>
            abstract unshift: content: Array<U2<YXmlElement, YXmlText>> -> unit
            /// <summary>Returns the i-th element from a YArray.</summary>
            /// <param name="index">The index of the element to return from the YArray</param>
            abstract get: index: float -> U2<YXmlElement, YXmlText>
            /// Transforms this YArray to a JavaScript Array.
            abstract slice: ?start: float * ?``end``: float -> Array<U2<YXmlElement, YXmlText>>
            /// <summary>Executes a provided function on once on overy child element.</summary>
            /// <param name="f">A function to execute on every element of this YArray.</param>
            abstract forEach: f: (U2<YXmlElement, YXmlText> -> float -> YXmlFragment -> unit) -> unit

        /// Represents a list of {@link YXmlElement}.and {@link YXmlText} types.
        /// A YxmlFragment is similar to a {@link YXmlElement}, but it does not have a
        /// nodeName and it does not have attributes. Though it can be bound to a DOM
        /// element - in this case the attributes and the nodeName are not shared.
        type [<AllowNullLiteral>] YXmlFragmentStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> YXmlFragment

        type CSS_Selector =
            string

        type [<AllowNullLiteral>] domFilter =
            [<Emit "$0($1...)">] abstract Invoke: nodeName: string * attributes: Map<obj option, obj option> -> bool

        type [<AllowNullLiteral>] YXmlFragmentToDOM =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

    module YXmlHook =
        type YMap<'MapType> = Types.YMap.YMap<'MapType>
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract YXmlHook: YXmlHookStatic
            abstract readYXmlHook: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> YXmlHook

        /// You can manage binding to a custom type with YXmlHook.
        type [<AllowNullLiteral>] YXmlHook =
            inherit YMap<obj option>
            abstract hookName: string with get, set
            /// Creates an Item with the same effect as this Item (without position effect)
            abstract _copy: unit -> YXmlHook
            abstract clone: unit -> YXmlHook
            /// <summary>Creates a Dom Element that mirrors this YXmlElement.</summary>
            /// <param name="_document">The document object (you must define
            ///  this when calling this method in
            ///  nodejs)</param>
            /// <param name="hooks">Optional property to customize how hooks
            ///        are presented in the DOM</param>
            /// <param name="binding">You should not set this property. This is
            ///         used if DomBinding wants to create a
            ///         association to the created DOM type</param>
            abstract toDOM: ?_document: Document * ?hooks: YXmlHookToDOM * ?binding: obj -> Element

        /// You can manage binding to a custom type with YXmlHook.
        type [<AllowNullLiteral>] YXmlHookStatic =
            /// <param name="hookName">nodeName of the Dom Node.</param>
            [<Emit "new $0($1...)">] abstract Create: hookName: string -> YXmlHook

        type [<AllowNullLiteral>] YXmlHookToDOM =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

    module YXmlText =
        type YText = Types.YText.YText
        type YXmlElement = Types.YXmlElement.YXmlElement
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract YXmlText: YXmlTextStatic
            abstract readYXmlText: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> YXmlText

        /// Represents text in a Dom Element. In the future this type will also handle
        /// simple formatting information like bold and italic.
        type [<AllowNullLiteral>] YXmlText =
            inherit YText
    //        obj
    //        obj
            abstract _copy: unit -> YXmlText
            abstract clone: unit -> YXmlText
            /// <summary>Creates a Dom Element that mirrors this YXmlText.</summary>
            /// <param name="_document">The document object (you must define
            ///  this when calling this method in
            ///  nodejs)</param>
            /// <param name="hooks">Optional property to customize how hooks
            ///         are presented in the DOM</param>
            /// <param name="binding">You should not set this property. This is
            ///         used if DomBinding wants to create a
            ///         association to the created DOM type.</param>
            abstract toDOM: ?_document: Document * ?hooks: YXmlTextToDOM * ?binding: obj -> Text
            abstract toString: unit -> string option

        /// Represents text in a Dom Element. In the future this type will also handle
        /// simple formatting information like bold and italic.
        type [<AllowNullLiteral>] YXmlTextStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> YXmlText

        type [<AllowNullLiteral>] YXmlTextToDOM =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

module Utils =
    module AbstractConnector =
        type Observable<'T> = System.IObservable<'T>
        type Doc = Utils.Doc.Doc

        type [<AllowNullLiteral>] IExports =
            abstract AbstractConnector: AbstractConnectorStatic

        /// This is an abstract interface that all Connectors should implement to keep them interchangeable.
        type [<AllowNullLiteral>] AbstractConnector =
            inherit Observable<obj option>
            abstract doc: Doc with get, set
            abstract awareness: obj option with get, set

        /// This is an abstract interface that all Connectors should implement to keep them interchangeable.
        type [<AllowNullLiteral>] AbstractConnectorStatic =
            [<Emit "new $0($1...)">] abstract Create: ydoc: Doc * awareness: obj option -> AbstractConnector

    module DeleteSet =
        type Transaction = Utils.Transaction.Transaction
        type GC = Structs.GC.GC
        type Item = Structs.Item.Item
        type ID = Utils.ID.ID
        type StructStore = Utils.StructStore.StructStore
        type DSEncoderV1 = Utils.UpdateEncoder.DSEncoderV1
        type DSEncoderV2 = Utils.UpdateEncoder.DSEncoderV2
        type DSDecoderV1 = Utils.UpdateDecoder.DSDecoderV1
        type DSDecoderV2 = Utils.UpdateDecoder.DSDecoderV2

        type [<AllowNullLiteral>] IExports =
            abstract DeleteItem: DeleteItemStatic
            abstract DeleteSet: DeleteSetStatic
            abstract iterateDeletedStructs: transaction: Transaction * ds: DeleteSet * f: (U2<GC, Item> -> unit) -> unit
            abstract findIndexDS: dis: Array<DeleteItem> * clock: float -> float option
            abstract isDeleted: ds: DeleteSet * id: ID -> bool
            abstract sortAndMergeDeleteSet: ds: DeleteSet -> unit
            abstract mergeDeleteSets: dss: Array<DeleteSet> -> DeleteSet
            abstract addToDeleteSet: ds: DeleteSet * client: float * clock: float * length: float -> unit
            abstract createDeleteSet: unit -> DeleteSet
            abstract createDeleteSetFromStructStore: ss: StructStore -> DeleteSet
            abstract writeDeleteSet: encoder: U2<DSEncoderV1, DSEncoderV2> * ds: DeleteSet -> unit
            abstract readDeleteSet: decoder: U2<DSDecoderV1, DSDecoderV2> -> DeleteSet
            abstract readAndApplyDeleteSet: decoder: U2<DSDecoderV1, DSDecoderV2> * transaction: Transaction * store: StructStore -> Uint8Array option

        type [<AllowNullLiteral>] DeleteItem =
            abstract clock: float with get, set
            abstract len: float with get, set

        type [<AllowNullLiteral>] DeleteItemStatic =
            [<Emit "new $0($1...)">] abstract Create: clock: float * len: float -> DeleteItem

        /// We no longer maintain a DeleteStore. DeleteSet is a temporary object that is created when needed.
        /// - When created in a transaction, it must only be accessed after sorting, and merging
        ///    - This DeleteSet is send to other clients
        /// - We do not create a DeleteSet when we send a sync message. The DeleteSet message is created directly from StructStore
        /// - We read a DeleteSet as part of a sync/update message. In this case the DeleteSet is already sorted and merged.
        type [<AllowNullLiteral>] DeleteSet =
            abstract clients: Map<float, Array<DeleteItem>> with get, set

        /// We no longer maintain a DeleteStore. DeleteSet is a temporary object that is created when needed.
        /// - When created in a transaction, it must only be accessed after sorting, and merging
        ///    - This DeleteSet is send to other clients
        /// - We do not create a DeleteSet when we send a sync message. The DeleteSet message is created directly from StructStore
        /// - We read a DeleteSet as part of a sync/update message. In this case the DeleteSet is already sorted and merged.
        type [<AllowNullLiteral>] DeleteSetStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> DeleteSet

    module Doc =
        type Observable<'T> = System.IObservable<'T>
        type Item = Structs.Item.Item
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type YEvent<'T, 'E> = Utils.YEvent.YEvent<'T, 'E>
        type StructStore = Utils.StructStore.StructStore
        type Transaction = Utils.Transaction.Transaction
        type YArray<'T> = Types.YArray.YArray<'T>
        type YText = Types.YText.YText
        type YMap<'MapType> = Types.YMap.YMap<'MapType>
        type YXmlFragment = Types.YXmlFragment.YXmlFragment

        type [<AllowNullLiteral>] IExports =
            abstract generateNewClientId: obj
            abstract Doc: DocStatic

        /// A Yjs instance handles the state of shared data.
        type [<AllowNullLiteral>] Doc =
            inherit Observable<string>
            abstract gc: bool with get, set
            abstract gcFilter: (Item -> bool) with get, set
            abstract clientID: float with get, set
            abstract guid: string with get, set
            abstract collectionid: string option with get, set
            abstract share: Map<string, AbstractType<YEvent<obj option, obj>>> with get, set
            abstract store: StructStore with get, set
            abstract _transaction: Transaction option with get, set
            abstract _transactionCleanups: Array<Transaction> with get, set
            abstract subdocs: Set<Doc> with get, set
            /// If this document is a subdocument - a document integrated into another document - then _item is defined.
            abstract _item: Item option with get, set
            abstract shouldLoad: bool with get, set
            abstract autoLoad: bool with get, set
            abstract meta: obj option with get, set
            abstract isLoaded: bool with get, set
            abstract whenLoaded: Promise<obj option> with get, set
            /// Notify the parent document that you request to load data into this subdocument (if it is a subdocument).
            /// 
            /// `load()` might be used in the future to request any provider to load the most current data.
            /// 
            /// It is safe to call `load()` multiple times.
            abstract load: unit -> unit
            abstract getSubdocs: unit -> Set<Doc>
            abstract getSubdocGuids: unit -> Set<string>
            /// <summary>Changes that happen inside of a transaction are bundled. This means that
            /// the observer fires _after_ the transaction is finished and that all changes
            /// that happened inside of the transaction are sent as one message to the
            /// other peers.</summary>
            /// <param name="f">The function that should be executed as a transaction</param>
            /// <param name="origin">Origin of who started the transaction. Will be stored on transaction.origin</param>
            abstract transact: f: (Transaction -> unit) * ?origin: obj -> unit
            /// <summary>Define a shared data type.
            /// 
            /// Multiple calls of `y.get(name, TypeConstructor)` yield the same result
            /// and do not overwrite each other. I.e.
            /// `y.define(name, Array) === define(name, Array)`
            /// 
            /// After this method is called, the type is also available on `y.share.get(name)`.
            /// 
            /// *Best Practices:*
            /// Define all types right after the Yjs instance is created and store them in a separate object.
            /// Also use the typed methods `getText(name)`, `getArray(name)`, ..</summary>
            /// <param name="TypeConstructor">The constructor of the type definition. E.g. Text, Array, Map, ...</param>
            abstract get: name: string * ?TypeConstructor: Function -> AbstractType<obj option>
            abstract getArray: ?name: string -> YArray<'T>
            abstract getText: ?name: string -> YText
            abstract getMap: ?name: string -> YMap<'T_1>
            abstract getXmlFragment: ?name: string -> YXmlFragment
            /// Converts the entire document into a js object, recursively traversing each yjs type
            /// Doesn't log types that have not been defined (using ydoc.getType(..)).
            abstract toJSON: unit -> DocToJSONReturn
            abstract on: eventName: string * f: (ResizeArray<obj option> -> obj option) -> unit

        type [<AllowNullLiteral>] DocToJSONReturn =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

        /// A Yjs instance handles the state of shared data.
        type [<AllowNullLiteral>] DocStatic =
            /// <param name="p0">configuration</param>
            [<Emit "new $0($1...)">] abstract Create: ?p0: DocOpts -> Doc

        type [<AllowNullLiteral>] DocOpts =
            /// Disable garbage collection (default: gc=true)
            abstract gc: bool option with get, set
            /// Will be called before an Item is garbage collected. Return false to keep the Item.
            abstract gcFilter: (Item -> bool) option with get, set
            /// Define a globally unique identifier for this document
            abstract guid: string option with get, set
            /// Associate this document with a collection. This only plays a role if your provider has a concept of collection.
            abstract collectionid: string option with get, set
            /// Any kind of meta information you want to associate with this document. If this is a subdocument, remote peers will store the meta information as well.
            abstract meta: obj option with get, set
            /// If a subdocument, automatically load document. If this is a subdocument, remote peers will load the document as well automatically.
            abstract autoLoad: bool option with get, set
            /// Whether the document should be synced by the provider now. This is toggled to true when you call ydoc.load()
            abstract shouldLoad: bool option with get, set

    module encoding =
        module Decoding = Lib0.Decoding
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type StructStore = Utils.StructStore.StructStore
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2
        type Doc = Utils.Doc.Doc
        type Item = Structs.Item.Item
        type GC = Structs.GC.GC
        type Transaction = Utils.Transaction.Transaction
        type DSDecoderV1 = Utils.UpdateDecoder.DSDecoderV1
        type DSDecoderV2 = Utils.UpdateDecoder.DSDecoderV2
        type DSEncoderV1 = Utils.UpdateEncoder.DSEncoderV1
        type DSEncoderV2 = Utils.UpdateEncoder.DSEncoderV2

        type [<AllowNullLiteral>] IExports =
            abstract writeClientsStructs: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * store: StructStore * _sm: Map<float, float> -> unit
            abstract readClientsStructRefs: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> * doc: Doc -> Map<float, IExportsReadClientsStructRefsMap>
            abstract writeStructsFromTransaction: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * transaction: Transaction -> unit
            abstract readUpdateV2: decoder: Decoding.Decoder * ydoc: Doc * ?transactionOrigin: obj * ?structDecoder: U2<UpdateDecoderV1, UpdateDecoderV2> -> unit
            abstract readUpdate: decoder: Decoding.Decoder * ydoc: Doc * ?transactionOrigin: obj -> unit
            abstract applyUpdateV2: ydoc: Doc * update: Uint8Array * ?transactionOrigin: obj * ?YDecoder: obj -> unit
            abstract applyUpdate: ydoc: Doc * update: Uint8Array * ?transactionOrigin: obj -> unit
            abstract writeStateAsUpdate: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * doc: Doc * ?targetStateVector: Map<float, float> -> unit
            abstract encodeStateAsUpdateV2: doc: Doc * ?encodedTargetStateVector: Uint8Array * ?encoder: U2<UpdateEncoderV2, UpdateEncoderV1> -> Uint8Array
            abstract encodeStateAsUpdate: doc: Doc * ?encodedTargetStateVector: Uint8Array -> Uint8Array
            abstract readStateVector: decoder: U2<DSDecoderV1, DSDecoderV2> -> Map<float, float>
            abstract decodeStateVector: decodedState: Uint8Array -> Map<float, float>
            abstract writeStateVector: encoder: U2<DSEncoderV1, DSEncoderV2> * sv: Map<float, float> -> U2<DSEncoderV1, DSEncoderV2>
            abstract writeDocumentStateVector: encoder: U2<DSEncoderV1, DSEncoderV2> * doc: Doc -> U2<DSEncoderV1, DSEncoderV2>
            abstract encodeStateVectorV2: doc: U2<Doc, Map<float, float>> * ?encoder: U2<DSEncoderV1, DSEncoderV2> -> Uint8Array
            abstract encodeStateVector: doc: U2<Doc, Map<float, float>> -> Uint8Array

        type [<AllowNullLiteral>] IExportsReadClientsStructRefsMap =
            abstract i: float with get, set
            abstract refs: Array<U2<Item, GC>> with get, set

    module EventHandler =

        type [<AllowNullLiteral>] IExports =
            abstract EventHandler: EventHandlerStatic
            abstract createEventHandler: unit -> EventHandler<'ARG0, 'ARG1>
            abstract addEventHandlerListener: eventHandler: EventHandler<'ARG0, 'ARG1> * f: ('ARG0 -> 'ARG1 -> unit) -> float
            abstract removeEventHandlerListener: eventHandler: EventHandler<'ARG0, 'ARG1> * f: ('ARG0 -> 'ARG1 -> unit) -> unit
            abstract removeAllEventHandlerListeners: eventHandler: EventHandler<'ARG0, 'ARG1> -> unit
            abstract callEventHandlerListeners: eventHandler: EventHandler<'ARG0, 'ARG1> * arg0: 'ARG0 * arg1: 'ARG1 -> unit

        /// General event handler implementation.
        type [<AllowNullLiteral>] EventHandler<'ARG0, 'ARG1> =
            abstract l: ResizeArray<('ARG0 -> 'ARG1 -> unit)> with get, set

        /// General event handler implementation.
        type [<AllowNullLiteral>] EventHandlerStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> EventHandler<'ARG0, 'ARG1>

    module ID =
        module Encoding = Lib0.Encoding
        module Decoding = Lib0.Decoding
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>

        type [<AllowNullLiteral>] IExports =
            abstract ID: IDStatic
            abstract compareIDs: a: ID option * b: ID option -> bool
            abstract createID: client: float * clock: float -> ID
            abstract writeID: encoder: Encoding.Encoder * id: ID -> unit
            abstract readID: decoder: Decoding.Decoder -> ID
            abstract findRootTypeKey: ``type``: AbstractType<obj option> -> string

        type [<AllowNullLiteral>] ID =
            /// Client id
            abstract client: float with get, set
            /// unique per client id, continuous number
            abstract clock: float with get, set

        type [<AllowNullLiteral>] IDStatic =
            /// <param name="client">client id</param>
            /// <param name="clock">unique per client id, continuous number</param>
            [<Emit "new $0($1...)">] abstract Create: client: float * clock: float -> ID

    module isParentOf =
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Item = Structs.Item.Item

        type [<AllowNullLiteral>] IExports =
            abstract isParentOf: parent: AbstractType<obj option> * child: Item option -> bool

    module logging =
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>

        type [<AllowNullLiteral>] IExports =
            abstract logType: ``type``: AbstractType<obj option> -> unit

    module PermanentUserData =
        type YMap<'MapType> = Types.YMap.YMap<'MapType>
        type Doc = Utils.Doc.Doc
        type DeleteSet = Utils.DeleteSet.DeleteSet
        type Transaction = Utils.Transaction.Transaction
        type ID = Utils.ID.ID

        type [<AllowNullLiteral>] IExports =
            abstract PermanentUserData: PermanentUserDataStatic

        type [<AllowNullLiteral>] PermanentUserData =
            abstract yusers: YMap<obj option> with get, set
            abstract doc: Doc with get, set
            /// Maps from clientid to userDescription
            abstract clients: Map<float, string> with get, set
            abstract dss: Map<string, DeleteSet> with get, set
            abstract setUserMapping: doc: Doc * clientid: float * userDescription: string * ?p3: PermanentUserDataSetUserMapping -> unit
            abstract getUserByClientId: clientid: float -> obj option
            abstract getUserByDeletedId: id: ID -> string option

        type [<AllowNullLiteral>] PermanentUserDataStatic =
            [<Emit "new $0($1...)">] abstract Create: doc: Doc * ?storeType: YMap<obj option> -> PermanentUserData

        type [<AllowNullLiteral>] PermanentUserDataSetUserMapping =
            abstract filter: (Transaction -> DeleteSet -> bool) option with get, set

    module RelativePosition =
        module Encoding = Lib0.Encoding
        module Decoding = Lib0.Decoding
        type ID = Utils.ID.ID
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Doc = Utils.Doc.Doc

        type [<AllowNullLiteral>] IExports =
            abstract RelativePosition: RelativePositionStatic
            abstract relativePositionToJSON: rpos: RelativePosition -> obj option
            abstract createRelativePositionFromJSON: json: obj option -> RelativePosition
            abstract AbsolutePosition: AbsolutePositionStatic
            abstract createAbsolutePosition: ``type``: AbstractType<obj option> * index: float * ?assoc: float -> AbsolutePosition
            abstract createRelativePosition: ``type``: AbstractType<obj option> * item: ID option * ?assoc: float -> RelativePosition
            abstract createRelativePositionFromTypeIndex: ``type``: AbstractType<obj option> * index: float * ?assoc: float -> RelativePosition
            abstract writeRelativePosition: encoder: Encoding.Encoder * rpos: RelativePosition -> Encoding.Encoder
            abstract encodeRelativePosition: rpos: RelativePosition -> Uint8Array
            abstract readRelativePosition: decoder: Decoding.Decoder -> RelativePosition
            abstract decodeRelativePosition: uint8Array: Uint8Array -> RelativePosition
            abstract createAbsolutePositionFromRelativePosition: rpos: RelativePosition * doc: Doc -> AbsolutePosition option
            abstract compareRelativePositions: a: RelativePosition option * b: RelativePosition option -> bool

        /// A relative position is based on the Yjs model and is not affected by document changes.
        /// E.g. If you place a relative position before a certain character, it will always point to this character.
        /// If you place a relative position at the end of a type, it will always point to the end of the type.
        /// 
        /// A numeric position is often unsuited for user selections, because it does not change when content is inserted
        /// before or after.
        /// 
        /// ```Insert(0, 'x')('a|bc') = 'xa|bc'``` Where | is the relative position.
        /// 
        /// One of the properties must be defined.
        type [<AllowNullLiteral>] RelativePosition =
            abstract ``type``: ID option with get, set
            abstract tname: string option with get, set
            abstract item: ID option with get, set
            /// A relative position is associated to a specific character. By default
            /// assoc >= 0, the relative position is associated to the character
            /// after the meant position.
            /// I.e. position 1 in 'ab' is associated to character 'b'.
            /// 
            /// If assoc < 0, then the relative position is associated to the caharacter
            /// before the meant position.
            abstract assoc: float with get, set

        /// A relative position is based on the Yjs model and is not affected by document changes.
        /// E.g. If you place a relative position before a certain character, it will always point to this character.
        /// If you place a relative position at the end of a type, it will always point to the end of the type.
        /// 
        /// A numeric position is often unsuited for user selections, because it does not change when content is inserted
        /// before or after.
        /// 
        /// ```Insert(0, 'x')('a|bc') = 'xa|bc'``` Where | is the relative position.
        /// 
        /// One of the properties must be defined.
        type [<AllowNullLiteral>] RelativePositionStatic =
            [<Emit "new $0($1...)">] abstract Create: ``type``: ID option * tname: string option * item: ID option * ?assoc: float -> RelativePosition

        type [<AllowNullLiteral>] AbsolutePosition =
            abstract ``type``: AbstractType<obj option> with get, set
            abstract index: float with get, set
            abstract assoc: float with get, set

        type [<AllowNullLiteral>] AbsolutePositionStatic =
            [<Emit "new $0($1...)">] abstract Create: ``type``: AbstractType<obj option> * index: float * ?assoc: float -> AbsolutePosition

    module Snapshot =
        type DeleteSet = Utils.DeleteSet.DeleteSet
        type DSEncoderV1 = Utils.UpdateEncoder.DSEncoderV1
        type DSEncoderV2 = Utils.UpdateEncoder.DSEncoderV2
        type DSDecoderV1 = Utils.UpdateDecoder.DSDecoderV1
        type DSDecoderV2 = Utils.UpdateDecoder.DSDecoderV2
        type Doc = Utils.Doc.Doc
        type Item = Structs.Item.Item
        type Transaction = Utils.Transaction.Transaction

        type [<AllowNullLiteral>] IExports =
            abstract Snapshot: SnapshotStatic
            abstract equalSnapshots: snap1: Snapshot * snap2: Snapshot -> bool
            abstract encodeSnapshotV2: snapshot: Snapshot * ?encoder: U2<DSEncoderV1, DSEncoderV2> -> Uint8Array
            abstract encodeSnapshot: snapshot: Snapshot -> Uint8Array
            abstract decodeSnapshotV2: buf: Uint8Array * ?decoder: U2<DSDecoderV1, DSDecoderV2> -> Snapshot
            abstract decodeSnapshot: buf: Uint8Array -> Snapshot
            abstract createSnapshot: ds: DeleteSet * sm: Map<float, float> -> Snapshot
            abstract emptySnapshot: Snapshot
            abstract snapshot: doc: Doc -> Snapshot
            abstract isVisible: item: Item * snapshot: Snapshot option -> bool
            abstract splitSnapshotAffectedStructs: transaction: Transaction * snapshot: Snapshot -> unit
            abstract createDocFromSnapshot: originDoc: Doc * snapshot: Snapshot * ?newDoc: Doc -> Doc

        type [<AllowNullLiteral>] Snapshot =
            abstract ds: DeleteSet with get, set
            /// State Map
            abstract sv: Map<float, float> with get, set

        type [<AllowNullLiteral>] SnapshotStatic =
            /// <param name="sv">state map</param>
            [<Emit "new $0($1...)">] abstract Create: ds: DeleteSet * sv: Map<float, float> -> Snapshot

    module StructStore =
        type GC = Structs.GC.GC
        type Item = Structs.Item.Item
        type ID = Utils.ID.ID
        type Transaction = Utils.Transaction.Transaction

        type [<AllowNullLiteral>] IExports =
            abstract StructStore: StructStoreStatic
            abstract getStateVector: store: StructStore -> Map<float, float>
            abstract getState: store: StructStore * client: float -> float
            abstract integretyCheck: store: StructStore -> unit
            abstract addStruct: store: StructStore * ``struct``: U2<GC, Item> -> unit
            abstract findIndexSS: structs: Array<U2<Item, GC>> * clock: float -> float
            abstract find: store: StructStore * id: ID -> U2<GC, Item>
            abstract getItem: arg0: StructStore * arg1: ID -> Item
            abstract findIndexCleanStart: transaction: Transaction * structs: Array<U2<Item, GC>> * clock: float -> float
            abstract getItemCleanStart: transaction: Transaction * id: ID -> Item
            abstract getItemCleanEnd: transaction: Transaction * store: StructStore * id: ID -> Item
            abstract replaceStruct: store: StructStore * ``struct``: U2<GC, Item> * newStruct: U2<GC, Item> -> unit
            abstract iterateStructs: transaction: Transaction * structs: Array<U2<Item, GC>> * clockStart: float * len: float * f: (U2<GC, Item> -> unit) -> unit

        type [<AllowNullLiteral>] StructStore =
            abstract clients: Map<float, Array<U2<GC, Item>>> with get, set
            abstract pendingStructs: StructStorePendingStructs option with get, set
            abstract pendingDs: Uint8Array option with get, set

        type [<AllowNullLiteral>] StructStoreStatic =
            [<Emit "new $0($1...)">] abstract Create: unit -> StructStore

        type [<AllowNullLiteral>] StructStorePendingStructs =
            abstract missing: Map<float, float> with get, set
            abstract update: Uint8Array with get, set

    module Transaction =
        type Doc = Utils.Doc.Doc
        type DeleteSet = Utils.DeleteSet.DeleteSet
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type YEvent<'T, 'E> = Utils.YEvent.YEvent<'T, 'E>
        type AbstractStruct = Structs.AbstractStruct.AbstractStruct
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type StructStore = Utils.StructStore.StructStore
        type Item = Structs.Item.Item

        type [<AllowNullLiteral>] IExports =
            abstract Transaction: TransactionStatic
            abstract writeUpdateMessageFromTransaction: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> * transaction: Transaction -> bool
            abstract nextID: transaction: Transaction -> obj
            abstract addChangedTypeToTransaction: transaction: Transaction * ``type``: AbstractType<YEvent<obj option, obj>> * parentSub: string option -> unit
            abstract tryGc: ds: DeleteSet * store: StructStore * gcFilter: (Item -> bool) -> unit
            abstract transact: doc: Doc * f: (Transaction -> unit) * ?origin: obj * ?local: bool -> unit

        /// A transaction is created for every change on the Yjs model. It is possible
        /// to bundle changes on the Yjs model in a single transaction to
        /// minimize the number on messages sent and the number of observer calls.
        /// If possible the user of this library should bundle as many changes as
        /// possible. Here is an example to illustrate the advantages of bundling:
        type [<AllowNullLiteral>] Transaction =
            /// The Yjs instance.
            abstract doc: Doc with get, set
            /// Describes the set of deleted items by ids
            abstract deleteSet: DeleteSet with get, set
            /// Holds the state before the transaction started.
            abstract beforeState: Map<float, float> with get, set
            /// Holds the state after the transaction.
            abstract afterState: Map<float, float> with get, set
            /// All types that were directly modified (property added or child
            /// inserted/deleted). New types are not included in this Set.
            /// Maps from type to parentSubs (`item.parentSub = null` for YArray)
            abstract changed: Map<AbstractType<YEvent<obj option, obj>>, Set<string option>> with get, set
            /// Stores the events for the types that observe also child elements.
            /// It is mainly used by `observeDeep`.
            abstract changedParentTypes: Map<AbstractType<YEvent<obj option, obj>>, Array<YEvent<obj option, obj>>> with get, set
            abstract _mergeStructs: Array<AbstractStruct> with get, set
            abstract origin: obj option with get, set
            /// Stores meta information on the transaction
            abstract meta: Map<string, obj> with get, set
            /// Whether this change originates from this doc.
            abstract local: bool with get, set
            abstract subdocsAdded: Set<Doc> with get, set
            abstract subdocsRemoved: Set<Doc> with get, set
            abstract subdocsLoaded: Set<Doc> with get, set

        /// A transaction is created for every change on the Yjs model. It is possible
        /// to bundle changes on the Yjs model in a single transaction to
        /// minimize the number on messages sent and the number of observer calls.
        /// If possible the user of this library should bundle as many changes as
        /// possible. Here is an example to illustrate the advantages of bundling:
        type [<AllowNullLiteral>] TransactionStatic =
            [<Emit "new $0($1...)">] abstract Create: doc: Doc * origin: obj option * local: bool -> Transaction

    module UndoManager =
        type Observable<'T> = System.IObservable<'T>
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Item = Structs.Item.Item
        type Doc = Utils.Doc.Doc
        type Transaction = Utils.Transaction.Transaction
        type DeleteSet = Utils.DeleteSet.DeleteSet

        type [<AllowNullLiteral>] IExports =
            abstract UndoManager: UndoManagerStatic
            abstract StackItem: StackItemStatic

        /// Fires 'stack-item-added' event when a stack item was added to either the undo- or
        /// the redo-stack. You may store additional stack information via the
        /// metadata property on `event.stackItem.meta` (it is a `Map` of metadata properties).
        /// Fires 'stack-item-popped' event when a stack item was popped from either the
        /// undo- or the redo-stack. You may restore the saved stack information from `event.stackItem.meta`.
        type [<AllowNullLiteral>] UndoManager =
            inherit Observable<UndoManagerObservable>
            abstract scope: Array<AbstractType<obj option>> with get, set
            abstract deleteFilter: (Item -> bool) with get, set
            abstract trackedOrigins: Set<obj option> with get, set
            abstract undoStack: Array<StackItem> with get, set
            abstract redoStack: Array<StackItem> with get, set
            /// Whether the client is currently undoing (calling UndoManager.undo)
            abstract undoing: bool with get, set
            abstract redoing: bool with get, set
            abstract doc: Doc with get, set
            abstract lastChange: float with get, set
            abstract ignoreRemoteMapChanges: bool with get, set
            abstract afterTransactionHandler: (Transaction -> unit) with get, set
            abstract addToScope: ytypes: U2<Array<AbstractType<obj option>>, AbstractType<obj option>> -> unit
            abstract addTrackedOrigin: origin: obj option -> unit
            abstract removeTrackedOrigin: origin: obj option -> unit
            abstract clear: ?clearUndoStack: bool * ?clearRedoStack: bool -> unit
            /// UndoManager merges Undo-StackItem if they are created within time-gap
            /// smaller than `options.captureTimeout`. Call `um.stopCapturing()` so that the next
            /// StackItem won't be merged.
            abstract stopCapturing: unit -> unit
            /// Undo last changes on type.
            abstract undo: unit -> StackItem option
            /// Redo last undo operation.
            abstract redo: unit -> StackItem option
            /// Are undo steps available?
            abstract canUndo: unit -> bool
            /// Are redo steps available?
            abstract canRedo: unit -> bool

        /// Fires 'stack-item-added' event when a stack item was added to either the undo- or
        /// the redo-stack. You may store additional stack information via the
        /// metadata property on `event.stackItem.meta` (it is a `Map` of metadata properties).
        /// Fires 'stack-item-popped' event when a stack item was popped from either the
        /// undo- or the redo-stack. You may restore the saved stack information from `event.stackItem.meta`.
        type [<AllowNullLiteral>] UndoManagerStatic =
            /// <param name="typeScope">Accepts either a single type, or an array of types</param>
            [<Emit "new $0($1...)">] abstract Create: typeScope: U2<AbstractType<obj option>, Array<AbstractType<obj option>>> * ?p1: UndoManagerOptions -> UndoManager

        type [<AllowNullLiteral>] UndoManagerOptions =
            abstract captureTimeout: float option with get, set
            /// Sometimes
            /// it is necessary to filter whan an Undo/Redo operation can delete. If this
            /// filter returns false, the type/item won't be deleted even it is in the
            /// undo/redo scope.
            abstract deleteFilter: (Item -> bool) option with get, set
            abstract trackedOrigins: Set<obj option> option with get, set
            /// Experimental. By default, the UndoManager will never overwrite remote changes. Enable this property to enable overwriting remote changes on key-value changes (Map, properties on Xml, etc..).
            abstract ignoreRemoteMapChanges: bool option with get, set

        type [<AllowNullLiteral>] StackItem =
            abstract insertions: DeleteSet with get, set
            abstract deletions: DeleteSet with get, set
            /// Use this to save and restore metadata like selection range
            abstract meta: Map<obj option, obj option> with get, set

        type [<AllowNullLiteral>] StackItemStatic =
            [<Emit "new $0($1...)">] abstract Create: deletions: DeleteSet * insertions: DeleteSet -> StackItem

        type [<StringEnum>] [<RequireQualifiedAccess>] UndoManagerObservable =
            | [<CompiledName "stack-item-added">] StackItemAdded
            | [<CompiledName "stack-item-popped">] StackItemPopped
            | [<CompiledName "stack-cleared">] StackCleared
            | [<CompiledName "stack-item-updated">] StackItemUpdated

    module UpdateDecoder =
        module Decoding = Lib0.Decoding
        type ID = Utils.ID.ID

        type [<AllowNullLiteral>] IExports =
            abstract DSDecoderV1: DSDecoderV1Static
            abstract UpdateDecoderV1: UpdateDecoderV1Static
            abstract DSDecoderV2: DSDecoderV2Static
            abstract UpdateDecoderV2: UpdateDecoderV2Static

        type [<AllowNullLiteral>] DSDecoderV1 =
            abstract restDecoder: Decoding.Decoder with get, set
            abstract resetDsCurVal: unit -> unit
            abstract readDsClock: unit -> float
            abstract readDsLen: unit -> float

        type [<AllowNullLiteral>] DSDecoderV1Static =
            [<Emit "new $0($1...)">] abstract Create: decoder: Decoding.Decoder -> DSDecoderV1

        type [<AllowNullLiteral>] UpdateDecoderV1 =
            inherit DSDecoderV1
            abstract readLeftID: unit -> ID
            abstract readRightID: unit -> ID
            /// Read the next client id.
            /// Use this in favor of readID whenever possible to reduce the number of objects created.
            abstract readClient: unit -> float
            abstract readInfo: unit -> float
            abstract readString: unit -> string
            abstract readParentInfo: unit -> bool
            abstract readTypeRef: unit -> float
            /// Write len of a struct - well suited for Opt RLE encoder.
            abstract readLen: unit -> float
            abstract readAny: unit -> obj option
            abstract readBuf: unit -> Uint8Array
            /// Legacy implementation uses JSON parse. We use any-decoding in v2.
            abstract readJSON: unit -> obj option
            abstract readKey: unit -> string

        type [<AllowNullLiteral>] UpdateDecoderV1Static =
            [<Emit "new $0($1...)">] abstract Create: unit -> UpdateDecoderV1

        type [<AllowNullLiteral>] DSDecoderV2 =
            abstract restDecoder: Decoding.Decoder with get, set
            abstract resetDsCurVal: unit -> unit
            abstract readDsClock: unit -> float
            abstract readDsLen: unit -> float

        type [<AllowNullLiteral>] DSDecoderV2Static =
            [<Emit "new $0($1...)">] abstract Create: decoder: Decoding.Decoder -> DSDecoderV2

        type [<AllowNullLiteral>] UpdateDecoderV2 =
            inherit DSDecoderV2
            /// List of cached keys. If the keys[id] does not exist, we read a new key
            /// from stringEncoder and push it to keys.
            abstract keys: Array<string> with get, set
            abstract keyClockDecoder: Decoding.IntDiffOptRleDecoder with get, set
            abstract clientDecoder: Decoding.UintOptRleDecoder with get, set
            abstract leftClockDecoder: Decoding.IntDiffOptRleDecoder with get, set
            abstract rightClockDecoder: Decoding.IntDiffOptRleDecoder with get, set
            abstract infoDecoder: Decoding.RleDecoder<float> with get, set
            abstract stringDecoder: Decoding.StringDecoder with get, set
            abstract parentInfoDecoder: Decoding.RleDecoder<float> with get, set
            abstract typeRefDecoder: Decoding.UintOptRleDecoder with get, set
            abstract lenDecoder: Decoding.UintOptRleDecoder with get, set
            abstract readLeftID: unit -> ID
            abstract readRightID: unit -> ID
            /// Read the next client id.
            /// Use this in favor of readID whenever possible to reduce the number of objects created.
            abstract readClient: unit -> float
            abstract readInfo: unit -> float
            abstract readString: unit -> string
            abstract readParentInfo: unit -> bool
            abstract readTypeRef: unit -> float
            /// Write len of a struct - well suited for Opt RLE encoder.
            abstract readLen: unit -> float
            abstract readAny: unit -> obj option
            abstract readBuf: unit -> Uint8Array
            /// This is mainly here for legacy purposes.
            /// 
            /// Initial we incoded objects using JSON. Now we use the much faster lib0/any-encoder. This method mainly exists for legacy purposes for the v1 encoder.
            abstract readJSON: unit -> obj option
            abstract readKey: unit -> string

        type [<AllowNullLiteral>] UpdateDecoderV2Static =
            [<Emit "new $0($1...)">] abstract Create: unit -> UpdateDecoderV2

    module UpdateEncoder =
        module Encoding = Lib0.Encoding
        type ID = Utils.ID.ID

        type [<AllowNullLiteral>] IExports =
            abstract DSEncoderV1: DSEncoderV1Static
            abstract UpdateEncoderV1: UpdateEncoderV1Static
            abstract DSEncoderV2: DSEncoderV2Static
            abstract UpdateEncoderV2: UpdateEncoderV2Static

        type [<AllowNullLiteral>] DSEncoderV1 =
            abstract restEncoder: Encoding.Encoder with get, set
            abstract toUint8Array: unit -> Uint8Array
            abstract resetDsCurVal: unit -> unit
            abstract writeDsClock: clock: float -> unit
            abstract writeDsLen: len: float -> unit

        type [<AllowNullLiteral>] DSEncoderV1Static =
            [<Emit "new $0($1...)">] abstract Create: unit -> DSEncoderV1

        type [<AllowNullLiteral>] UpdateEncoderV1 =
            inherit DSEncoderV1
            abstract writeLeftID: id: ID -> unit
            abstract writeRightID: id: ID -> unit
            /// Use writeClient and writeClock instead of writeID if possible.
            abstract writeClient: client: float -> unit
            /// <param name="info">An unsigned 8-bit integer</param>
            abstract writeInfo: info: float -> unit
            abstract writeString: s: string -> unit
            abstract writeParentInfo: isYKey: bool -> unit
            /// <param name="info">An unsigned 8-bit integer</param>
            abstract writeTypeRef: info: float -> unit
            /// Write len of a struct - well suited for Opt RLE encoder.
            abstract writeLen: len: float -> unit
            abstract writeAny: any: obj option -> unit
            abstract writeBuf: buf: Uint8Array -> unit
            abstract writeJSON: embed: obj option -> unit
            abstract writeKey: key: string -> unit

        type [<AllowNullLiteral>] UpdateEncoderV1Static =
            [<Emit "new $0($1...)">] abstract Create: unit -> UpdateEncoderV1

        type [<AllowNullLiteral>] DSEncoderV2 =
            abstract restEncoder: Encoding.Encoder with get, set
            abstract dsCurrVal: float with get, set
            abstract toUint8Array: unit -> Uint8Array
            abstract resetDsCurVal: unit -> unit
            abstract writeDsClock: clock: float -> unit
            abstract writeDsLen: len: float -> unit

        type [<AllowNullLiteral>] DSEncoderV2Static =
            [<Emit "new $0($1...)">] abstract Create: unit -> DSEncoderV2

        type [<AllowNullLiteral>] UpdateEncoderV2 =
            inherit DSEncoderV2
            abstract keyMap: Map<string, float> with get, set
            /// Refers to the next uniqe key-identifier to me used.
            /// See writeKey method for more information.
            abstract keyClock: float with get, set
            abstract keyClockEncoder: Encoding.IntDiffOptRleEncoder with get, set
            abstract clientEncoder: Encoding.UintOptRleEncoder with get, set
            abstract leftClockEncoder: Encoding.IntDiffOptRleEncoder with get, set
            abstract rightClockEncoder: Encoding.IntDiffOptRleEncoder with get, set
            abstract infoEncoder: Encoding.RleEncoder<float> with get, set
            abstract stringEncoder: Encoding.StringEncoder with get, set
            abstract parentInfoEncoder: Encoding.RleEncoder<float> with get, set
            abstract typeRefEncoder: Encoding.UintOptRleEncoder with get, set
            abstract lenEncoder: Encoding.UintOptRleEncoder with get, set
            abstract writeLeftID: id: ID -> unit
            abstract writeRightID: id: ID -> unit
            abstract writeClient: client: float -> unit
            /// <param name="info">An unsigned 8-bit integer</param>
            abstract writeInfo: info: float -> unit
            abstract writeString: s: string -> unit
            abstract writeParentInfo: isYKey: bool -> unit
            /// <param name="info">An unsigned 8-bit integer</param>
            abstract writeTypeRef: info: float -> unit
            /// Write len of a struct - well suited for Opt RLE encoder.
            abstract writeLen: len: float -> unit
            abstract writeAny: any: obj option -> unit
            abstract writeBuf: buf: Uint8Array -> unit
            /// This is mainly here for legacy purposes.
            /// 
            /// Initial we incoded objects using JSON. Now we use the much faster lib0/any-encoder. This method mainly exists for legacy purposes for the v1 encoder.
            abstract writeJSON: embed: obj option -> unit
            /// Property keys are often reused. For example, in y-prosemirror the key `bold` might
            /// occur very often. For a 3d application, the key `position` might occur very often.
            /// 
            /// We cache these keys in a Map and refer to them via a unique number.
            abstract writeKey: key: string -> unit

        type [<AllowNullLiteral>] UpdateEncoderV2Static =
            [<Emit "new $0($1...)">] abstract Create: unit -> UpdateEncoderV2

    module updates =
        type GC = Structs.GC.GC
        type Item = Structs.Item.Item
        type Skip = Structs.Skip.Skip
        type UpdateDecoderV1 = Utils.UpdateDecoder.UpdateDecoderV1
        type UpdateDecoderV2 = Utils.UpdateDecoder.UpdateDecoderV2
        type UpdateEncoderV2 = Utils.UpdateEncoder.UpdateEncoderV2
        type UpdateEncoderV1 = Utils.UpdateEncoder.UpdateEncoderV1
        type DSEncoderV1 = Utils.UpdateEncoder.DSEncoderV1
        type DSEncoderV2 = Utils.UpdateEncoder.DSEncoderV2

        type [<AllowNullLiteral>] IExports =
            abstract LazyStructReader: LazyStructReaderStatic
            abstract logUpdate: update: Uint8Array -> unit
            abstract logUpdateV2: update: Uint8Array * ?YDecoder: obj -> unit
            abstract decodeUpdate: update: Uint8Array -> DecodeUpdateReturn
            abstract decodeUpdateV2: update: Uint8Array * ?YDecoder: obj -> DecodeUpdateV2Return
            abstract LazyStructWriter: LazyStructWriterStatic
            abstract mergeUpdates: updates: Array<Uint8Array> -> Uint8Array
            abstract encodeStateVectorFromUpdateV2: update: Uint8Array * ?YEncoder: obj * ?YDecoder: obj -> Uint8Array
            abstract encodeStateVectorFromUpdate: update: Uint8Array -> Uint8Array
            abstract parseUpdateMetaV2: update: Uint8Array * ?YDecoder: obj -> ParseUpdateMetaV2Return
            abstract parseUpdateMeta: update: Uint8Array -> ParseUpdateMetaReturn
            abstract mergeUpdatesV2: updates: Array<Uint8Array> * ?YDecoder: obj * ?YEncoder: obj -> Uint8Array
            abstract diffUpdateV2: update: Uint8Array * sv: Uint8Array * ?YDecoder: obj * ?YEncoder: obj -> Uint8Array
            abstract diffUpdate: update: Uint8Array * sv: Uint8Array -> Uint8Array
            abstract convertUpdateFormat: update: Uint8Array * YDecoder: obj * YEncoder: obj -> Uint8Array
            abstract convertUpdateFormatV1ToV2: update: Uint8Array -> Uint8Array
            abstract convertUpdateFormatV2ToV1: update: Uint8Array -> Uint8Array

        type [<AllowNullLiteral>] DecodeUpdateReturn =
            abstract structs: ResizeArray<U3<GC, Item, Skip>> with get, set
            abstract ds: obj with get, set

        type [<AllowNullLiteral>] DecodeUpdateV2Return =
            abstract structs: ResizeArray<U3<GC, Item, Skip>> with get, set
            abstract ds: obj with get, set

        type [<AllowNullLiteral>] ParseUpdateMetaV2Return =
            abstract from: Map<float, float> with get, set
            abstract ``to``: Map<float, float> with get, set

        type [<AllowNullLiteral>] ParseUpdateMetaReturn =
            abstract from: Map<float, float> with get, set
            abstract ``to``: Map<float, float> with get, set

        type [<AllowNullLiteral>] LazyStructReader =
            abstract gen: Generator<U3<GC, Item, Skip>, unit, obj> with get, set
            abstract curr: U3<Item, Skip, GC> option with get, set
            abstract ``done``: bool with get, set
            abstract filterSkips: bool with get, set
            abstract next: unit -> U3<Item, GC, Skip> option

        type [<AllowNullLiteral>] LazyStructReaderStatic =
            [<Emit "new $0($1...)">] abstract Create: decoder: U2<UpdateDecoderV1, UpdateDecoderV2> * filterSkips: bool -> LazyStructReader

        type [<AllowNullLiteral>] LazyStructWriter =
            abstract currClient: float with get, set
            abstract startClock: float with get, set
            abstract written: float with get, set
            abstract encoder: U2<UpdateEncoderV2, UpdateEncoderV1> with get, set
            /// We want to write operations lazily, but also we need to know beforehand how many operations we want to write for each client.
            /// 
            /// This kind of meta-information (#clients, #structs-per-client-written) is written to the restEncoder.
            /// 
            /// We fragment the restEncoder and store a slice of it per-client until we know how many clients there are.
            /// When we flush (toUint8Array) we write the restEncoder using the fragments and the meta-information.
            abstract clientStructs: ResizeArray<LazyStructWriterClientStructs> with get, set

        type [<AllowNullLiteral>] LazyStructWriterStatic =
            [<Emit "new $0($1...)">] abstract Create: encoder: U2<UpdateEncoderV1, UpdateEncoderV2> -> LazyStructWriter

        type [<AllowNullLiteral>] LazyStructWriterClientStructs =
            abstract written: float with get, set
            abstract restEncoder: Uint8Array with get, set

    module YEvent =
        type AbstractType<'EventType> = Types.AbstractType.AbstractType<'EventType>
        type Transaction = Utils.Transaction.Transaction
        type AbstractStruct = Structs.AbstractStruct.AbstractStruct
        type Item = Structs.Item.Item

        type [<AllowNullLiteral>] IExports =
            abstract YEvent: YEventStatic

        type [<AllowNullLiteral>] YEvent<'T, 'E> =
            /// The type on which this event was created on.
            abstract target: 'T with get, set
            /// The current target on which the observe callback is called.
            abstract currentTarget: AbstractType<obj option> with get, set
            /// The transaction that triggered this event.
            abstract transaction: Transaction with get, set
            abstract changes: Object option with get
            abstract keys: Map<string, KeysMap> option with get
            abstract delta: ResizeArray<Delta<'E>> with get
    //        obj
            /// Check if a struct is deleted by this event.
            /// 
            /// In contrast to change.deleted, this method also returns true if the struct was added and then deleted.
            abstract deletes: ``struct``: AbstractStruct -> bool
    //        obj
    //        obj
            /// Check if a struct is added by this event.
            /// 
            /// In contrast to change.deleted, this method also returns true if the struct was added and then deleted.
            abstract adds: ``struct``: AbstractStruct -> bool
    //        obj

        type [<AllowNullLiteral>] YEventStatic =
            /// <param name="target">The changed type.</param>
            [<Emit "new $0($1...)">] abstract Create: target: 'T * transaction: Transaction -> YEvent<'T, _>

        type [<StringEnum>] [<RequireQualifiedAccess>] KeysMapAction =
            | Add
            | Update
            | Delete

        type [<AllowNullLiteral>] KeysMap =
            abstract action: KeysMapAction with get, set
            abstract oldValue: obj option with get, set
            abstract newValue: obj option with get, set

        type [<AllowNullLiteral>] DeltaAttributes =
            [<Emit "$0[$1]{{=$2}}">] abstract Item: x: string -> obj option with get, set

        type [<AllowNullLiteral>] Delta<'insert> =
            abstract insert: 'insert option with get, set
            abstract retain: int option with get, set
            abstract delete: int option with get, set
            abstract attributes: DeltaAttributes option with get, set

module Y =
    type Doc = Utils.Doc.Doc
    type Text = Types.YText.YText
    type Map<'a> = Types.YMap.YMap<'a>
    type Array<'a> = Types.YArray.YArray<'a>
    type XmlFragment = Types.YXmlFragment.YXmlFragment
    type AbstractType = Types.AbstractType.AbstractType<obj>
    type Transaction = Utils.Transaction.Transaction
    
    type Delta<'insert> = Utils.YEvent.Delta<'insert>
    
    module Delta =
        open JsInterop
        let (|Insert|Retain|Delete|) (delta : Delta<_>) =
            match delta.insert, delta.retain, delta.delete with
            | Some insert, None, None -> Insert insert
            | None, Some retain, None -> Retain retain
            | None, None, Some delete -> Delete delete
            | _, _, _ -> invalidOp $"Invalid delta event. ({delta})"

        let Insert ins = jsOptions<Delta<_>> (fun o -> o.insert <- Some ins)
        let Delete del = jsOptions<Delta<_>> (fun o -> o.delete <- Some del)
        let Retain ret = jsOptions<Delta<_>> (fun o -> o.retain <- Some ret)

    module Text =
        type Event = Types.YText.YTextEvent

    module Array =
        type Event<'a> = Types.YArray.YArrayEvent<'a>


    
// Exported members
type Y () =
    [<ImportMember("yjs")>]
    static member Doc : Utils.Doc.DocStatic = jsNative
    [<ImportMember("yjs")>]
    static member Transaction : Utils.Doc.Transaction = jsNative
    [<ImportMember("yjs")>]
    static member Array : Types.YArray.YArrayStatic = jsNative
    [<ImportMember("yjs")>]
    static member Map : Types.YMap.YMapStatic = jsNative
    [<ImportMember("yjs")>]
    static member Text : Types.YText.YTextStatic = jsNative
    [<ImportMember("yjs")>]
    static member XmlText : Types.YXmlText.YXmlTextStatic = jsNative
    [<ImportMember("yjs")>]
    static member XmlHook : Types.YXmlHook.YXmlHookStatic = jsNative
    [<ImportMember("yjs")>]
    static member XmlElement : Types.YXmlElement.YXmlElementStatic = jsNative
    [<ImportMember("yjs")>]
    static member XmlFragment : Types.YXmlFragment.YXmlFragmentStatic = jsNative
    [<ImportMember("yjs")>]
    static member YXmlEvent : Types.YXmlEvent.YXmlEventStatic = jsNative
    [<ImportMember("yjs")>]
    static member YMapEvent : Types.YMap.YMapEventStatic = jsNative
    [<ImportMember("yjs")>]
    static member YArrayEvent : Types.YArray.YArrayEventStatic = jsNative
    [<ImportMember("yjs")>]
    static member YTextEvent : Types.YText.YTextEventStatic = jsNative
    [<ImportMember("yjs")>]
    static member YEvent : Utils.YEvent.YEventStatic = jsNative
    [<ImportMember("yjs")>]
    static member Item : Structs.Item.ItemStatic = jsNative
    [<ImportMember("yjs")>]
    static member AbstractStruct : Structs.AbstractStruct.AbstractStructStatic = jsNative
    [<ImportMember("yjs")>]
    static member GC : Structs.GC.GCStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentBinary : Structs.ContentBinary.ContentBinaryStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentDeleted : Structs.ContentDeleted.ContentDeletedStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentEmbed : Structs.ContentEmbed.ContentEmbedStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentFormat : Structs.ContentFormat.ContentFormatStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentJSON : Structs.ContentJSON.ContentJSONStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentAny : Structs.ContentAny.ContentAnyStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentString : Structs.ContentString.ContentStringStatic = jsNative
    [<ImportMember("yjs")>]
    static member ContentType : Structs.ContentType.ContentTypeStatic = jsNative
    [<ImportMember("yjs")>]
    static member AbstractType : Types.AbstractType.AbstractTypeStatic = jsNative
    [<ImportMember("yjs")>]
    static member getTypeChildren (t: Types.AbstractType.AbstractType<obj option>) : Array<Structs.Item.Item> = jsNative
    [<ImportMember("yjs")>]
    static member createRelativePositionFromTypeIndex (``type``: Types.AbstractType.AbstractType<obj option>, index: float, ?assoc: float) : Utils.RelativePosition.RelativePosition = jsNative
    [<ImportMember("yjs")>]
    static member createRelativePositionFromJSON (json: obj option) : Utils.RelativePosition.RelativePosition = jsNative
    [<ImportMember("yjs")>]
    static member createAbsolutePositionFromRelativePosition (rpos: Utils.RelativePosition.RelativePosition, doc: Utils.Doc.Doc) : Utils.RelativePosition.AbsolutePosition option = jsNative
    [<ImportMember("yjs")>]
    static member compareRelativePositions (a: Utils.RelativePosition.RelativePosition option, b: Utils.RelativePosition.RelativePosition option) : bool = jsNative
    [<ImportMember("yjs")>]
    static member AbsolutePosition : Utils.RelativePosition.AbsolutePositionStatic = jsNative
    [<ImportMember("yjs")>]
    static member RelativePosition : Utils.RelativePosition.RelativePositionStatic = jsNative
    [<ImportMember("yjs")>]
    static member ID : Utils.ID.IDStatic = jsNative
    [<ImportMember("yjs")>]
    static member createID (client: float, clock: float) : Utils.ID.ID = jsNative
    [<ImportMember("yjs")>]
    static member compareIDs (client: float, clock: float) : Utils.ID.ID = jsNative
    [<ImportMember("yjs")>]
    static member getState (store: Utils.StructStore.StructStore, client: float) : float = jsNative
    [<ImportMember("yjs")>]
    static member Snapshot : Utils.Snapshot.SnapshotStatic = jsNative
    [<ImportMember("yjs")>]
    static member createSnapshot (ds: Utils.DeleteSet.DeleteSet, sm: Map<float, float>) : Utils.Snapshot.Snapshot = jsNative
    [<ImportMember("yjs")>]
    static member createDeleteSet (unit) : Utils.DeleteSet.DeleteSet = jsNative
    [<ImportMember("yjs")>]
    static member createDeleteSetFromStructStore (ss: Utils.StructStore.StructStore) : Utils.DeleteSet.DeleteSet = jsNative
    [<ImportMember("yjs")>]
    static member cleanupYTextFormatting ( ``type``: Types.YText.YText) : float = jsNative
    [<ImportMember("yjs")>]
    static member snapshot (doc: Utils.Doc.Doc) : Utils.Snapshot.Snapshot = jsNative
    [<ImportMember("yjs")>]
    static member emptySnapshot : Utils.Snapshot.Snapshot = jsNative
    [<ImportMember("yjs")>]
    static member findRootTypeKey (``type``: Types.AbstractType.AbstractType<obj option>) : string = jsNative
    [<ImportMember("yjs")>]
    static member findIndexSS (structs: Array<U2<Structs.Item.Item, Structs.GC.GC>>, clock: float) : float = jsNative
    [<ImportMember("yjs")>]
    static member getItem (arg0: Utils.StructStore.StructStore, arg1: Utils.ID.ID) : Structs.Item.Item = jsNative
    [<ImportMember("yjs")>]
    static member typeListToArraySnapshot (``type``: Types.AbstractType.AbstractType<obj option>, snapshot: Utils.Snapshot.Snapshot) : Array<obj option> = jsNative
    // IExportsTypeListInsertGenericsAfterArray seems odd...
    // See packages\Fable.Yjs\node_modules\yjs\dist\src\types\AbstractType.d.ts
    [<ImportMember("yjs")>]
    static member typeMapGetSnapshot (parent: Types.AbstractType.AbstractType<obj option>, key: string, snapshot: Utils.Snapshot.Snapshot) : U6<Types.AbstractType.IExportsTypeListInsertGenericsAfterArray, float, Array<obj option>, string, Uint8Array, Types.AbstractType.AbstractType<obj option>> option= jsNative
    [<ImportMember("yjs")>]
    static member createDocFromSnapshot (originDoc: Utils.Doc.Doc, snapshot: Utils.Snapshot.Snapshot, ?newDoc: Utils.Doc.Doc) : Utils.Doc.Doc = jsNative
    [<ImportMember("yjs")>]
    static member iterateDeletedStructs (transaction: Utils.Transaction.Transaction, ds: Utils.DeleteSet.DeleteSet, f: (U2<Structs.GC.GC, Structs.Item.Item> -> unit)) : unit = jsNative
    [<ImportMember("yjs")>]
    static member applyUpdate (ydoc: Utils.Doc.Doc, update: Uint8Array, ?transactionOrigin: obj) : unit = jsNative
    [<ImportMember("yjs")>]
    static member applyUpdateV2 (ydoc: Utils.Doc.Doc, update: Uint8Array, ?transactionOrigin: obj, ?YDecoder: obj) : unit = jsNative
    [<ImportMember("yjs")>]
    static member readUpdate (decoder: Lib0.Decoding.Decoder, ydoc: Utils.Doc.Doc, ?transactionOrigin: obj) : unit = jsNative
    [<ImportMember("yjs")>]
    static member readUpdateV2 (decoder: Lib0.Decoding.Decoder, ydoc: Utils.Doc.Doc, ?transactionOrigin: obj, ?structDecoder: U2<Utils.UpdateDecoder.UpdateDecoderV1, Utils.UpdateDecoder.UpdateDecoderV2>) : unit = jsNative
    [<ImportMember("yjs")>]
    static member encodeStateAsUpdate (doc: Utils.Doc.Doc, ?encodedTargetStateVector: Uint8Array) : Uint8Array= jsNative
    [<ImportMember("yjs")>]
    static member encodeStateAsUpdateV2 (doc: Utils.Doc.Doc, ?encodedTargetStateVector: Uint8Array, ?encoder: U2<Utils.UpdateEncoder.UpdateEncoderV2, Utils.UpdateEncoder.UpdateEncoderV1>) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member encodeStateVector (doc: U2<Utils.Doc.Doc, Map<float, float>>) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member UndoManager : Utils.UndoManager.UndoManager = jsNative
    [<ImportMember("yjs")>]
    static member decodeSnapshot (buf: Uint8Array) : Utils.Snapshot.Snapshot = jsNative
    [<ImportMember("yjs")>]
    static member encodeSnapshot (snapshot: Utils.Snapshot.Snapshot) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member decodeSnapshotV2 (buf: Uint8Array, ?decoder: U2<Utils.UpdateDecoder.DSDecoderV1, Utils.UpdateDecoder.DSDecoderV2>) : Utils.Snapshot.Snapshot = jsNative
    [<ImportMember("yjs")>]
    static member encodeSnapshotV2 (snapshot: Utils.Snapshot.Snapshot, ?encoder: U2<Utils.UpdateEncoder.DSEncoderV1, Utils.UpdateEncoder.DSEncoderV2>) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member decodeStateVector (decodedState: Uint8Array) : Map<float, float> = jsNative
    [<ImportMember("yjs")>]
    static member logUpdate (update: Uint8Array) : unit = jsNative
    [<ImportMember("yjs")>]
    static member logUpdateV2 (update: Uint8Array, ?YDecoder: obj) : unit = jsNative
    [<ImportMember("yjs")>]
    static member decodeUpdate (update: Uint8Array) : Utils.updates.DecodeUpdateReturn = jsNative
    [<ImportMember("yjs")>]
    static member decodeUpdateV2 (update: Uint8Array, ?YDecoder: obj) : Utils.updates.DecodeUpdateV2Return = jsNative
    [<ImportMember("yjs")>]
    static member relativePositionToJSON (rpos: Utils.RelativePosition.RelativePosition) : obj option = jsNative
    [<ImportMember("yjs")>]
    static member isDeleted (ds: Utils.DeleteSet.DeleteSet, id: Utils.ID.ID) : bool = jsNative
    [<ImportMember("yjs")>]
    static member isParentOf (parent: Types.AbstractType.AbstractType<obj option>, child: Structs.Item.Item option) : bool = jsNative
    [<ImportMember("yjs")>]
    static member equalSnapshots (snap1: Utils.Snapshot.Snapshot, snap2: Utils.Snapshot.Snapshot) : bool = jsNative
    [<ImportMember("yjs")>]
    static member PermanentUserData : Utils.PermanentUserData.PermanentUserDataStatic = jsNative
    [<ImportMember("yjs")>]
    static member tryGc (ds: Utils.DeleteSet.DeleteSet, store: Utils.StructStore.StructStore, gcFilter: (Structs.Item.Item -> bool)) : unit = jsNative
    [<ImportMember("yjs")>]
    static member transact (doc: Types.AbstractType.Doc, f: (Utils.Transaction.Transaction -> unit), ?origin: obj, ?local: bool) : unit = jsNative
    [<ImportMember("yjs")>]
    static member AbstractConnector : Utils.AbstractConnector.AbstractConnectorStatic = jsNative
    [<ImportMember("yjs")>]
    static member logType (``type``: Types.AbstractType.AbstractType<obj option>) unit = jsNative
    [<ImportMember("yjs")>]
    static member mergeUpdates (updates: Array<Uint8Array>) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member mergeUpdatesV2 (updates: Array<Uint8Array>, ?YDecoder: obj, ?YEncoder: obj) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member parseUpdateMeta (update: Uint8Array) : Utils.updates.ParseUpdateMetaReturn = jsNative
    [<ImportMember("yjs")>]
    static member parseUpdateMetaV2 (update: Uint8Array, ?YDecoder: obj) : Utils.updates.ParseUpdateMetaV2Return = jsNative
    [<ImportMember("yjs")>]
    static member encodeStateVectorFromUpdate (update: Uint8Array) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member encodeStateVectorFromUpdateV2 (update: Uint8Array, ?YEncoder: obj, ?YDecoder: obj) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member encodeRelativePosition (rpos: Utils.RelativePosition.RelativePosition) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member decodeRelativePosition (uint8Array: Uint8Array) : Utils.RelativePosition.RelativePosition = jsNative
    [<ImportMember("yjs")>]
    static member diffUpdate (update: Uint8Array, sv: Uint8Array) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member diffUpdateV2 (update: Uint8Array, sv: Uint8Array, ?YDecoder: obj, ?YEncoder: obj) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member convertUpdateFormatV1ToV2 (update: Uint8Array) : Uint8Array = jsNative
    [<ImportMember("yjs")>]
    static member convertUpdateFormatV2ToV1 (update: Uint8Array) : Uint8Array = jsNative