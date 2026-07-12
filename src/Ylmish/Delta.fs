/// The binding's list machinery: apply an Adaptive IndexListDelta to a Yjs
/// sequence (Y.Array / Y.Text) as position-based inserts and deletes,
/// coalescing contiguous runs. This is how `Encode.list` ships local list
/// changes as O(delta) index operations.
module internal Ylmish.Internal.Delta

open FSharp.Data.Adaptive

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
