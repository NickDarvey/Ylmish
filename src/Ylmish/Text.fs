namespace Ylmish

// Plan 0002, Step 3 — Ylmish.Text, for real. Pure value type, no Yjs
// dependency. See doc/plans/0002-ylmish-redesign.md ("`Ylmish.Text`").

/// A single edit intent: at `At`, remove `Removed` characters, insert
/// `Inserted`. Internal: the binding runtime (Step 5) drains these into the
/// backing Y.Text as precise deltas.
type internal Splice = { At : int; Removed : int; Inserted : string }

/// Collaboratively-edited text: an immutable value carrying content plus
/// pending edit intent (splices), so the runtime can apply precise deltas to
/// the backing Y.Text rather than guessing by diffing strings.
///
/// Equality, comparison and hashing are by CONTENT ONLY — pending intent is
/// transport, not identity (plan 0002, open question 2). One consequence,
/// documented deliberately: an edit that leaves content identical (e.g.
/// replacing "a" with "a") is elided entirely — adaptive change propagation is
/// content-equality-driven, so a content-neutral intent could never reach the
/// doc anyway.
///
/// (A sealed class rather than a record: Adaptify introspects record fields
/// when generating adaptive wrappers and falls back to `obj` for a record
/// whose representation it cannot see across the assembly boundary; an opaque
/// class is passed through as a plain changeable value with its type intact —
/// see the plan 0002 Step 3 check-in.)
[<Sealed>]
type Text internal (content : string, pending : Splice list (* newest first *)) =
    member internal _.TContent = content
    member internal _.TPending = pending

    override _.Equals (o : obj) =
        match o with
        | :? Text as t -> content = t.TContent
        | _ -> false

    override _.GetHashCode () = content.GetHashCode ()

    interface System.IComparable with
        member _.CompareTo (o : obj) =
            match o with
            | :? Text as t -> compare content t.TContent
            | _ -> invalidArg "o" "cannot compare Ylmish.Text with a value of another type"

    override _.ToString () = content

[<RequireQualifiedAccess>]
module Text =

    // ---- internal: what the binding runtime (Step 5) uses ------------------

    let internal applySplice (content : string) (s : Splice) : string =
        content.Substring (0, s.At) + s.Inserted + content.Substring (s.At + s.Removed)

    /// Pending intents in application order (oldest first). Each applies to the
    /// content produced by the previous one, starting from the content at the
    /// last drain.
    let internal pending (t : Text) : Splice list = List.rev t.TPending

    /// The runtime consumed the intents (flushed them into the Y.Text).
    let internal drain (t : Text) : Text = Text (t.TContent, [])

    // ---- construction -------------------------------------------------------

    let empty : Text = Text ("", [])

    /// Intent-free text (initial state, or a value decoded from the doc).
    let ofString (value : string) : Text =
        Text ((if isNull value then "" else value), [])

    let toString (text : Text) : string = text.TContent

    let length (text : Text) : int = text.TContent.Length

    // ---- intent-carrying edits (what update functions use) -----------------
    //
    // Positions are clamped into range rather than throwing: an out-of-range
    // edit inside an Elmish `update` must not crash the loop, and clamping is
    // pinned by tests so it is a contract, not an accident. Content-neutral
    // edits are elided (see the type's doc comment).

    let private push (s : Splice) (text : Text) : Text =
        if s.Removed = 0 && s.Inserted = "" then text
        else
            let next = applySplice text.TContent s
            if next = text.TContent then text
            else Text (next, s :: text.TPending)

    let insert (at : int) (value : string) (text : Text) : Text =
        let value = if isNull value then "" else value
        let at = max 0 (min at text.TContent.Length)
        push { At = at; Removed = 0; Inserted = value } text

    let remove (at : int) (count : int) (text : Text) : Text =
        let at = max 0 (min at text.TContent.Length)
        let count = max 0 (min count (text.TContent.Length - at))
        push { At = at; Removed = count; Inserted = "" } text

    let replace (at : int) (count : int) (value : string) (text : Text) : Text =
        let value = if isNull value then "" else value
        let at = max 0 (min at text.TContent.Length)
        let count = max 0 (min count (text.TContent.Length - at))
        push { At = at; Removed = count; Inserted = value } text

    /// Convenience for a plain <input>/<textarea> onChange: derives a single
    /// splice from the old and new values by common prefix/suffix diff.
    /// Minimal for a single contiguous edit (L3); ambiguous for repeated
    /// characters (convergence unaffected, interleaving fidelity slightly
    /// coarser) — use insert/remove/replace when the edit position is known.
    let edit (newValue : string) (text : Text) : Text =
        let newValue = if isNull newValue then "" else newValue
        let oldValue = text.TContent
        if oldValue = newValue then text
        else
            let maxAffix = min oldValue.Length newValue.Length
            let mutable p = 0
            while p < maxAffix && oldValue.[p] = newValue.[p] do
                p <- p + 1
            let mutable s = 0
            while s < maxAffix - p
                  && oldValue.[oldValue.Length - 1 - s] = newValue.[newValue.Length - 1 - s] do
                s <- s + 1
            push
                { At = p
                  Removed = oldValue.Length - p - s
                  Inserted = newValue.Substring (p, newValue.Length - p - s) }
                text
