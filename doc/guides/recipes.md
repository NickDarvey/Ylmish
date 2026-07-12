# Recipes

Patterns built from plain public combinators — nothing here needs a special
module. Every code block is a verbatim excerpt of compiled code; the source
file is named above each block.

## Offline-creatable entities: app-minted keys

**The rule: anything creatable offline needs a unique key.** Ylmish accepts
one honest limitation from Yjs: if two offline peers create the *same* nested
container (same key) and then sync, one creation wins wholesale. The design
answer is not a runtime warning — it is a modelling rule: entities live in
`Encode.map`, keyed by an id the app mints at creation (a GUID, a nanoid,
whatever). Different keys never conflict, so concurrent creation is safe by
construction; per-item fields then merge per their own encodings.

The example app's acceptance test for exactly this
([`tests/Ylmish.Tests/TodoCollaborative.fs`](../../tests/Ylmish.Tests/TodoCollaborative.fs)):

<!-- sample: concurrent-adds -->
```fsharp
test "concurrent adds from both peers both survive (issue #83's class, at the example level)" {
    let d1 = Y.Doc.Create ()
    let d2 = Y.Doc.Create ()
    use p1 = Elmish.Program.test (Main.makeProgram d1)
    use p2 = Elmish.Program.test (Main.makeProgram d2)

    // Offline: both peers add concurrently, under their own unique keys.
    p1.Dispatch (user (AddTodo ("id-1", "From peer 1", 1.0)))
    p2.Dispatch (user (AddTodo ("id-2", "From peer 2", 2.0)))
    syncBoth d1 d2

    Expect.equal p1.Model.Todos p2.Model.Todos "models converge"
    Expect.equal (titles p1.Model) [ "From peer 1"; "From peer 2" ]
        "NEITHER add was lost — the failure mode issue #83 reported"
}
```

## Ordering: a fractional index, not a structural move

Identity and order are different things: the map key is the entity's
**immutable identity**; display order is **mutable data** — a plain numeric
field, last-writer-wins like any register. To reorder, write a number between
the neighbours' numbers. To move to the top, write something below the current
minimum.

Why not move items in a list? A structural "move" is delete-here + insert-there,
and two peers moving the same item concurrently can duplicate it (each peer's
delete pairs with the *other's* insert). A fractional index cannot duplicate
anything: a reorder is one register write, and concurrent reorders just race
deterministically. The demo stages this live.

The shape, from the demo model
([`examples/TodoCollaborative/Model.fs`](../../examples/TodoCollaborative/Model.fs)):

<!-- sample: todo-record -->
```fsharp
/// One todo. A record of independent registers plus a collaborative note:
/// because the codec encodes each field separately (see Codec.fs), concurrent
/// edits to DIFFERENT fields of the same todo merge per field — and concurrent
/// edits to the SAME note merge as text. `Order` is a fractional index —
/// reordering writes a number instead of moving structure, so concurrent
/// reorders converge without duplication.
type Todo = { Title : string; Done : bool; Order : float; Note : Text }
```

The demo uses a bare `float` and midpoints (`0.5` to move above `1.0`). Real
apps eventually want string-based fractional indices (the
[`fractional-indexing`](https://www.npmjs.com/package/fractional-indexing)
scheme) to avoid float exhaustion under repeated reordering — that is consumer
policy, and it rides `Encode.string` exactly the same way.

## Rolling migration: dual-key read/write

Schema changes must tolerate a fleet of clients on mixed versions sharing one
doc. The recipe for renaming a key (here `"title"` → `"heading"`) is plain
combinators, no framework:

- **v2 writes both keys** (new shape plus the old key for v1 readers), and
  **reads new-or-old, preferring new**.
- **v1 keeps working untouched** — and because the binding never deletes keys
  it doesn't mention, v1 clients cannot destroy the new key they don't
  understand.

From the compatibility test
([`tests/Ylmish.Tests/Program.fs`](../../tests/Ylmish.Tests/Program.fs)):

<!-- sample: migration-dual-key -->
```fsharp
let mkV2 (doc : Y.Doc) =
    let heading = cval ""
    let enc =
        Encode.object [
            "heading", Encode.string heading   // the new shape
            "title", Encode.string heading     // dual-write for v1 readers
        ]
    let att = Binding.attach doc enc
    heading, enc, att
let decodeV2 : Decoder<string, string> =
    Decode.object {
        let! newKey = Decode.object.optional "heading" Decode.string
        let! oldKey = Decode.object.optional "title" Decode.string
        // Read new-or-old, prefer new.
        return
            match newKey, oldKey with
            | Some h, _ -> h
            | None, Some t -> t
            | None, None -> ""
    }
```

(The test drives these through the internal binding layer directly; in an app
the same `enc`/`decodeV2` pair goes into `withYlmish`'s `Encode`/`Decode`
options unchanged.)

Roll-out proceeds in the usual three phases: ship v2 (dual read/write) →
wait out v1 clients → ship v3 that drops the `"title"` key from its encoder.
Old docs keep their stale `"title"` value; nothing reads it, and any v3 client
editing the field simply stops maintaining it.

## App-only state

Anything the codec doesn't mention stays local: keep UI state (filters,
selections, half-typed drafts) in the model, out of the codec, and use
`Decode.ask` + `{ model with ... }` so remote updates carry it through. The
walkthrough is in [codec.md](codec.md); the demo shows a draft that never
syncs and never appears in the doc.
