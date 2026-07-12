(*
    The doc samples' single source of truth is compiled code. Regions of
    src/, examples/ and tests/ are marked with comment pairs

        // sample:begin <name>
        ...
        // sample:end <name>

    and quoted into README.md / doc/guides/*.md wherever a fenced block is
    tagged <!-- sample: <name> -->. The demo transcript is quoted the same
    way: a block tagged <!-- output: demo --> holds what `npm run demo`
    prints. Regions may nest or overlap; marker lines are stripped from the
    extraction, and the extraction is dedented.

        dotnet fsi doc/sync-samples.fsx           rewrite the docs in place
        dotnet fsi doc/sync-samples.fsx --check   exit 1 if any block is stale

    CI runs --check, so a sample or the demo cannot drift from its source.
*)

open System.Diagnostics
open System.IO
open System.Text.RegularExpressions

let root = Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, ".."))
let rel (path : string) = Path.GetRelativePath (root, path)
let check = fsi.CommandLineArgs |> Array.contains "--check"

// --- Extract the marked regions from the .fs sources -------------------------

let sources =
    [ "src"; "examples"; "tests" ]
    |> Seq.collect (fun dir -> Directory.EnumerateFiles (Path.Combine (root, dir), "*.fs", SearchOption.AllDirectories))
    |> Seq.filter (fun f ->
        f.Split Path.DirectorySeparatorChar |> Array.forall (fun part -> part <> "obj" && part <> "bin")
        && not (f.EndsWith ".g.fs"))

let (|Marker|_|) (line : string) =
    let m = Regex.Match (line, @"^\s*// sample:(begin|end) (\S+)\s*$")
    if m.Success then Some (m.Groups.[1].Value, m.Groups.[2].Value) else None

/// Dedent, drop nested marker lines, and trim blank edges.
let clean (lines : string list) =
    let lines = lines |> List.filter (fun l -> (|Marker|_|) l |> Option.isNone)
    let indent =
        match lines |> List.filter (fun l -> l.Trim () <> "") with
        | [] -> 0
        | filled -> filled |> List.map (fun l -> l.Length - l.TrimStart().Length) |> List.min
    let blank (l : string) = l.Trim () = ""
    lines
    |> List.map (fun l -> if blank l then "" else l.[indent ..])
    |> List.skipWhile blank |> List.rev |> List.skipWhile blank |> List.rev
    |> String.concat "\n"

let extract (samples : Map<string, string>) (file : string) =
    let lines = File.ReadAllLines file |> List.ofArray
    let began, ended =
        lines
        |> List.indexed
        |> List.fold (fun (began, ended) (i, line) ->
            match line with
            | Marker ("begin", name) ->
                if Map.containsKey name began then failwithf "duplicate sample '%s' at %s:%d" name (rel file) (i + 1)
                Map.add name i began, ended
            | Marker ("end", name) ->
                match Map.tryFind name began with
                | Some start -> began, (name, lines.[start + 1 .. i - 1]) :: ended
                | None -> failwithf "sample:end '%s' without begin at %s:%d" name (rel file) (i + 1)
            | _ -> began, ended)
            (Map.empty, [])
    match began |> Map.toList |> List.filter (fun (name, _) -> not (List.exists (fst >> (=) name) ended)) with
    | (name, i) :: _ -> failwithf "sample:begin '%s' never ends (%s:%d)" name (rel file) (i + 1)
    | [] -> ()
    (samples, ended) ||> List.fold (fun samples (name, body) ->
        if Map.containsKey name samples then failwithf "duplicate sample '%s' in %s" name (rel file)
        Map.add name (clean body) samples)

let samples = sources |> Seq.fold extract Map.empty

// --- The demo transcript ------------------------------------------------------

let demoTranscript =
    lazy (
        printfn "running the demo to capture its transcript…"
        let psi = ProcessStartInfo ("npm", "run --silent demo", WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false)
        use proc = Process.Start psi
        let out = proc.StandardOutput.ReadToEnd ()
        proc.WaitForExit ()
        if proc.ExitCode <> 0 then failwith "the demo run failed"
        let lines = Regex.Replace(out, "\x1b\\[[0-9;]*m", "").Split '\n'
        match lines |> Array.tryFindIndex (fun l -> l.StartsWith "TodoCollaborative — ") with
        | Some start -> (lines.[start ..] |> String.concat "\n").TrimEnd ()
        | None -> failwith "demo output did not contain the transcript header")

// --- Rewrite the docs ---------------------------------------------------------

let docs =
    Path.Combine (root, "README.md")
    :: List.ofSeq (Directory.EnumerateFiles (Path.Combine (root, "doc", "guides"), "*.md"))

let sampleBlock = Regex @"(?s)(<!-- sample: (\S+) -->\s*\n```fsharp\n)(.*?)(```)"
let outputBlock = Regex @"(?s)(<!-- output: demo -->\s*\n```\n)(.*?)(```)"

let sync (doc : string) =
    let original = File.ReadAllText doc
    let updated =
        sampleBlock.Replace (original, fun m ->
            match Map.tryFind m.Groups.[2].Value samples with
            | Some text -> m.Groups.[1].Value + text + "\n" + m.Groups.[4].Value
            | None -> failwithf "%s references unknown sample '%s'" (rel doc) m.Groups.[2].Value)
        |> fun text ->
            outputBlock.Replace (text, fun m -> m.Groups.[1].Value + demoTranscript.Value + "\n" + m.Groups.[3].Value)
    if updated = original then None
    else
        if not check then
            File.WriteAllText (doc, updated)
            printfn "updated %s" (rel doc)
        Some (rel doc)

match docs |> List.choose sync, check with
| [], _ -> printfn "all doc samples in sync (%d regions)" samples.Count
| stale, true ->
    for doc in stale do eprintfn "STALE: %s — run `npm run docs`" doc
    exit 1
| _, false -> ()
