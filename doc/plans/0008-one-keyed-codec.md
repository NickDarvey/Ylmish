# 0008 — One keyed codec: element-wise `map`, value `sequence`, and uniform migration

**This supersedes the original 0008 framing** (a migration module with two
backends). The redirect: the reason migration needed two backends is that the
codec has *two field worlds* — typed `Decode.object` fields vs the stringly
`CollectionItem`. Collapse them into **one**, and migration becomes a single
uniform layer that falls out almost for free.

The target: **the model's type is the merge choice.** A collection you edit
per-item is a `HashMap<key, Record>` (`Encode.map`, element-wise, keyed by the map
key). A keyless list of values is an `IndexList<Value>` (`Encode.sequence`, a CRDT
sequence). A record is `Encode.object`. Collection items are **just objects** — the
same `Encode.object` / `Decode.object` DSL, typed, with no `CollectionItem`, no
`merged` cell, and no `"id"` string argument.

Parent/supersedes: 0007 (`Encode.collection` + `CollectionItem` + the minimal
helpers become obsolete here). Completes 0005 (the binding exposes its merged
value — see Step 1).

## State

**Last updated:** 2026-06-28 · **Status: PROPOSAL (direction concurred; steps
below).** Large — phased so each phase is independently shippable; later phases may
split into their own plans if they destabilise the core.

### Progress

- [x] **Step 0** — Spikes done (both confirmed; throwaway code removed). (a)
  adaptify turns `HashMap<string,'V>` into `amap<string, Adaptive'V>`; (b) a binding
  can own + expose its merged value and the decoder read it off `Element.Custom`,
  converging two-doc with no consumer cell. See *Decisions*.
- [ ] **Step 1** — `Encode.map` / `Decode.map`, **element-wise**, keyed by the
  `amap` key; items encoded/decoded via the object DSL; per-item scalars LWW,
  per-item text CRDT (roots auto-named `<map>/<key>/<field>`). Internal merged
  value read off the element — **no consumer cell**. Port the `Codec.Collection`
  merge tests.
- [ ] **Step 2** — **Cut over**: rewrite the todo example to `HashMap` +
  `Encode.map`; delete `Encode.collection`, `CollectionItem`, and its helpers.
  Keep the two-peer `withYlmish` tests green (liveness via the existing
  `afterTransaction` read-back).
- [ ] **Step 3** — `Encode.sequence` / `Decode.sequence` for keyless **value**
  lists (a `Y.Array` of values; concurrent add/remove/reorder merge). Tests.
- [ ] **Step 4** — **Flatten nested object scalars**: nested `Encode.value` becomes
  a path-keyed root-map entry (auto-named), so nested scalars merge per-key; add
  `Encode.atomic` for the whole-subtree-LWW opt-in. (Riskiest — touches
  `materialize`/`dematerialize`; may split out.)
- [ ] **Step 5** — **Uniform migration vocabulary**: typed `Field`
  (`Field.string`/`bool`/`int`, `renamedFrom`, `defaultsTo`, `migrate`) over
  `Decode.object` / `Encode.object`. Because map items are objects, it covers
  collections too. Replace the example's hand-rolled rename; test an object-field
  rename + a value transform.
- [ ] **Step 6** — Docs: README merge-semantics rewritten around the taxonomy
  table; example walkthrough updated; the "model type is the merge choice" and
  migration-vocabulary stories.

### Decisions & lessons

- **Step 0(a) — adaptify maps `HashMap` to a keyed `amap` (confirmed).**
  `HashMap<string, Sub>` generates a `FSharp.Data.Traceable.ChangeableModelMap`
  exposed as **`amap<string, AdaptiveSub>`**, and `Update` does a keyed reconcile
  (`_Items_.Update(value.Items)`). So `Encode.map` takes `amap<string, Adaptive'V>`
  + an item *object* codec; the map key is the identity. Crucially — unlike the
  positional `IndexList` delta (0006 Step 1) — a map's delta is **keyed by
  construction**, so there is no positional-identity hazard for maps at all. (Map
  keys are `string`; non-string keys need a key codec — out of scope.)
- **Step 0(b) — cell-free decode works (confirmed two-doc).** A binding can own its
  merged value internally and expose it; the decoder reads it straight off
  `Element.Custom` (spike used a side interface + downcast; Step 1 will instead add
  `Value` to `CustomElement`, completing 0005). Two peers writing concurrently
  converged and both read the merged value off their element — **no consumer-threaded
  `merged` cell**. Liveness under `withYlmish` rides the existing `afterTransaction`
  read-back (0006 Step 5).

### Agent pickup prompt

> You are executing plan 0008. Settle Step 0 first (adaptify `HashMap`; the
> cell-free decode via the element's exposed merged value). Then work in order, one
> green commit each (`npm test`). The end state: collection items are ordinary
> objects through `Encode.object`/`Decode.object`; `Encode.map` is keyed by the
> model's map key; `Encode.sequence` covers keyless value lists; migration is a
> single typed vocabulary over the object DSL. Keep the Elmish loop and codec
> readable.

## The target, concretely

The taxonomy — pick the structure that matches the merge you want:

| Model field | Combinator | Merge | Per-item edit? | Use |
|---|---|---|---|---|
| `HashMap<K, Record>` | `Encode.map` | element-wise, keyed | **yes** | entities you edit (todos, comments) |
| `IndexList<Value>` | `Encode.sequence` | CRDT sequence (add/remove/reorder) | no (whole values) | tags, log lines, ordered scalars |
| `IndexList<_>` | `Encode.list` | whole-container LWW | n/a | small / uncontended |
| `Record` | `Encode.object` | per-field (flat) | — | nested records |
| any field | `Encode.atomic` | one LWW unit (opt-in) | — | replace-as-a-whole |

The codec that produces it — an item is just an object, the map needs no `"id"`:

```fsharp
[<ModelType>] type Comment = { Body : string; Done : bool }     // key lives in the map
[<ModelType>] type Model   = { Title: string; Body: string; Author: Author
                               Comments : HashMap<string, Comment> }

let encodeComment (c : AdaptiveComment) = Encode.object [
    "done", c.Done |> Encode.value id     // typed bool, LWW
    "body", c.Body |> Encode.text         // CRDT text
]
let decodeComment = Decode.object {
    let! isDone = Decode.object.required "done" Decode.value
    let! body   = Decode.object.required "body" Decode.text
    return { Done = isDone; Body = body }
}

let encode (m : AdaptiveModel) = Encode.object [
    "title",    m.Title |> Encode.value id
    "body",     m.Body  |> Encode.text
    "author",   encodeAuthor m.Author
    "comments", Encode.map encodeComment m.Comments        // keyed by the map key
]
let decode = Decode.object {
    let! title    = Decode.object.required "title"    Decode.value
    let! body     = Decode.object.required "body"     Decode.text
    let! author   = Decode.object.required "author"   decodeAuthor
    let! comments = Decode.object.required "comments" (Decode.map decodeComment)
    return { Title = title; Body = body; Author = author; Comments = comments }
}
```

No `CollectionItem`, no `merged` cell, no `make ()`, no string conversions.

## The two design pieces behind the clean surface

1. **Items are objects (the unify).** `Encode.map` runs each value through the item
   *object* encoder and keys the element-wise `Y.Map` (+ per-item text roots) off
   the **map key** — the binding logic 0007 already has, fed by an `Element` item
   tree instead of a flat `CollectionItem`. Per-item text roots are auto-named
   `<map>/<key>/<field>` from the item's `Encode.text` position (the `Scheme` walk,
   keyed by the map key — stable, so it survives reorder; the 0004 result).

2. **No consumer cell (completes 0005).** Today `Decode.collection` reads a
   `cval` the consumer threads. Instead the binding keeps its merged value
   internally and **exposes it on `Element.Custom`** (0005's `abstract Value`);
   `Decode.map` reads it off the element, like `Decode.object` reads its tree.
   Liveness under `withYlmish` already works via the `afterTransaction` read-back
   (0006 Step 5).

## Why migration then falls out

Once map items are objects, **every field everywhere decodes through
`Decode.object`** — one world. A rename is read-both + dual-write *at a key*,
expressed once as a typed `Field` and reused for object fields and map items alike
(Step 5). No second backend, because there is no second representation. The
`done`→`completed` example collapses to one `Field.bool "completed" |>
Field.renamedFrom "done"`.

`Encode.sequence` (keyless values) sits slightly apart — there are no fields to
migrate, only whole values — which is correct: migration is a *field* concern.

## Scope / non-goals

- **In:** `Encode.map`/`Decode.map` (element-wise, keyed by the model map key);
  items via the object DSL; cell-free decode (binding exposes its value);
  `Encode.sequence`; `Encode.atomic`; flatten nested scalars; the typed migration
  vocabulary; rewrite the example; docs.
- **Out:** non-string map keys (flag as a follow-on — needs a key codec); a general
  version-branching framework; automatic one-shot data rewrites; the
  per-`Encode.object` uniform-value-type wart (separate cleanup).

## Open questions

- **Phasing.** Step 4 (flatten nested scalars — touches the core `materialize`
  path) is the riskiest and is separable from the map/migration core; ship it last
  or split it into its own plan if it destabilises?
- **`Encode.list` keep or drop?** Once `map`/`sequence`/`atomic` exist, is
  whole-container-LWW `Encode.list` still worth keeping, or does `atomic` cover it?
- **Version-tag escape hatch** for changes too structural for tolerant read-both —
  minimal form, or tolerant-only + document the limit? Decide at Step 5.
