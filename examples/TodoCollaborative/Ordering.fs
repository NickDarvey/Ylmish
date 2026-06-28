module TodoCollaborative.Ordering

open Fable.Core

// Prioritising the list is the CONSUMER's concern, not Ylmish's. A todo's position
// is a *fractional-index* string key: reordering an item sets a new key strictly
// between its neighbours' keys, so two peers reordering *different* items each make
// one independent field write that merges, and the displayed list is recovered by
// sorting on the key. Ylmish only syncs the key (a per-item value); it does not
// own ordering. We use the small, well-trodden `fractional-indexing` library.

[<Import("generateKeyBetween", "fractional-indexing")>]
let private generateKeyBetween (_a : string) (_b : string) : string = jsNative

/// A key strictly between `prev` and `next` (either open-ended as `None`):
/// `keyBetween None None` is the first key, `keyBetween (Some last) None` appends.
/// `null` is the library's open-ended sentinel (F# `None` would marshal to
/// `undefined`, which it rejects).
let keyBetween (prev : string option) (next : string option) : string =
    let sentinel = function Some s -> s | None -> null
    generateKeyBetween (sentinel prev) (sentinel next)
