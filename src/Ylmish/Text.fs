namespace Ylmish

// Plan 0002, Step 2b — target API skeleton. Signatures are the deliverable;
// bodies are stubs until Step 3. See doc/plans/0002-ylmish-redesign.md
// ("`Ylmish.Text`").

/// Collaboratively-edited text: an immutable value carrying content plus
/// pending edit intent (splices), so the runtime can apply precise deltas to
/// the backing Y.Text rather than guessing by diffing strings.
///
/// Equality and comparison are by CONTENT ONLY — pending intent is transport,
/// not identity (plan 0002, open question 2). The runtime drains intents when
/// flushing to the backing Y.Text and returns intent-free values on the way
/// back in.
type Text =
    // Step 3 replaces this stub with content + pending intents, keeping
    // content-only structural equality.
    private
    | TextStub

[<RequireQualifiedAccess>]
module Text =
    let private nyi () : 'a = failwith "plan 0002: Ylmish.Text is implemented in Step 3"

    let empty : Text = TextStub

    let ofString (value : string) : Text = nyi ()
    let toString (text : Text) : string = nyi ()
    let length (text : Text) : int = nyi ()

    // Intent-carrying edits — what Elmish update functions use.

    let insert (at : int) (value : string) (text : Text) : Text = nyi ()
    let remove (at : int) (count : int) (text : Text) : Text = nyi ()
    let replace (at : int) (count : int) (value : string) (text : Text) : Text = nyi ()

    /// Convenience for a plain <input>/<textarea> onChange: derives a single
    /// splice from the old and new values by common prefix/suffix diff.
    /// Ambiguous for repeated characters (documented; convergence unaffected);
    /// the affix-diff strategy is validated minimal by lesson L3.
    let edit (newValue : string) (text : Text) : Text = nyi ()
