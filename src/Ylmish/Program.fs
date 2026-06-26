[<RequireQualifiedAccess>]
module Ylmish.Program

open Elmish
open FSharp.Data.Adaptive
open Yjs

open Ylmish.Adaptive.Codec

// module Unpersist =
    
//     let create (init : 'T -> 'AdaptiveT) (update : 'AdaptiveT -> 'T -> unit) = 
//         Adaptify.Unpersist.create init update

//     [<GeneralizableValue>]
//     let aval<'T> = Adaptify.Unpersist.aval<'T>
        
//     [<GeneralizableValue>]
//     let aset<'T> = Adaptify.Unpersist.aset<'T>
        
//     [<GeneralizableValue>]
//     let alist<'T> = Adaptify.Unpersist.alist<'T>

//     [<GeneralizableValue>]
//     let amap<'K, 'V> = Adaptify.Unpersist.amap<'K, 'V>

//     let inline instance< ^T, ^AdaptiveT when (^T or ^AdaptiveT) : (static member Unpersist : Unpersist< ^T, ^AdaptiveT >) > =
//         ((^T or ^AdaptiveT) : (static member Unpersist : Unpersist< ^T, ^AdaptiveT >) ())

type YlmishOptions<'model, 'amodel> = {
    Create : 'model -> 'amodel
    Update : 'amodel -> 'model -> unit
    Encode : Encoder<'amodel, Element<string>>
    Decode : Decoder<'model, Element<string>, 'model>
    Doc : Y.Doc
}

// let inline unpersist< ^model, ^amodel
//     when ^amodel: (static member Create : ^model -> ^amodel)
//     and  ^amodel: (static member Update : ^amodel -> ^model -> ^amodel)
// > = {
//     Create = fun model -> (^amodel : (static member Create: ^model -> ^amodel) model)
//     Update = fun model next -> (^amodel : (static member Update : ^amodel -> ^model -> ^amodel) (model, next))
// }


//let connect (amodel : 'amodel) (ydoc : Y.Doc) (encode : Encoder<'amodel>) (decode : Decoder<'amodel>) =
//    match encode amodel with
//    | Element.Map amap ->
//        let ymap = Y.Map.ofAdaptive amap
//    ()

type Message<'model, 'msg> =
    | Set of 'model
    | User of 'msg

[<GeneralizableValue>]
let withYlmish (options : YlmishOptions<'model, 'amodel>) (program: Program<'arg, 'model, 'msg, 'view>) =
    let mutable amodel : 'amodel option = None
    let mutable encoded : Encoded<Element<string>> option = None
    let mutable isWritingToYDoc = false
    let mutable currentModel : 'model option = None
    // Disposable for the connect attachments (collaborative-text roots).
    let mutable connectDisposable : System.IDisposable option = None

    let update userUpdate msg model =
        currentModel <- Some model
        match msg with
        | Set m ->
            match amodel with
            | Some am -> transact (fun () -> options.Update am m)
            | None -> invalidOp "withYlmish: amodel not initialized. init must run before update."
            m, Cmd.none
        | User userMsg ->
            let m, c = userUpdate userMsg model
            let c = c |> Cmd.map User
            match amodel with
            | Some am ->
                transact (fun () -> options.Update am m)
                match encoded with
                | Some enc ->
                    isWritingToYDoc <- true
                    try Y.Doc.materialize options.Doc enc
                    finally isWritingToYDoc <- false
                | None -> invalidOp "withYlmish: encoded not initialized. init must run before update."
            | None -> invalidOp "withYlmish: amodel not initialized. init must run before update."
            m, c

    let subs userSubscribe model =
        Sub.batch [
            userSubscribe model |> Sub.map "ylmish" User
            [ ["ylmish-ydoc"], fun dispatch ->
                let rootMap = options.Doc.getMap()
                let handler _ _ =
                    if not isWritingToYDoc then
                        let element = Y.Doc.dematerialize options.Doc
                        match currentModel with
                        | Some m ->
                            let decoded = Decode.run m options.Decode (AVal.constant (Some element))
                            match AVal.force decoded with
                            | Ok restoredModel -> dispatch (Set restoredModel)
                            | Error errors -> eprintfn "withYlmish: Y.Doc change could not be decoded, ignoring. %s" (Error.printAll errors)
                        | None -> eprintfn "withYlmish: Y.Doc change observed before model initialized, ignoring."
                rootMap.observeDeep handler
                { new System.IDisposable with
                    member _.Dispose() = rootMap.unobserveDeep handler }
            ]
            [ ["ylmish-text"], fun dispatch ->
                // Remote collaborative-text edits land on separate Y.Text roots
                // that the root-map observer above never sees. Observe the
                // connected text clists directly: when one changes (a remote CRDT
                // edit applied via attach), re-decode the live tree against the
                // *current* model — so `ask` preserves non-persisted fields — and
                // dispatch Set. This is the O(delta), model-preserving read path.
                let rec gather acc el =
                    match el with
                    | Element.Text chars -> chars :: acc
                    | Element.AMap m ->
                        AMap.force m
                        |> HashMap.fold (fun acc _ v -> match v with Some c -> gather acc c | None -> acc) acc
                    | Element.AList l ->
                        AList.force l
                        |> IndexList.toList
                        |> List.fold (fun acc v -> match v with Some c -> gather acc c | None -> acc) acc
                    | _ -> acc
                let textLeaves =
                    match encoded |> Option.bind AVal.force with
                    | Some el -> gather [] el
                    | None -> []
                let readback () =
                    if not isWritingToYDoc then
                        match currentModel, encoded with
                        | Some m, Some enc ->
                            match AVal.force (Decode.run m options.Decode enc) with
                            | Ok restored -> dispatch (Set restored)
                            | Error errors ->
                                eprintfn "withYlmish: text change could not be decoded, ignoring. %s" (Error.printAll errors)
                        | _ -> ()
                let disposables =
                    textLeaves
                    |> List.map (fun (chars : clist<char>) ->
                        // AddCallback emits an initial echo only for a non-empty
                        // list; skip exactly that one so the first *remote* edit
                        // on an initially-empty field still triggers a read-back.
                        let mutable initial = not (Seq.isEmpty chars)
                        chars.AddCallback (fun _ _ ->
                            if initial then initial <- false
                            else readback ()))
                { new System.IDisposable with
                    member _.Dispose() = for d in disposables do d.Dispose() }
            ]
        ]

    let init userInit arg =
        let m, c = userInit arg
        let am = options.Create m
        amodel <- Some am
        let enc = options.Encode am
        encoded <- Some enc

        // Connect collaborative-text leaves to their own Y.Text roots. This wires
        // bi-directional, identity-preserving delta sync so concurrent text edits
        // CRDT-merge (the #83 fix). Non-text fields stay on materialize below.
        connectDisposable <- Some (Y.Doc.connect options.Doc enc)

        // Check if Y.Doc already has state
        // (Yjs Fable bindings do not expose a size property on Y.Map, so we use forEach)
        let rootMap = options.Doc.getMap ()
        let mutable hasExistingState = false
        rootMap.forEach(fun _ _ _ -> hasExistingState <- true) |> ignore

        if hasExistingState then
            // Dematerialize existing Y.Doc state and decode it
            let element = Y.Doc.dematerialize options.Doc
            let decoded = Decode.run m options.Decode (AVal.constant (Some element))
            match AVal.force decoded with
            | Ok restoredModel ->
                currentModel <- Some restoredModel
                // connect has realized the adaptive graph, so updating the
                // amodel must happen inside a transaction.
                transact (fun () -> options.Update am restoredModel)
                restoredModel, Cmd.none
            | Error errors ->
                eprintfn "withYlmish: failed to decode existing Y.Doc state, falling back to initial model. %s" (Error.printAll errors)
                currentModel <- Some m
                Y.Doc.materialize options.Doc enc
                let c = c |> Cmd.map User
                m, c
        else
            // No existing state, materialize into Y.Doc
            currentModel <- Some m
            Y.Doc.materialize options.Doc enc
            let c = c |> Cmd.map User
            m, c

    let setState userSetState model dispatch =
        userSetState model (User >> dispatch)

    let view userView model dispatch =
        userView model (User >> dispatch)

    let termination (userTerminationPredicate, userTerminationAction) =
        (fun msg ->
            match msg with
            | Set _ -> false
            | User userMsg -> userTerminationPredicate userMsg
        ),
        (fun model ->
            connectDisposable |> Option.iter (fun d -> d.Dispose ())
            userTerminationAction model)

    program
    |> Program.map init update view setState subs termination

// module Yjs.Adaptive.Elmish

// module Codec = 
//     open Yjs

//     let (|Text|_|) (obj : obj) =
//         if typeof<Y.Text>.IsInstanceOfType(obj)
//         then Some (obj :?> Y.Text)
//         else None

//     let (|Value|_|) (obj : obj) =
//         if typeof<Y.AbstractType>.IsInstanceOfType(obj)
//         then Some (obj :?> Y.AbstractType)
//         else None

//     let (|Map|_|) (obj : obj) =
//         if typeof<Y.Map<obj>>.IsInstanceOfType(obj)
//         then Some (obj :?> Y.Map<obj>)
//         else None

// // Encode should go from an aval -> yval
// module Encode =
//     open FSharp.Data.Adaptive
//     open Yjs

//     [<RequireQualifiedAccess>]
//     type Encoding =
//         | Text of Y.Text
//         | Value of Y.AbstractType

//     // type Encoder<'a> = ()
//     [<RequireQualifiedAccess>]
//     type Encoder =
//         | Text of (Y.Text -> unit)
//         | Value of (Y.AbstractType -> unit)
//         | Array of (Y.Array<obj> -> unit)
//         | Map of (Y.Map<obj> -> unit)

//     let object (props : (string * (Encoder)) list) =
//         Encoder.Map <| fun init ->
//         for (key, encode) in props do
//             match encode, init.get key with
//             | Encoder.Text encode, Some (Codec.Text value) ->
//                 encode value
//             | Encoder.Text encode, None ->
//                 let value = Y.Text.Create ()
//                 encode value
//                 ignore <| init.set (key, value)
//             // | Encoder.Value encode, Some (Codec.Value value) ->
//             //     encode value
//             // | Encoder.Value encode, None ->
//             //     let value = Y.AbstractType.Create ()
//             //     encode value
//             //     ignore <| init.set (key, value)
//             ()

//     let value (a : 'a aval) = Encoder.Value <| fun (init : Y.AbstractType) ->
//         let boop = a.addc
//     let text (a : char alist) = Encoder.Text <| fun (init : Y.Text) -> ()

//     let array (item : 'a -> Encoder) (a : 'a alist) =
//         Encoder.Array <| fun (init : Y.Array<obj>) ->
//         ()


//     // Run


//     let from (key, doc : Y.Doc) = function
//         | Encoder.Text encode ->
//             encode <| doc.getText key
//         | Encoder.Map encode ->
//             encode <| doc.getMap key
//         | Encoder.Array encode ->
//             encode <| doc.getArray key



// // module Decode =
// //     open Yjs

// //     [<RequireQualifiedAccess>]
// //     type PathSegment = 
// //         | ObjectKey   of string
// //         | ArrayIndex  of int
// //         override x.ToString () =
// //             match x with
// //             | ObjectKey  k -> k
// //             | ArrayIndex i -> sprintf "[%i]" i

// //     type Path = PathSegment list

// //     [<RequireQualifiedAccess>]
// //     type Element =
// //         | Map of Y.Map<obj>
// //         | Array of Y.Array<obj>
// //         | Text of Y.Text
// //         | Value of obj

// //     module Element =
        
// //         type Kind =
// //             | MapKind
// //             | ArrayKind
// //             | TextKind

// //         let kind (element : Element) = 
// //             match element with
// //             | Element.Map _ -> MapKind
// //             | Element.Array _ -> ArrayKind
// //             | Element.Text _ -> TextKind

// //     type DecodingError =
// //         | NotConvertible of {| Path : Path; Actual : Element.Kind; Expected : Element.Kind list |}
// //         | MissingProperty of {| Path : Path |}

// //     type Decoder<'a, 'out> =  Path * 'a -> Result<'out, DecodingError>
// //     // type ValueDecoder<'out> = Path * Element -> Result<'out, DecodingError>

// //     module Decode =
// //         open FSharp.Data.Adaptive

// //         let root : Decoder<_,_> = Ok

// //         let inline run (nav : Decoder<_,_>) dep = nav dep

// //         /// Creates a Decoder<'a> from an 'a.
// //         let ok c : Decoder<_,_> = fun _ -> Ok c
        
// //         /// Creates a Decode<_> from an error.
// //         let error e : Decoder<_,_> = fun _ -> Error e

// //         let from (element : Element) : Decoder<_,_> = ok ([], element)

// //         let fromArray (a : Y.Array<obj>) = from (Element.Array a)

// //         let fromMap (a : Y.Map<obj>) = from (Element.Map a)

// //         /// Lift a Result<'a, Failure> to a Decode<'a>.
// //         let ofResult (result : Result<'a, DecodingError>) : Decoder<_,_> =
// //             fun _ -> result

// //         let bind (f : 'a -> Decoder<_,'b>) (a : Decoder<_,'a>): Decoder<_,'b> =
// //             fun dep -> a dep |> Result.bind (fun s -> f s dep)

// //         let map (f : 'a -> 'b) (a : Decoder<_,'a>) : Decoder<_, 'b> =
// //             a >> Result.map f

// //         let value (f : 'a -> Result<'b, DecodingError>) : Decoder<_, 'a> -> Decoder<_,'b> =
// //             bind (f >> ofResult)

// //         let optional f : Decoder<_,_> -> Decoder<_,_> =
// //             value <| fun (path, element) ->
// //                 match element with
// //                 | Some el -> f (path, el) |> Result.map Some
// //                 | None    -> Ok None

// //         let required f : Decoder<_,_> -> Decoder<_,_> =
// //             value <| fun (path, element) ->
// //                 match element with
// //                 | Some el -> f (path, el)
// //                 | None    -> Error <| MissingProperty {| Path = path |}

// //         let text : Decoder<Element, char clist> = fun (path, element) ->
// //             match element with
// //             | Element.Text text -> Ok ()
// //             | _ -> Error <| NotConvertible {|
// //                 Path = path
// //                 Actual = Element.kind element
// //                 Expected = [ Element.TextKind ] |}

// //         let object (_ : Decoder<_,'a>) : Decoder<_,cmap<string, 'a>> = fun (path, element) ->
// //             match element with
// //             | Element.Map map ->
// //                 ()
// //             | element -> Error <| NotConvertible {|
// //                     Path = path
// //                     Actual = Element.kind element
// //                     Expected = [ Element.ArrayKind ]
// //                 |}

// //         let array (item : Decoder<Element,'a>) : Decoder<Element, 'a clist> = fun (path, element) ->
// //             match element with
// //             | Element.Array array ->
// //                 ()
// //             | element -> Error <| NotConvertible {|
// //                     Path = path
// //                     Actual = Element.kind element
// //                     Expected = [ Element.ArrayKind ]
// //                 |}
        
// //         let string : Decoder<Element, string> = fun (path, element) ->
// //             match element with
// //             | Element.Value (:? string as value) -> Ok value
// //             | _ -> Error <| NotConvertible {|
// //                 Path = path
// //                 Actual = Element.kind element
// //                 Expected = [ Element.TextKind ] |}

// //         // Integral support like...
// //         // https://github.com/thoth-org/Thoth.Json/blob/main/src/Decode.fs#L160
                
// //         let key (_ : string) : Decoder<_,_> -> Decoder<_,_> =
// //             failwith "Not impl"

// //     type DecodeBuilder() =
// //         member _.Return x = Decode.ok x
// //         member _.Bind (m, f) = Decode.bind f m
// //         member _.ReturnFrom m = m
// //         member _.Zero() = Decode.ok ()
// //         member _.Run f = f


// //     let decode = DecodeBuilder()

// //     let (?) decode path = Decode.key path decode



// // module ExampleDecoding =
// //     open Decode
// //     open Yjs
// //     open FSharp.Data.Adaptive

// //     module V1 =
// //         type Thing = {
// //             name  : string cval
// //         }

// //         type Model = {
// //             foo : int cval
// //             bar : string cval
// //             things : Thing clist
// //         }

// //         module Thing =
// //             let decode : Decoder<Element, Thing> = decode {
// //                 let! model = Decode.fromMap <| doc.getMap "model"
// //             }

// //         let decode : Decoder<Element,Model> = decode {
// //             let! bar = Decode.root?bar |> Decode.required Decode.string
// //             let! things = Decode.root?things |> Decode.required (Decode.array Thing.decode)
// //             return {
// //                 foo = cval 1
// //                 bar = bar
// //                 things = things
// //             }
// //         }
    
// // module ExampleDecoding2 =
// //     open FSharp.Data.Adaptive
    
// //     let bloop (map : Yjs.Y.Map<obj>) : cmap<string, obj> =
// //         ()

// //     let bloop2 (map : cmap<string, obj>) =
// //         // each time the map changes, emit a new T
// //         // we could just break this into map -> T
// //         // then invoking that on each callback..
// //         map.AddCallback(fun init delta -> 
// //             let idk = delta |> HashMapDelta.toArray |> Array.head
// //             ())
// // module Decode =
// //     open Yjs
// //     open FSharp.Data.Adaptive

// //     type Decoder<'a> = Y.AbstractType -> 'a

// //     let string (value : Y.AbstractType) =
// //         match value with
// //         | Codec.Value value ->
// //             value.ToString ()

// //     type Getter (map : Y.Map<obj>) =
// //         member _.Field key decoder =
// //             match map.get key with
// //             | Some value -> decoder (value :?> Y.AbstractType) // but also just values
// //             | None -> invalidOp "TODO"

// //     let object (builder : Getter -> 'a) (obj : Y.AbstractType) =
// //         match obj with
// //         | Codec.Map map ->
// //             let getter = Getter map
// //             builder getter

// // module Observe =
// //     open Yjs

// //     type Observer<'a> = ('a -> unit) -> Y.AbstractType -> unit //idisposable

// //     let ofDecoder (decoder : Decode.Decoder<'a>) : Observer<'a> =
// //         fun update obj -> obj.observe (fun _ _ -> update <| decoder obj)

// //     let string = ofDecoder Decode.string
// //     // let object = ofDecoder << Decode.object

// //     type OGetter (update, map : Y.Map<obj>) =
// //         member _.Field key (observer : Observer<'a>) =
// //             match map.get key with
// //             | Some value ->
// //                 observer update (value :?> Y.AbstractType)
// //             | None -> invalidOp "TODO"
// //     let object (builder : OGetter -> 'a) : Observer<'a> =
// //         fun update obj ->
// //             match obj with
// //             | Codec.Map map ->
// //                 map.observe (fun _ _ ->
// //                     let getter = OGetter (update, map)
// //                     update <| builder getter
// //                 )


// // Decode should go from yval -> model
// // module ExampleDecode =
// //     open Yjs
// //     let decode (init : Sample.Model) =

// //         // root.observe (fun _ _ -> update <| fun get -> {
// //         //     bar = get.Field "bar" Decode.string
// //         //     foo = ""
// //         //     things = []
// //         // })
// //         Observe.object <| fun get -> {
// //             init with
// //                 bar = get.Field "bar" Observe.string
// //         }


