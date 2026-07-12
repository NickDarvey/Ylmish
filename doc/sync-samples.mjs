#!/usr/bin/env node
// The doc samples' single source of truth is compiled code. Regions of
// `src/`, `examples/` and `tests/` are marked with comment pairs
//
//     // sample:begin <name>
//     ...
//     // sample:end <name>
//
// and quoted into README.md / doc/guides/*.md wherever a fenced block is
// tagged `<!-- sample: <name> -->`. The demo transcript is quoted the same
// way: a block tagged `<!-- output: demo -->` holds what `npm run demo`
// prints. Regions may nest or overlap; marker lines are stripped from the
// extraction, and the extraction is dedented.
//
//     node doc/sync-samples.mjs           rewrite the docs in place
//     node doc/sync-samples.mjs --check   exit 1 if any doc block is stale
//
// CI runs --check, so a sample or the demo cannot drift from its source.

import { readFileSync, writeFileSync, readdirSync, statSync } from 'node:fs'
import { execSync } from 'node:child_process'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(fileURLToPath(import.meta.url), '..', '..')
const check = process.argv.includes('--check')

// --- Collect .fs sources -----------------------------------------------------

const sources = []
const walk = dir => {
    for (const entry of readdirSync(dir)) {
        if (['obj', 'bin', 'node_modules'].includes(entry)) continue
        const path = join(dir, entry)
        if (statSync(path).isDirectory()) walk(path)
        else if (entry.endsWith('.fs') && !entry.endsWith('.g.fs')) sources.push(path)
    }
}
for (const dir of ['src', 'examples', 'tests']) walk(join(root, dir))

// --- Extract marked regions --------------------------------------------------

const BEGIN = /^\s*\/\/ sample:begin (\S+)\s*$/
const END = /^\s*\/\/ sample:end (\S+)\s*$/
const MARKER = /^\s*\/\/ sample:(?:begin|end) \S+\s*$/

const dedent = lines => {
    const indents = lines.filter(l => l.trim() !== '').map(l => l.match(/^\s*/)[0].length)
    const cut = indents.length === 0 ? 0 : Math.min(...indents)
    return lines.map(l => (l.trim() === '' ? '' : l.slice(cut)))
}

const trimBlankEdges = lines => {
    let a = 0, b = lines.length
    while (a < b && lines[a].trim() === '') a++
    while (b > a && lines[b - 1].trim() === '') b--
    return lines.slice(a, b)
}

const samples = new Map()
for (const file of sources) {
    const lines = readFileSync(file, 'utf8').split('\n')
    const open = new Map()
    lines.forEach((line, i) => {
        const begin = line.match(BEGIN)
        const end = line.match(END)
        if (begin) {
            if (open.has(begin[1]) || samples.has(begin[1]))
                throw new Error(`duplicate sample '${begin[1]}' at ${relative(root, file)}:${i + 1}`)
            open.set(begin[1], i)
        } else if (end) {
            const start = open.get(end[1])
            if (start === undefined)
                throw new Error(`sample:end '${end[1]}' without begin at ${relative(root, file)}:${i + 1}`)
            open.delete(end[1])
            const body = lines.slice(start + 1, i).filter(l => !MARKER.test(l))
            samples.set(end[1], {
                file: relative(root, file),
                text: trimBlankEdges(dedent(body)).join('\n'),
            })
        }
    })
    for (const [name, i] of open)
        throw new Error(`sample:begin '${name}' never ends (${relative(root, file)}:${i + 1})`)
}

// --- The demo transcript -----------------------------------------------------

let demo = null
const demoTranscript = () => {
    if (demo === null) {
        console.log('running the demo to capture its transcript…')
        const raw = execSync('npm run --silent demo', { cwd: root, maxBuffer: 64 * 1024 * 1024 })
            .toString()
            .replace(/\x1b\[[0-9;]*m/g, '')
        const lines = raw.split('\n')
        const start = lines.findIndex(l => l.startsWith('TodoCollaborative — '))
        if (start < 0) throw new Error('demo output did not contain the transcript header')
        demo = lines.slice(start).join('\n').trimEnd()
    }
    return demo
}

// --- Rewrite the docs --------------------------------------------------------

const docs = [
    join(root, 'README.md'),
    ...readdirSync(join(root, 'doc', 'guides'))
        .filter(f => f.endsWith('.md'))
        .map(f => join(root, 'doc', 'guides', f)),
]

const SAMPLE_BLOCK = /(<!-- sample: (\S+) -->\s*\n```fsharp\n)([\s\S]*?)(```)/g
const OUTPUT_BLOCK = /(<!-- output: demo -->\s*\n```\n)([\s\S]*?)(```)/g

let stale = 0
for (const doc of docs) {
    const original = readFileSync(doc, 'utf8')
    let updated = original.replace(SAMPLE_BLOCK, (_, head, name, _body, tail) => {
        const sample = samples.get(name)
        if (!sample) throw new Error(`${relative(root, doc)} references unknown sample '${name}'`)
        return head + sample.text + '\n' + tail
    })
    updated = updated.replace(OUTPUT_BLOCK, (_, head, _body, tail) => head + demoTranscript() + '\n' + tail)
    if (updated !== original) {
        stale++
        if (check) console.error(`STALE: ${relative(root, doc)} — run \`npm run docs\``)
        else {
            writeFileSync(doc, updated)
            console.log(`updated ${relative(root, doc)}`)
        }
    }
}

if (check) {
    if (stale > 0) process.exit(1)
    console.log(`all doc samples in sync (${samples.size} regions)`)
} else if (stale === 0) {
    console.log(`nothing to update (${samples.size} regions)`)
}
