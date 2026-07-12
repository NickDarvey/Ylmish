# TODO

1. **Pin (and if needed fix) `Encode.option` Some→None inside a replaced keyed-map item.**

   When a keyed-map item's value is replaced wholesale (any one-field record edit
   replaces the item's whole `Encoded`), the binding disposes the old item
   attachment, re-flushes the new encoding into the adopted containers, and
   re-attaches. The flush path never *deletes* keys — an option field that
   transitioned Some→None as part of that same replacement relies on the fresh
   option attachment's transition callback to delete the backing key, and that
   callback's initial-skip may swallow the transition. Nothing exercises
   option-inside-map-item today (noted at plan 0002 Step 10). Write the test
   first: add an optional field to a keyed item's record, flip it `Some x` →
   `None` via a normal item edit, and assert the key disappears from the item's
   `Y.Map` on both peers.
