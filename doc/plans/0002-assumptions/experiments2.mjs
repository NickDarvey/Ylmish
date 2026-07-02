import * as Y from 'yjs'

function syncBoth(a, b) {
  Y.applyUpdate(a, Y.encodeStateAsUpdate(b))
  Y.applyUpdate(b, Y.encodeStateAsUpdate(a))
}

// U5b: re-set integrated type, then sync to a second doc — is the doc corrupted?
{
  const d1 = new Y.Doc()
  const t = new Y.Text(); t.insert(0, 'x')
  d1.getMap().set('a', t)
  let err = null
  try { d1.getMap().set('b', t) } catch (e) { err = e.message }
  const d2 = new Y.Doc()
  let syncErr = null
  try { Y.applyUpdate(d2, Y.encodeStateAsUpdate(d1)) } catch (e) { syncErr = e.message }
  console.log('U5b re-set integrated type:', JSON.stringify({
    setErr: err,
    syncErr,
    d2a: (() => { try { return String(d2.getMap().get('a')) } catch (e) { return 'ERR ' + e.message } })(),
    d2b: (() => { try { return String(d2.getMap().get('b')) } catch (e) { return 'ERR ' + e.message } })(),
  }))
}

// U5c: insert a type integrated in doc1 into doc2 — cross-doc reuse
{
  const d1 = new Y.Doc()
  const t = new Y.Text(); t.insert(0, 'x')
  d1.getMap().set('a', t)
  const d2 = new Y.Doc()
  let err = null
  try { d2.getMap().set('a', t) } catch (e) { err = e.message }
  console.log('U5c cross-doc reuse:', JSON.stringify({ err }))
}

// U13: concurrent "move" as delete+insert — duplication?
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  const arr = new Y.Array(); arr.push(['a', 'b', 'c'])
  d1.getMap().set('xs', arr)
  syncBoth(d1, d2)
  // both concurrently move 'a' to the end (delete@0, insert@end)
  const move = (d) => {
    const xs = d.getMap().get('xs')
    d.transact(() => { const v = xs.get(0); xs.delete(0, 1); xs.push([v]) })
  }
  move(d1); move(d2)
  syncBoth(d1, d2)
  console.log('U13 concurrent move:', JSON.stringify({
    d1: d1.getMap().get('xs').toArray(),
    d2: d2.getMap().get('xs').toArray(),
  }))
}

// U14: does a single doc.transact spanning many nested types produce ONE observeDeep batch?
{
  const d = new Y.Doc()
  const t = new Y.Text(); const a = new Y.Array()
  d.getMap().set('t', t); d.getMap().set('a', a)
  let batches = 0, evtsTotal = 0
  d.getMap().observeDeep((evts) => { batches++; evtsTotal += evts.length })
  d.transact(() => { t.insert(0, 'hi'); a.push([1]); d.getMap().set('k', 'v') })
  console.log('U14 transact batching:', JSON.stringify({ batches, evtsTotal }))
}

// U15: update exchange when only a subset of keys is understood — forward compat.
// Old client has {v1key}, new client adds {v2key with Y.Text}. Old client applies update fine?
{
  const oldc = new Y.Doc(); const newc = new Y.Doc()
  oldc.getMap().set('v1', 'old-data')
  syncBoth(oldc, newc)
  const t = new Y.Text(); t.insert(0, 'new-data')
  newc.getMap().set('v2', t)
  let err = null
  try { syncBoth(oldc, newc) } catch (e) { err = e.message }
  console.log('U15 unknown keys tolerated:', JSON.stringify({
    err,
    oldSees: [...oldc.getMap().keys()],
    v1: oldc.getMap().get('v1'),
  }))
}
