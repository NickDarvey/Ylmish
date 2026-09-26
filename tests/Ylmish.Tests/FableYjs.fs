module Ylmish.Tests.FableYjs

// The hand-maintained corners of the Fable.Yjs bindings: that each member is
// typed the way the JS it binds actually calls or returns, observed at runtime
// rather than read off a signature.

open Yjs

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

type private Doc = Yjs.Utils.Doc.Doc

let tests = testList "Fable.Yjs bindings" [
    testList "Doc.onUpdate / offUpdate" [
        test "the handler receives the update and the transaction's origin" {
            let doc : Doc = Y.Doc.Create ()
            let seen = ResizeArray ()
            let h = Y.UpdateHandler (fun update origin d _ -> seen.Add (update, origin, d))
            doc.onUpdate h
            let origin = obj ()
            doc.transact ((fun _ -> (doc.getMap () : Y.Map<obj>).set ("k", box "v") |> ignore), origin)
            Expect.equal seen.Count 1 "one content-changing transaction, one call"
            let update, o, d = seen.[0]
            Expect.isTrue (obj.ReferenceEquals (o, origin)) "origin is the token the transaction was tagged with"
            Expect.isTrue (obj.ReferenceEquals (d, doc)) "doc is the doc that changed"
            // The update is a real V1 update: applying it elsewhere reproduces the write.
            let other : Doc = Y.Doc.Create ()
            Y.applyUpdate (other, update)
            Expect.equal ((other.getMap () : Y.Map<obj>).get "k") (Some (box "v")) "the update carries the write"
        }

        test "offUpdate with the same handler stops delivery" {
            let doc : Doc = Y.Doc.Create ()
            let mutable calls = 0
            let h = Y.UpdateHandler (fun _ _ _ _ -> calls <- calls + 1)
            doc.onUpdate h
            (doc.getMap () : Y.Map<obj>).set ("a", box 1.) |> ignore
            doc.offUpdate h
            (doc.getMap () : Y.Map<obj>).set ("b", box 2.) |> ignore
            Expect.equal calls 1 "only the write before offUpdate was delivered"
        }
    ]

    // What a doc hands back is `obj` until something looks. Each `tryOf` must
    // answer for its own class (subclasses included, which is what `instanceof`
    // means) and refuse everything else — a sibling Y type, a plain value, and
    // `null`, the three shapes a garbled shared doc actually holds.
    testList "tryOf" [
        test "Text.tryOf answers a Y.Text as itself" {
            let text = Y.Text.Create "t"
            Expect.isTrue (Y.Text.tryOf (box text) |> Option.exists (fun t -> obj.ReferenceEquals (t, text))) "the same text"
        }
        test "Text.tryOf answers a Y.XmlText, which extends Y.Text" {
            Expect.isSome (Y.Text.tryOf (box (Y.XmlText.Create ()))) "a subclass is a Y.Text"
        }
        test "Text.tryOf refuses a Y.Map" {
            Expect.isNone (Y.Text.tryOf (box (Y.Map.Create () : Y.Map<obj>))) "a map is not text"
        }
        test "Text.tryOf refuses a plain string" {
            Expect.isNone (Y.Text.tryOf (box "t")) "a string is not a Y.Text"
        }
        test "Text.tryOf refuses null" {
            Expect.isNone (Y.Text.tryOf null) "null is nothing"
        }
        test "Map.tryOf answers a Y.Map as itself" {
            let map : Y.Map<obj> = Y.Map.Create ()
            Expect.isTrue (Y.Map.tryOf (box map) |> Option.exists (fun m -> obj.ReferenceEquals (m, map))) "the same map"
        }
        test "Map.tryOf refuses a Y.Text" {
            Expect.isNone (Y.Map.tryOf (box (Y.Text.Create ()))) "text is not a map"
        }
        test "Map.tryOf refuses a plain object" {
            Expect.isNone (Y.Map.tryOf (Fable.Core.JsInterop.createObj [ "k", box 1. ])) "a plain object is not a Y.Map"
        }
        test "Array.tryOf answers a Y.Array" {
            Expect.isSome (Y.Array.tryOf (box (Y.Array.Create () : Y.Array<obj>))) "an array"
        }
        test "Array.tryOf refuses a Y.Map" {
            Expect.isNone (Y.Array.tryOf (box (Y.Map.Create () : Y.Map<obj>))) "a map is not an array"
        }
        test "XmlFragment.tryOf answers a Y.XmlElement, which extends Y.XmlFragment" {
            Expect.isSome (Y.XmlFragment.tryOf (box (Y.XmlElement.Create "p"))) "a subclass is a fragment"
        }
        test "XmlFragment.tryOf refuses a Y.Text" {
            Expect.isNone (Y.XmlFragment.tryOf (box (Y.Text.Create ()))) "text is not a fragment"
        }
    ]

    testList "Lib0.Buffer" [
        test "an update survives a base64 round-trip and still applies" {
            let a : Doc = Y.Doc.Create ()
            (a.getText "t").insert (0, "héllo ✓")
            let wire : string = Lib0.Buffer.toBase64 (Y.encodeStateAsUpdate a)
            let b : Doc = Y.Doc.Create ()
            Y.applyUpdate (b, Lib0.Buffer.fromBase64 wire)
            Expect.equal ((b.getText "t").toString ()) "héllo ✓" "the peer reads what was written"
        }
    ]
]
