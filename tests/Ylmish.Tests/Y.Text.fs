module Ylmish.Y.Text

open FSharp.Data.Adaptive
open Yjs

open Ylmish

#if FABLE_COMPILER
open Fable.Mocha
#else
open Expecto
#endif

let tests = testList "Y.Text" [
    testList "ofAdaptive" [
        test "ofAdaptive (initialisation)" {
            let atext = clist [ 'a'; 'b'; 'd' ]
            let ydoc = Y.Doc.Create ()
            let ytext = Y.Text.ofAdaptive atext
            let _ = ydoc.getMap("container").set("test", ytext)

            Expect.equal (System.String.Concat atext) "abd" "atext doesn't equal expected value"
            Expect.equal (ytext.toString()) "abd" "ytext doesn't equal expected value"
        }

        test "ofAdaptive, atext_InsertAt(): given \"abd\", insert 'c' to give \"abcd\"" {
            let atext = clist [ 'a'; 'b'; 'd' ]
            let ydoc = Y.Doc.Create ()
            let ytext = Y.Text.ofAdaptive atext
            let _ = ydoc.getMap("container").set("test", ytext)

            let _ = transact (fun () -> atext.InsertAt (2, 'c'))

            Expect.equal (System.String.Concat atext) "abcd" "atext doesn't equal expected value"
            Expect.equal (ytext.toString()) "abcd" "ytext doesn't equal expected value"
        }

        test "ofAdaptive, atext_InsertAt(): given \"abd\", insert 'c' to give \"abcd\", then insert X to give \"abcXd\"" {
            let atext = clist [ 'a'; 'b'; 'd' ]
            let ydoc = Y.Doc.Create ()
            let ytext = Y.Text.ofAdaptive atext
            let _ = ydoc.getMap("container").set("test", ytext)

            let _ = transact (fun () -> atext.InsertAt (2, 'c'))

            Expect.equal (System.String.Concat atext) "abcd" "atext doesn't equal expected value"
            Expect.equal (ytext.toString()) "abcd" "ytext doesn't equal expected value"

            let _ = transact (fun () -> atext.InsertAt (3, 'X'))

            Expect.equal (System.String.Concat atext) "abcXd" "atext doesn't equal expected value"
            Expect.equal (ytext.toString()) "abcXd" "ytext doesn't equal expected value"
        }

        test "ofAdaptive, ytext_insert(): given \"abd\", insert 'c' to give \"abcd\"" {
            let atext = clist [ 'a'; 'b'; 'd' ]
            let ydoc = Y.Doc.Create ()
            let ytext = Y.Text.ofAdaptive atext
            let _ = ydoc.getMap("container").set("test", ytext)

            let _ = ytext.insert(2, "c")

            Expect.equal (System.String.Concat atext) "abcd" "atext doesn't equal expected value"
            Expect.equal (ytext.toString()) "abcd" "ytext doesn't equal expected value"
        }
    ]

    testList "toAdaptive" [
        test "toAdaptive, (initialisation)" {
            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "abd")
            let atext = Y.Text.toAdaptive ytext

            Expect.equal (System.String.Concat atext) "abd" "atext doesn't equal expected value"
            Expect.equal (ytext.toString()) "abd" "ytext doesn't equal expected value"
        }

        test "toAdaptive, ytext_insert()" {
            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let atext = Y.Text.toAdaptive ytext

            let _ = ytext.insert(0, "abd")
            let _ = ytext.insert(2, "c")

            Expect.equal (System.String.Concat atext) "abcd" "atext doesn't equal expected value"
            Expect.equal (ytext.toString()) "abcd" "ytext doesn't equal expected value"
        }
    ]

    testList "attachDecode (decode-only)" [
        test "attachDecode observes Y.Text changes and updates adaptive clist" {
            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "abc")

            let atext : char clist = ytext.toString () :> _ seq |> clist
            let active = ref false
            let _ = Y.Text.attachDecode active atext ytext

            // Insert from Yjs side should be reflected in adaptive
            let _ = ytext.insert(3, "def")

            Expect.equal (System.String.Concat atext) "abcdef" "atext should reflect Y.Text changes"
            Expect.equal (ytext.toString()) "abcdef" "ytext should have expected value"
        }

        test "attachDecode does not sync Adaptive→Yjs (encode direction is not attached)" {
            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "abc")

            let atext : char clist = ytext.toString () :> _ seq |> clist
            let active = ref false
            let _ = Y.Text.attachDecode active atext ytext

            // Insert from adaptive side should NOT be reflected in ytext
            let _ = transact (fun () -> atext.InsertAt(3, 'X'))

            Expect.equal (System.String.Concat atext) "abcX" "atext should have the inserted character"
            Expect.equal (ytext.toString()) "abc" "ytext should NOT have the character (encode not attached)"
        }
    ]

    testList "attachEncode (encode-only)" [
        test "attachEncode observes adaptive changes and updates Y.Text" {
            let ydoc = Y.Doc.Create ()
            let atext = clist [ 'a'; 'b'; 'c' ]
            let ytext = Y.Text.Create (System.String.Concat atext)
            let _ = ydoc.getMap("container").set("test", ytext)

            let active = ref false
            let _ = Y.Text.attachEncode active atext ytext

            // Insert from adaptive side should be reflected in Yjs
            let _ = transact (fun () -> atext.InsertAt(3, 'd'))

            Expect.equal (System.String.Concat atext) "abcd" "atext should have expected value"
            Expect.equal (ytext.toString()) "abcd" "ytext should reflect adaptive changes"
        }

        test "attachEncode does not sync Yjs→Adaptive (decode direction is not attached)" {
            let ydoc = Y.Doc.Create ()
            let atext = clist [ 'a'; 'b'; 'c' ]
            let ytext = Y.Text.Create (System.String.Concat atext)
            let _ = ydoc.getMap("container").set("test", ytext)

            let active = ref false
            let _ = Y.Text.attachEncode active atext ytext

            // Insert from Yjs side should NOT be reflected in atext
            let _ = ytext.insert(3, "X")

            Expect.equal (System.String.Concat atext) "abc" "atext should NOT have the character (decode not attached)"
            Expect.equal (ytext.toString()) "abcX" "ytext should have the inserted character"
        }
    ]

    testList "attach (bi-directional helper)" [
        test "attach synchronizes both directions with shared reentrancy guard" {
            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "abc")

            let atext : char clist = ytext.toString () :> _ seq |> clist
            let active = ref false
            use disposable = Y.Text.attach active atext ytext

            // Insert from Yjs side
            let _ = ytext.insert(3, "X")
            Expect.equal (System.String.Concat atext) "abcX" "atext should reflect Y.Text change"

            // Insert from adaptive side
            let _ = transact (fun () -> atext.InsertAt(4, 'Y'))
            Expect.equal (ytext.toString()) "abcXY" "ytext should reflect adaptive change"

            // Dispose should work correctly
            disposable.Dispose()
        }

        test "attach prevents feedback loops with shared reentrancy guard" {
            let ydoc = Y.Doc.Create ()
            let ytext = ydoc.getText "test"
            let _ = ytext.insert(0, "a")

            let atext : char clist = ytext.toString () :> _ seq |> clist
            let active = ref false
            let _ = Y.Text.attach active atext ytext

            // This should not cause infinite loop
            let _ = transact (fun () -> atext.InsertAt(1, 'b'))

            Expect.equal (System.String.Concat atext) "ab" "atext should have expected value"
            Expect.equal (ytext.toString()) "ab" "ytext should have expected value"
        }
    ]
]
