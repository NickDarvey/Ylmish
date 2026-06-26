module rec Ylmish.Y

open System

open FSharp.Data.Adaptive
open Yjs

open Ylmish.Adaptive
open Ylmish.Disposables

module A =

    /// A value inside an Adaptive element
    type Value =
        | String of string
        | Text of IndexList<char>

    /// An Adapative element.
    type Element = Ylmish.Adaptive.Codec.Element<Value>

module Y =
    module Y =
        // TODO move this type to Yjs Fable bindings
        // Y.Map, Y.Array only support these element types anyway
        // > Collection used to store key-value entries in an unordered manner. Keys are always represented as UTF-8 strings. Values can be any value type supported by Yrs: JSON-like primitives as well as shared data types.
        // https://github.com/y-crdt/y-crdt/blob/279cd56d7472fbb41af743ecf28f552024cabd65/yrs/src/types/map.rs#L15-L17
        // Null is a proper value
        // https://github.com/yjs/yjs/blob/9afc5cf61531f19d9caceb590002392ad393ed62/tests/y-map.tests.js#L106
        [<Fable.Core.Erase>]
        [<RequireQualifiedAccess>]
        type Element =
            | String of string
            | Text of Y.Text
            | Array of Y.Array<Element option>
            | Map of Y.Map<Element option>

    /// A Y element.
    type Element = Y.Element

[<RequireQualifiedAccess>]
module Delta =
    let applyYDelta
        (getItem  : 'a -> int -> 'b)
        (getLength: 'a -> int)
        (delta: ResizeArray<Y.Delta<'a>>) (list: clist<'b>) : unit =
        let rec loop i (delta: Y.Delta<'a> list) =
            match delta with
            | [] -> ()
            | d::xs ->
                match d.delete, d.insert, d.retain with
                | Some n, None, None ->
                    for n in 0..(n - 1) do
                    list.RemoveAt(i) |> ignore

                    loop i xs

                | None, Some s, None ->
                    let length = getLength s
                    for n in 0..(length - 1) do
                        list.InsertAt(i + n, getItem s n) |> ignore

                    loop (i + length) xs

                | None, None, Some n ->
                    loop (i + n) xs

                | delete, insert, retain ->
                    failwith $"Expected exactly one of delete, insert and retain to be Some, but got: delete: {delete}, insert: {insert}, retain: {retain}"

        loop 0 (List.ofSeq delta)

    type private Op<'a> =
    | OpInsert of i: int * xs: 'a list
    | OpRemove of i: int * length: int * IndexListDelta<'a>
    | OpNone

    let applyAdaptiveDelta
        (insert : 'y      -> int -> 'b -> unit)
        (combine: 'a list -> 'b)
        (delete : 'y      -> int -> int -> unit)
        (list: IndexList<'a>) (delta: IndexListDelta<'a>) (y: 'y) : unit =
        let applyDeltas list ds =
            ds
            |> IndexList.applyDelta list
            |> fst

        let applyDelta list d =
            IndexListDelta<'a>.Empty.Add(d)
            |> applyDeltas list

        let getPosition index list =
            IndexList.tryGetPosition index list
            |> Option.get

        let rec loop current acc xs =
            match xs with
            | [] ->
                match acc with
                | OpInsert (i, xs)   -> insert y i (combine (List.rev xs))
                | OpRemove (i, n, _) -> delete y i n
                | OpNone             -> ()

            | ((index, op) as d)::xs ->
                match op with
                | ElementOperation.Set c ->
                    // Apply any pending removes, and inserts which are not
                    // contiguous with the current one
                    let updated, n, existing =
                        match acc with
                        | OpInsert (i, xs) ->
                            let updated = applyDelta current d
                            let n = getPosition index updated

                            let existing =
                                if i + xs.Length <> n - 1 then
                                    insert y i (combine (List.rev xs))
                                    []
                                else
                                    xs

                            updated, n, existing

                        | OpRemove (i, n, ds) ->
                            delete y i n
                            let updated = applyDeltas current (ds.Add(d))
                            updated, i, []

                        | OpNone ->
                            let updated = applyDelta current d
                            let n = getPosition index updated

                            updated, n, []

                    loop updated (OpInsert (n, c::existing)) xs

                | ElementOperation.Remove ->
                    match acc with
                    | OpInsert (i, xs)   -> insert y i (combine (List.rev xs))
                    | OpRemove (_, _, _) -> ()
                    | OpNone             -> ()

                    let acc =
                        match acc with
                        | OpRemove (i, n, ds) ->
                            OpRemove (i, n + 1, ds.Add(d))

                        | _ ->
                            let n = IndexList.tryGetPosition index current
                            OpRemove (n.Value, 1, IndexListDelta.Empty.Add(d))

                    loop current acc xs

        loop list OpNone (List.ofSeq delta)

[<RequireQualifiedAccess>]
module Text =
    // TODO this should be private but right now there's tests for Delta.applyYDelta/Delta.applyAdaptiveDelta via the text implementation.
    module Impl =
        let applyYDelta (delta: ResizeArray<Y.Delta<string>>) (list: clist<char>) : unit =
            Delta.applyYDelta
                (fun (s: string) n -> s[n])
                (fun s -> s.Length)
                delta list

        let applyAdaptiveDelta (list: IndexList<_>) (delta: IndexListDelta<char>) (y: Y.Text) : unit =
            Delta.applyAdaptiveDelta
                (fun (y: Y.Text) n s -> y.insert(n, s))
                (fun (cs: char list) -> String(Array.ofList cs))
                (fun  y i length     -> y.delete(i, length))
                list delta y

    // TODO are we using the right callbacks with Adaptive?
    //  https://docs.yjs.dev/api/shared-types/y.array#observing-changes-y.arrayevent
    //  https://github.com/fsprojects/FSharp.Data.Adaptive/commit/b2b8f7a7a5194762b294a461c03b498be0db38d0
    //  https://github.com/krauthaufen/RemoteAdaptive/blob/master/Program.fs
    //  https://github.com/krauthaufen/RemoteAdaptive/blob/master/Utilities.fs
    //  https://discord.com/channels/611129394764840960/624645480219148299/954458318418612354
    //  > zaoa — 03/19/2022
    //  > Can I ask why this way is prefered over AddCallback ?
    //  > krauthaufen — 03/19/2022
    //  > okay, so adaptive is a so-called push/pull implementation which uses a marking-phase for eagerly marking all affected things dirty and has a separate evaluation phase conceptually. When adding a callback that evaluates things you basically "fuse" both phases and the marking needs to execute user-code (like mapping functions, etc.)
    //  > This isn't really a problem until you have dynamic dependency graphs in which case execution-order is not easy to determine anymore when a user changes multiple inputs at once. There are very sophisticated ways to solve that but (since eager evaluation wasn't really important yet) we opted for a straight-forward way of dealing with that which will, in some scenarios, perform rather slow
    //  > however you might as well use it when your task isn't very performance-critical
    //  > zaoa — 03/19/2022
    //  > So how does the AddMarkingCallback approach differ?
    //  > krauthaufen — 03/19/2022
    //  > basically the callback is very cheap and no internal value gets updated during the marking-phase that way
    //  > all the heavy-lifting is offloaded to a thread where execution order is simply dictated by the call-stack
    //  > it's also a way to allow for clean batch-changes
    //  > things like
    //  > transact (fun () -> input.Add 10)
    //  > transact (fun () -> input.Add 11)
    //  > transact (fun () -> input.Add 12)
    //  > I think in the "event-world" this is called debouncing
    //  > we could arguably hide this implementation behind a combinator AList.observe : action : (State<'a> -> Delta<'a> -> unit) -> list : alist<'a> -> IDisposable
    // Alternatively, a different kind of wrapping:
    //  https://github.com/krauthaufen/Fable.Elmish.Adaptive/blob/master/src/Fable.React.Adaptive/AdaptiveHelpers.fs
    /// Attach Yjs→Adaptive (decode) direction: observes changes on the Y.Text and applies them to the clist.
    let attachDecode (active : bool ref) (atext : char clist) (ytext : Y.Text) : IDisposable =
        // https://docs.yjs.dev/api/delta-format
        let observeY (e : Y.Text.Event) (_ : Y.Transaction) =
            if not active.Value then
                active.Value <- true
                try
                    transact (fun () -> Impl.applyYDelta e.delta atext)
                finally
                    active.Value <- false

        ytext.observe observeY
        {
            new System.IDisposable with
                member _.Dispose () = ytext.unobserve observeY
        }

    /// Attach Adaptive→Yjs (encode) direction: observes changes on the clist and applies them to the Y.Text.
    let attachEncode (active : bool ref) (atext : char clist) (ytext : Y.Text) : IDisposable =
        // AddCallback emits an initial "echo" of the current contents only when
        // the list is non-empty; ytext already holds that content, so we skip it.
        // For an initially-empty list there is NO echo, so we must not skip the
        // first real edit (otherwise a fresh text field swallows its first keystroke).
        let mutable initialisationCallback = not (Seq.isEmpty atext)

        atext.AddCallback(fun list delta ->
            if initialisationCallback then
                // Skip the first callback - it's to perform initialisation, which we've already done
                // before this function is called
                initialisationCallback <- false
            else if not active.Value then
                active.Value <- true
                try
                match ytext.doc with
                | Some doc ->
                    doc.transact(fun _tr -> Impl.applyAdaptiveDelta list delta ytext)
                | None -> failwith $"ytext is not associated with a document"

                finally
                    active.Value <- false
        )

    /// Attach bi-directional synchronization between an adaptive clist and a Y.Text.
    /// Returns a combined IDisposable that will unsubscribe both directions when disposed.
    let attach (active : bool ref) (atext : char clist) (ytext : Y.Text) : IDisposable =
        let decodeDisposable = attachDecode active atext ytext
        let encodeDisposable = attachEncode active atext ytext
        {
            new System.IDisposable with
                member _.Dispose () =
                    decodeDisposable.Dispose()
                    encodeDisposable.Dispose()
        }

    let ofAdaptive (atext : char clist) : Y.Text =
        let initial = System.String.Concat(atext)
        let ytext = Y.Text.Create (initial)

        // TODO something with these disposables
        //  At first I thought we want something like
        //  https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.conditionalweaktable-2?view=net-7.0
        //  where when the subject is cleaned-up, our subscription is disposed.
        //  But then I realised the subject would never be cleaned-up _because_ of our subscription.
        //  Is this useful?
        //  https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/WeakRef
        let active = ref false
        let _ = attach active atext ytext
        ytext

    let toAdaptive (ytext : Y.Text) : char clist =
        let atext : char clist = ytext.toString () :> _ seq |> clist
        let active = ref false
        let _ = attach active atext ytext
        atext

[<RequireQualifiedAccess>]
module Array =
    module Impl =
        let applyYDelta (delta: ResizeArray<Y.Delta<Array<Y.Element option>>>) (list: clist<A.Element option>) : unit =
            Delta.applyYDelta (fun (items : Array<Y.Element option>) i -> items[i] |> Option.map Element.toAdaptive) (fun items -> items.Count) delta list

        let applyAdaptiveDelta (list: IndexList<A.Element option>) (delta: IndexListDelta<A.Element option>) (y: Y.Array<Y.Element option>) : unit =
            Delta.applyAdaptiveDelta
                (fun (y: Y.Array<_>) n s -> y.insert(n, s))
                (fun (xs: _ list)        -> xs |> List.map (Option.map Element.ofAdaptive) |> Array.ofList)
                (fun  y i length         -> y.delete(i, length))
                list delta y

    /// Attach Yjs→Adaptive (decode) direction: observes changes on the Y.Array and applies them to the clist.
    let attachDecode (active : bool ref) (alist : clist<A.Element option>) (yarray : Y.Array<Y.Element option>) : IDisposable =
        // https://docs.yjs.dev/api/delta-format
        let observeY (e : Y.Array.Event<Y.Element option>) (_ : Y.Transaction) =
            if not active.Value then
                active.Value <- true
                try
                    transact (fun () -> Impl.applyYDelta e.delta alist)
                finally
                    active.Value <- false

        yarray.observe observeY
        {
            new System.IDisposable with
                member _.Dispose () = yarray.unobserve observeY
        }

    /// Attach Adaptive→Yjs (encode) direction: observes changes on the clist and applies them to the Y.Array.
    let attachEncode (active : bool ref) (alist : clist<A.Element option>) (yarray : Y.Array<Y.Element option>) : IDisposable =
        let mutable initialisationCallback = true

        alist.AddCallback(fun list delta ->
            if initialisationCallback then
                // Skip the first callback - it's to perform initialisation, which we've already done
                // before this function is called
                initialisationCallback <- false
            else if not active.Value then
                active.Value <- true
                try
                match yarray.doc with
                | Some doc ->
                    doc.transact(fun _tr -> Impl.applyAdaptiveDelta list delta yarray)
                | None -> failwith $"yarray is not associated with a document"

                finally
                    active.Value <- false
        )

    /// Attach bi-directional synchronization between an adaptive clist and a Y.Array.
    /// Returns a combined IDisposable that will unsubscribe both directions when disposed.
    let attach (active : bool ref) (alist : clist<A.Element option>) (yarray : Y.Array<Y.Element option>) : IDisposable =
        let decodeDisposable = attachDecode active alist yarray
        let encodeDisposable = attachEncode active alist yarray
        {
            new System.IDisposable with
                member _.Dispose () =
                    decodeDisposable.Dispose()
                    encodeDisposable.Dispose()
        }

    let toAdaptive (yarray : Y.Array<Y.Element option>) : clist<A.Element option> =
        let alist =
            yarray
            |> Seq.map (Option.map Element.toAdaptive)
            |> clist
        let active = ref false
        let _ = attach active alist yarray
        alist

    let ofAdaptive (alist : alist<A.Element option>) : Y.Array<Y.Element option> =
        let yarray = Y.Array.Create ()
        do AList.force alist
            |> IndexList.map (Option.map Element.ofAdaptive)
            |> IndexList.toArray
            |> yarray.push
        yarray

module Map =
    /// Attach Yjs→Adaptive (decode) direction: observes changes on the Y.Map and applies them to the cmap.
    let attachDecode (active : bool ref) (amap : cmap<string, A.Element option>) (ymap : Y.Map<Y.Element option>) : IDisposable =
        let observeY (e : Yjs.Types.YMap.YMapEvent<Y.Element option>) (_ : Y.Transaction) =
            if not active.Value then
                active.Value <- true
                try
                    transact (fun () ->
                        // JS.Set.forEach has signature: (value, value2, set) -> unit
                        // But we can also convert it to an array
                        let keysSet : Fable.Core.JS.Set<obj option> = e.keysChanged
                        keysSet.forEach(fun key _ _ ->
                            match key with
                            | Some k ->
                                let keyStr = string k
                                match ymap.get keyStr with
                                | Some (Some yelement) ->
                                    amap.[keyStr] <- Some (Element.toAdaptive yelement)
                                | Some None ->
                                    amap.[keyStr] <- None
                                | None ->
                                    amap.Remove keyStr |> ignore
                            | None -> ()
                        )
                    )
                finally
                    active.Value <- false

        ymap.observe observeY
        {
            new System.IDisposable with
                member _.Dispose () = ymap.unobserve observeY
        }

    /// Attach Adaptive→Yjs (encode) direction: observes changes on the cmap and applies them to the Y.Map.
    let attachEncode (active : bool ref) (amap : cmap<string, A.Element option>) (ymap : Y.Map<Y.Element option>) : IDisposable =
        let mutable initialisationCallback = true

        amap.AddCallback(fun _ delta ->
            if initialisationCallback then
                initialisationCallback <- false
            else if not active.Value then
                active.Value <- true
                try
                    match ymap.doc with
                    | Some doc ->
                        doc.transact(fun _tr ->
                            // Iterate over HashMapDelta - it's a seq of (key, operation) tuples
                            delta
                            |> Seq.iter (fun (key, op) ->
                                match op with
                                | Set (Some value) ->
                                    let yelement = Element.ofAdaptive value
                                    ymap.set(key, Some yelement) |> ignore
                                | Set None ->
                                    ymap.set(key, None) |> ignore
                                | Remove ->
                                    ymap.delete key
                            )
                        )
                    | None -> failwith $"ymap is not associated with a document"
                finally
                    active.Value <- false
        )

    /// Attach bi-directional synchronization between an adaptive cmap and a Y.Map.
    /// Returns a combined IDisposable that will unsubscribe both directions when disposed.
    let attach (active : bool ref) (amap : cmap<string, A.Element option>) (ymap : Y.Map<Y.Element option>) : IDisposable =
        let decodeDisposable = attachDecode active amap ymap
        let encodeDisposable = attachEncode active amap ymap
        {
            new System.IDisposable with
                member _.Dispose () =
                    decodeDisposable.Dispose()
                    encodeDisposable.Dispose()
        }

    let toAdaptive (ymap : Y.Map<Y.Element option>) : cmap<string, A.Element option> =
        let amap = cmap ()
        ymap.forEach(fun value key _map ->
            match value with
            | Some yelement -> amap.[key] <- Some (Element.toAdaptive yelement)
            | None -> amap.[key] <- None
        ) |> ignore
        let active = ref false
        let _ = attach active amap ymap
        amap

    /// Convert a read-only adaptive map to a Y.Map (one-way, no observers).
    /// Used internally for element conversion.
    let internal ofAMap (amap : amap<string, A.Element option>) : Y.Map<Y.Element option> =
        let ymap = Y.Map.Create ()
        AMap.force amap
        |> HashMap.iter (fun key value ->
            ymap.set(key, Option.map Element.ofAdaptive value) |> ignore
        )
        ymap

    /// Convert a changeable adaptive map to a Y.Map with bi-directional synchronization.
    let ofAdaptive (amap : cmap<string, A.Element option>) : Y.Map<Y.Element option> =
        let ymap = Y.Map.Create ()
        // Initialize with current contents
        AMap.force amap
        |> HashMap.iter (fun key value ->
            ymap.set(key, Option.map Element.ofAdaptive value) |> ignore
        )
        // Attach observers for bi-directional synchronization
        let active = ref false
        let _ = attach active amap ymap
        ymap

module Element =
#if FABLE_COMPILER
    // Fable cannot perform runtime type tests on erased union cases that wrap
    // interface types (YArray, YMap), so we use explicit JS instanceof checks.
    [<Fable.Core.Emit("typeof $0 === 'string'")>]
    let private isString (_x: obj) : bool = false

    [<Fable.Core.Import("Array", "yjs")>]
    let private jsYArray : obj = obj()

    [<Fable.Core.Import("Map", "yjs")>]
    let private jsYMap : obj = obj()

    [<Fable.Core.Import("Text", "yjs")>]
    let private jsYText : obj = obj()

    [<Fable.Core.Emit("$0 instanceof $1")>]
    let private jsInstanceOf (_x: obj) (_ctor: obj) : bool = false

    let toAdaptive (yelement : Y.Element) : A.Element =
        let value : obj = unbox yelement
        match () with
        | _ when isString value ->
            A.Element.Value (A.Value.String (unbox value))
        | _ when jsInstanceOf value jsYText ->
            A.Element.Text (Text.toAdaptive (unbox value))
        | _ when jsInstanceOf value jsYArray ->
            A.Element.AList (Array.toAdaptive (unbox value))
        | _ when jsInstanceOf value jsYMap ->
            A.Element.AMap (Map.toAdaptive (unbox value) :> amap<_, _>)
        | _ ->
            failwith $"Element.toAdaptive: unsupported Y.Element runtime type: %A{value}"
#else
    let toAdaptive (yelement : Y.Element) : A.Element =
        match yelement with
        | Y.Element.Array yarray -> A.Element.AList (Array.toAdaptive yarray)
        | Y.Element.Map ymap -> A.Element.AMap (Map.toAdaptive ymap :> amap<_, _>)
        | Y.Element.String str -> A.Element.Value (A.Value.String str)
        | Y.Element.Text ytext -> A.Element.Text (Text.toAdaptive ytext)
#endif

    let ofAdaptive (aelement : A.Element) : Y.Element =
        match aelement with
        | A.Element.AList alist -> Y.Element.Array (Array.ofAdaptive alist)
        | A.Element.AMap amap -> Y.Element.Map (Map.ofAMap amap)
        | A.Element.Value (A.Value.String str) -> Y.Element.String str
        | A.Element.Value (A.Value.Text text) ->
            // Convert Text (IndexList<char>) to String for Y.Element
            Y.Element.String (System.String.Concat(text))
        | A.Element.Text text -> Y.Element.Text (Text.ofAdaptive text)
        | A.Element.Custom _ ->
            // Custom bindings are dispatched via Y.Doc.connect (plan 0002, Step 5).
            failwith "Element.Custom is not bridged through ofAdaptive (plan 0002, Step 5)"

module Doc =
    open FSharp.Data.Adaptive

    #if FABLE_COMPILER
    // Fable cannot perform runtime type tests on erased union cases
    [<Fable.Core.Emit("typeof $0 === 'string'")>]
    let private isString (_x: obj) : bool = false

    [<Fable.Core.Import("Array", "yjs")>]
    let private jsYArray : obj = obj()

    [<Fable.Core.Import("Map", "yjs")>]
    let private jsYMap : obj = obj()

    [<Fable.Core.Emit("$0 instanceof $1")>]
    let private jsInstanceOf (_x: obj) (_ctor: obj) : bool = false
    #endif

    /// Convert an Element<string> tree to a Y.Element tree
    let rec private elementToY (element : Codec.Element<string>) : Y.Element =
        match element with
        | Codec.Element.Value str -> Y.Element.String str
        | Codec.Element.AList alist ->
            let yarray = Y.Array.Create<Y.Element option> ()
            let items = AList.force alist |> IndexList.toList
            items |> List.iter (fun item ->
                match item with
                | Some e -> yarray.push [| Some (elementToY e) |] |> ignore
                | None -> yarray.push [| None |] |> ignore
            )
            Y.Element.Array yarray
        | Codec.Element.AMap amap ->
            let ymap = Y.Map.Create<Y.Element option> ()
            AMap.force amap
            |> HashMap.iter (fun key value ->
                match value with
                | Some e -> ymap.set(key, Some (elementToY e)) |> ignore
                | None -> ymap.set(key, None) |> ignore
            )
            Y.Element.Map ymap
        | Codec.Element.Text _ ->
            // The Element.Text ↔ Y.Element.Text bridge lands in plan 0002 Step 2.
            failwith "Element.Text is not yet bridged to Y.Element (plan 0002, Step 2)"
        | Codec.Element.Custom _ ->
            // Custom bindings are dispatched via Y.Doc.connect (plan 0002, Step 5).
            failwith "Element.Custom is not bridged through elementToY (plan 0002, Step 5)"

    /// Helper to convert any Y value (string, Y.Map, Y.Array) to Codec.Element
    let rec private valueToElement (value : obj) : Codec.Element<string> =
        #if FABLE_COMPILER
        match () with
        | _ when isString value -> Codec.Element.Value (unbox value)
        | _ when jsInstanceOf value jsYMap ->
            // It's a Y.Map<'T>, recursively convert its values
            let sourceMap = unbox<Y.Map<obj>> value
            let mutable items = HashMap.empty<string, Codec.Element<string> option>
            sourceMap.forEach (fun v k _ ->
                if isNull v then
                    items <- HashMap.add k None items
                else
                    items <- HashMap.add k (Some (valueToElement v)) items
            ) |> ignore
            Codec.Element.AMap (AMap.ofHashMap items)
        | _ when jsInstanceOf value jsYArray ->
            // It's a Y.Array<'T>, recursively convert its values
            let sourceArray = unbox<Y.Array<obj>> value
            let items =
                sourceArray.toArray()
                |> Seq.map (fun v ->
                    if isNull v then None
                    else Some (valueToElement v)
                )
                |> Seq.toList
                |> IndexList.ofList
            Codec.Element.AList (AList.ofIndexList items)
        | _ -> failwith $"valueToElement: unsupported value type: %A{value}"
        #else
        match value with
        | :? string as s -> Codec.Element.Value s
        | _ when value.GetType().Name.StartsWith("YMap") ->
            let sourceMap = unbox<Y.Map<obj>> value
            let mutable items = HashMap.empty<string, Codec.Element<string> option>
            sourceMap.forEach (fun v k _ ->
                if isNull v then
                    items <- HashMap.add k None items
                else
                    items <- HashMap.add k (Some (valueToElement v)) items
            ) |> ignore
            Codec.Element.AMap (AMap.ofHashMap items)
        | _ when value.GetType().Name.StartsWith("YArray") ->
            let sourceArray = unbox<Y.Array<obj>> value
            let items =
                sourceArray.toArray()
                |> Seq.map (fun v ->
                    if isNull v then None
                    else Some (valueToElement v)
                )
                |> Seq.toList
                |> IndexList.ofList
            Codec.Element.AList (AList.ofIndexList items)
        | _ -> failwith $"valueToElement: unsupported value type: %A{value}"
        #endif

    /// Materialize an Encoded Element tree into a Y.Doc's root map
    let materialize (doc : Y.Doc) (encoded : Codec.Encoded<Codec.Element<string>>) : unit =
        let rootMap = doc.getMap()

        // Force the encoded value to get the actual element tree
        match AVal.force encoded with
        | None -> ()  // Nothing to materialize
        | Some element ->
            // The root element should be an AMap (object)
            match element with
            | Codec.Element.AMap amap ->
                doc.transact(fun _ ->
                    // Clear existing keys that aren't in the new map
                    let newKeys = AMap.force amap |> HashMap.toSeq |> Seq.map fst |> Set.ofSeq
                    let mutable keysToDelete = []
                    rootMap.forEach (fun _ key _ ->
                        if not (Set.contains key newKeys) then
                            keysToDelete <- key :: keysToDelete
                    ) |> ignore

                    keysToDelete |> List.iter (fun key -> rootMap.delete key)

                    // Set all keys from the map
                    AMap.force amap
                    |> HashMap.iter (fun key value ->
                        match value with
                        | Some e ->
                            let yelement = elementToY e
                            rootMap.set(key, yelement) |> ignore
                        | None ->
                            rootMap.delete key
                    )
                )
            | _ -> failwith "Root element must be an AMap (object)"

    /// Dematerialize a Y.Doc's root map into an Element<string> tree
    let dematerialize (doc : Y.Doc) : Codec.Element<string> =
        let rootMap = doc.getMap()
        let mutable items = HashMap.empty<string, Codec.Element<string> option>

        rootMap.forEach (fun value key _ ->
            // Check if value is null/undefined
            if isNull (box value) then
                items <- HashMap.add key None items
            else
                // Convert the value directly to Codec.Element
                items <- HashMap.add key (Some (valueToElement value)) items
        ) |> ignore

        Codec.Element.AMap (AMap.ofHashMap items)