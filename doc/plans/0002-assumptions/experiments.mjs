import * as Y from 'yjs'

const results = []
function report(name, obj) {
  results.push({ name, ...obj })
  console.log(`\n=== ${name} ===`)
  console.log(JSON.stringify(obj, null, 2))
}

function syncBoth(a, b) {
  const ua = Y.encodeStateAsUpdate(a)
  const ub = Y.encodeStateAsUpdate(b)
  Y.applyUpdate(a, ub)
  Y.applyUpdate(b, ua)
}

// U1: doc.getMap() with no args / repeated calls — same instance? key?
{
  const doc = new Y.Doc()
  const m1 = doc.getMap()
  const m2 = doc.getMap()
  const m3 = doc.getMap('')
  report('U1 root map identity', {
    sameInstanceNoArgs: m1 === m2,
    noArgsEqualsEmptyString: m1 === m3,
    shareKeys: [...doc.share.keys()],
  })
}

// U2a: nested-type initialization race — each client creates its own Y.Array under same map key
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  const a1 = new Y.Array(); a1.push(['from-d1'])
  d1.getMap().set('todos', a1)
  const a2 = new Y.Array(); a2.push(['from-d2'])
  d2.getMap().set('todos', a2)
  syncBoth(d1, d2)
  report('U2a nested init race (each creates own Y.Array under same key)', {
    d1: d1.getMap().get('todos').toArray(),
    d2: d2.getMap().get('todos').toArray(),
    note: 'if one subtree wins wholesale, items from loser are LOST',
  })
}

// U2b: same scenario but with ROOT-level arrays (doc.getArray('todos'))
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  d1.getArray('todos').push(['from-d1'])
  d2.getArray('todos').push(['from-d2'])
  syncBoth(d1, d2)
  report('U2b root-level arrays same name', {
    d1: d1.getArray('todos').toArray(),
    d2: d2.getArray('todos').toArray(),
    note: 'root types with same name merge, no wholesale loss',
  })
}

// U3: concurrent edits to same Y.Text nested in a map (created ONCE, then synced)
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  const t = new Y.Text(); t.insert(0, 'hello')
  d1.getMap().set('body', t)
  syncBoth(d1, d2) // both now share the same text
  // concurrent edits
  d1.getMap().get('body').insert(5, ' world')   // "hello world"
  d2.getMap().get('body').insert(0, 'oh, ')      // "oh, hello"
  syncBoth(d1, d2)
  report('U3 concurrent edits on shared nested Y.Text', {
    d1: d1.getMap().get('body').toString(),
    d2: d2.getMap().get('body').toString(),
    converged: d1.getMap().get('body').toString() === d2.getMap().get('body').toString(),
  })
}

// U3b: string field stored as plain map value — concurrent set (the issue #83 failure mode)
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  d1.getMap().set('body', 'hello')
  syncBoth(d1, d2)
  d1.getMap().set('body', 'hello world')
  d2.getMap().set('body', 'oh, hello')
  syncBoth(d1, d2)
  report('U3b concurrent plain-string set (LWW clobber)', {
    d1: d1.getMap().get('body'),
    d2: d2.getMap().get('body'),
  })
}

// U4: what decides the LWW winner for concurrent map.set? clientID? insertion order?
{
  const outcomes = []
  for (const [c1, c2] of [[1, 2], [2, 1], [100, 5]]) {
    const d1 = new Y.Doc(); d1.clientID = c1
    const d2 = new Y.Doc(); d2.clientID = c2
    d1.getMap().set('k', 'v-from-' + c1)
    d2.getMap().set('k', 'v-from-' + c2)
    // apply d2's update to d1 first, then reverse — check order independence
    syncBoth(d1, d2)
    outcomes.push({ c1, c2, d1: d1.getMap().get('k'), d2: d2.getMap().get('k') })
  }
  report('U4 concurrent map.set winner rule', { outcomes, note: 'winner should be higher clientID, deterministic, NOT wall-clock' })
}

// U5: can an integrated Y type be moved / re-set elsewhere?
{
  const d1 = new Y.Doc()
  const t = new Y.Text(); t.insert(0, 'x')
  d1.getMap().set('a', t)
  let moveError = null
  try {
    d1.getMap().set('b', t) // re-set same instance to another key
  } catch (e) { moveError = e.message }
  const bIsA = d1.getMap().get('a') === d1.getMap().get('b')
  report('U5 move integrated type', {
    moveError,
    reSetSucceededButAliased: bIsA,
    aStr: String(d1.getMap().get('a')),
    bStr: (() => { try { return String(d1.getMap().get('b')) } catch (e) { return 'ERROR: ' + e.message } })(),
  })
}

// U6: transaction origins — do they propagate to observers, and does applyUpdate carry its own origin?
{
  const d1 = new Y.Doc()
  const seen = []
  d1.getMap().observe((e, tr) => { seen.push({ origin: String(tr.origin), local: tr.local }) })
  d1.transact(() => d1.getMap().set('k', 1), 'my-origin')
  d1.getMap().set('k', 2) // no origin
  const d2 = new Y.Doc()
  d2.getMap().set('k', 3)
  Y.applyUpdate(d1, Y.encodeStateAsUpdate(d2), 'remote-origin')
  report('U6 transaction origins', { seen })
}

// U7: observeDeep on root map — does it fire for nested Y.Text edits with paths?
{
  const d1 = new Y.Doc()
  const t = new Y.Text()
  d1.getMap().set('body', t)
  const events = []
  d1.getMap().observeDeep((evts) => {
    for (const e of evts) events.push({ type: e.constructor.name, path: e.path })
  })
  t.insert(0, 'hi')
  const arr = new Y.Array()
  d1.getMap().set('list', arr)
  arr.push([new Y.Map()])
  arr.get(0).set('inner', 'v')
  report('U7 observeDeep nested events', { events })
}

// U8: concurrent Y.Array inserts at same position — both survive?
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  const arr = new Y.Array(); arr.push(['base'])
  d1.getMap().set('xs', arr)
  syncBoth(d1, d2)
  d1.getMap().get('xs').insert(0, ['from-d1'])
  d2.getMap().get('xs').insert(0, ['from-d2'])
  syncBoth(d1, d2)
  report('U8 concurrent array inserts', {
    d1: d1.getMap().get('xs').toArray(),
    d2: d2.getMap().get('xs').toArray(),
  })
}

// U9: delete a nested map while the other client edits inside it
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  const item = new Y.Map(); item.set('title', 'a')
  const arr = new Y.Array(); arr.push([item])
  d1.getMap().set('xs', arr)
  syncBoth(d1, d2)
  d1.getMap().get('xs').delete(0, 1)               // d1 deletes the item
  d2.getMap().get('xs').get(0).set('title', 'b')    // d2 edits inside it
  syncBoth(d1, d2)
  report('U9 delete vs edit-inside', {
    d1len: d1.getMap().get('xs').length,
    d2len: d2.getMap().get('xs').length,
    note: 'deletion should win; concurrent edit lost',
  })
}

// U10: what primitive value types does Y.Map support?
{
  const d = new Y.Doc()
  const m = d.getMap()
  const vals = { num: 42, float: 1.5, bool: true, nul: null, str: 's', obj: { a: 1 }, arr: [1, 2] }
  const res = {}
  for (const [k, v] of Object.entries(vals)) {
    try { m.set(k, v); res[k] = { ok: true, back: m.get(k), type: typeof m.get(k) } }
    catch (e) { res[k] = { ok: false, err: e.message } }
  }
  let undef = null
  try { m.set('undef', undefined); undef = { ok: true, back: String(m.get('undef')) } } catch (e) { undef = { ok: false, err: e.message } }
  res.undef = undef
  report('U10 primitive types in Y.Map', res)
}

// U11: Y.Text delta granularity — does a Y.Text created-then-populated in same tx emit sane deltas after sync?
// And: does re-assigning a NEW Y.Text over an old key kill the old one for remote editors?
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  const t1 = new Y.Text(); t1.insert(0, 'v1')
  d1.getMap().set('body', t1)
  syncBoth(d1, d2)
  const t2ref = d2.getMap().get('body')
  // d1 replaces the Y.Text entirely (like a materialize would)
  const tNew = new Y.Text(); tNew.insert(0, 'v2')
  d1.getMap().set('body', tNew)
  // d2 concurrently edits the OLD text
  t2ref.insert(2, '!')
  syncBoth(d1, d2)
  report('U11 replace Y.Text vs concurrent edit of old', {
    d1: String(d1.getMap().get('body')),
    d2: String(d2.getMap().get('body')),
    note: 'replacement wins; old edits lost — why materialize-per-update breaks CRDT merging',
  })
}

// U12: offline convergence — two docs never synced start with "same" initial state via independent materialize; how bad is it?
{
  const d1 = new Y.Doc(); d1.clientID = 1
  const d2 = new Y.Doc(); d2.clientID = 2
  // both do what withYlmish init does today: materialize the same initial model independently
  for (const d of [d1, d2]) {
    const items = new Y.Array(); items.push(['seed'])
    d.getMap().set('items', items)
    const t = new Y.Text(); t.insert(0, 'seed-text')
    d.getMap().set('body', t)
  }
  syncBoth(d1, d2)
  report('U12 independent identical initialization', {
    items: d1.getMap().get('items').toArray(),
    body: String(d1.getMap().get('body')),
    note: 'identical independent init still duplicates or drops — structure is not content-addressed',
  })
}

console.log('\n\nDONE. ' + results.length + ' experiments run.')
