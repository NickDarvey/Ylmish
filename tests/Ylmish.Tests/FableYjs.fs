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
]
