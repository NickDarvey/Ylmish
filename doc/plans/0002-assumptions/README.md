# Yjs assumption experiments for plan 0002

Runnable evidence for the "Validated assumptions" table in
[`../0002-ylmish-redesign.md`](../0002-ylmish-redesign.md).

```bash
npm install yjs   # any 13.6.x
node experiments.mjs    # U1–U12
node experiments2.mjs   # U5b/U5c, U13–U15
```

Each experiment prints a JSON result; the `note` fields state what the outcome
implies. Step 1 of the plan ports these to `tests/Ylmish.Tests/Y.Assumptions.fs`
so CI pins the semantics against Yjs upgrades.

Notable: in U5/U5b the corruption from re-setting an integrated Y type does
**not** throw at the call site — Yjs logs a `TypeError` internally and the doc
becomes unsyncable only later (`applyUpdate` on a peer throws). That's why the
binding layer must make re-parenting impossible by construction rather than
try/catch around it.
