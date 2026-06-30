using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MilovanaEosEditor.Dsl.Commands;

namespace MilovanaEosEditor.Dsl;

/// <summary>One logical page from the outline: a <c>[PAGE]</c> and the source lines it spans.</summary>
public sealed record PageOutline(string Name, int Line, int EndLine);

/// <summary>Result of analysing a script (parse + validate + in-memory build), without writing anything.</summary>
public sealed class CompileResult
{
    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }
    public required IReadOnlyList<PageOutline> Pages { get; init; }
    public required EosObject BuiltPages { get; init; }
    public int LogicalPageCount { get; init; }
    public int EosPageCount { get; init; }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == Severity.Error);
}

/// <summary>Result of an export build, including the report text printed by the headless/CLI path.</summary>
public sealed class BuildResult
{
    public required bool Ok { get; init; }              // no errors -> tease.json was written
    public required IReadOnlyList<Diagnostic> Diagnostics { get; init; }
    public string? Json { get; init; }
    public required string Report { get; init; }
    public string? OutputPath { get; init; }
}

/// <summary>
/// Compiles a tease <c>script.md</c> (the marker DSL) into EOS <c>tease.json</c> — the C# port of
/// <c>Build-Tease.ps1</c> and the single source of truth for both live validation (<see cref="Compile"/>)
/// and export (<see cref="Build"/>). Parsing/colorizing offsets come from <see cref="TeaseScriptParser"/>;
/// per-marker rules from the <see cref="MarkerCommand"/> classes; everything cross-cutting (page
/// assembly, timed-page expansion, notification scoping, nav resolution) lives here.
/// </summary>
public sealed class TeaseScriptCompiler
{
    private readonly CommandRegistry _registry;
    private readonly TeaseScriptParser _parser;

    public TeaseScriptCompiler(CommandRegistry? registry = null)
    {
        _registry = registry ?? CommandRegistry.Default;
        _parser = new TeaseScriptParser(_registry);
    }

    // ===== internal page model =====
    private sealed class PageDef
    {
        public required string Name { get; init; }
        public required int Line { get; init; }
        public List<MarkerInstance> Items { get; } = new();
        public int EndLine { get; set; } = int.MaxValue;
    }

    private sealed record Exit(string Type, string Target); // Type: "goto" | "end"

    // ===================================================================================
    //  Analyze / Compile (no file writes)
    // ===================================================================================
    public CompileResult Compile(string text, ILocatorSource assets)
    {
        var diags = new DiagnosticSink();
        IReadOnlyList<MarkerInstance> markers = _parser.Parse(text, diags);

        // ---- group markers into pages (orphans + unknown keywords reported, then skipped) ----
        var pages = new List<PageDef>();
        var firstDeclared = new Dictionary<string, int>(StringComparer.Ordinal);
        var navRefs = new List<(string Target, int Line)>();
        PageDef? cur = null;

        foreach (MarkerInstance m in markers)
        {
            if (m.Command is null)
            {
                diags.Warn(m.Line, DiagnosticKind.UnknownKeyword, $"unknown keyword '[{m.Keyword}]'; line ignored");
                continue;
            }

            if (m.Keyword == "PAGE")
            {
                string key = m.TrimmedPayload;
                if (key.Length == 0)
                    diags.Error(m.Line, DiagnosticKind.EmptyPageKey, "[PAGE] with an empty key");
                else if (firstDeclared.ContainsKey(key))
                    diags.Error(m.Line, DiagnosticKind.DuplicatePage,
                        $"duplicate page key '{key}' (first declared at line {firstDeclared[key]})");

                if (cur is not null) cur.EndLine = m.Line - 1;
                cur = new PageDef { Name = key, Line = m.Line };
                pages.Add(cur);
                if (key.Length > 0 && !firstDeclared.ContainsKey(key)) firstDeclared[key] = m.Line;
                continue;
            }

            if (cur is null)
            {
                diags.Warn(m.Line, DiagnosticKind.OrphanBeforePage, $"[{m.Keyword}] appears before the first [PAGE]; ignored");
                continue;
            }

            cur.Items.Add(m);

            // Capture user-authored nav targets for the unresolved-target check (matches PS navRefs).
            if (m.Keyword == "GOTO" && m.TrimmedPayload.Length > 0)
                navRefs.Add((m.TrimmedPayload, m.Line));
            else if (m.Keyword is "OPTION" or "NOTIFICATION")
            {
                string? target = m.Param("target");
                if (!string.IsNullOrEmpty(target)) navRefs.Add((target, m.Line));
            }
        }

        if (pages.Count == 0)
            diags.Error(0, DiagnosticKind.NoPages, "no [PAGE] markers found in script.md");

        // ---- per-marker validation (param/typed/self), decidable from each marker alone ----
        foreach (PageDef page in pages)
            foreach (MarkerInstance item in page.Items)
                item.Command!.Validate(item, diags);

        // ---- build pages (assembly + cross-cutting validation + image/audio resolution) ----
        var built = new List<(string Name, List<EosObject> Actions)>();
        foreach (PageDef page in pages)
        {
            bool timed = page.Items.Any(it => it.Keyword is "METRONOME" or "PAUSE");
            if (timed) BuildTimed(page, assets, built, diags);
            else BuildSimple(page, assets, built, diags);
        }

        ScopeNotifications(built);

        // ---- nav validation ----
        var declared = built.Select(b => b.Name).ToHashSet(StringComparer.Ordinal);
        foreach (string target in navRefs.Select(r => r.Target).Distinct())
        {
            if (!declared.Contains(target))
            {
                int line = navRefs.First(r => r.Target == target).Line;
                diags.Error(line, DiagnosticKind.BrokenJump, $"nav target '{target}' does not resolve to any page");
            }
        }
        if (pages.Count > 0 && !declared.Contains("start"))
            diags.Error(0, DiagnosticKind.NoStartPage, "no 'start' page (the first logical page must be [PAGE: start])");

        var pagesObj = new EosObject();
        foreach ((string name, List<EosObject> actions) in built)
            pagesObj.Add(name, new EosArray().AddRange(actions));

        return new CompileResult
        {
            Diagnostics = Sort(diags.Items),
            Pages = pages.Select(p => new PageOutline(p.Name, p.Line, p.EndLine)).ToList(),
            BuiltPages = pagesObj,
            LogicalPageCount = pages.Count,
            EosPageCount = built.Count,
        };
    }

    // ===================================================================================
    //  Simple page
    // ===================================================================================
    private void BuildSimple(PageDef page, ILocatorSource assets, List<(string, List<EosObject>)> built, DiagnosticSink diags)
    {
        Exit? exit = null;
        foreach (MarkerInstance it in page.Items)
        {
            if (it.Keyword == "GOTO")
            {
                string t = it.TrimmedPayload;
                if (t.Length == 0) diags.Error(it.Line, DiagnosticKind.MissingPayload, "[GOTO] has no target page key");
                exit = new Exit("goto", t);
            }
            else if (it.Keyword == "END") exit = new Exit("end", "");
        }

        bool hasChoice = page.Items.Any(it => it.Keyword == "CHOICE");
        if (exit is null && !hasChoice)
            diags.Warn(page.Line, DiagnosticKind.DeadEndPage, $"page '{page.Name}' has no [GOTO]/[END]/[CHOICE] exit (it will dead-end)");

        int lastSay = -1;
        for (int i = 0; i < page.Items.Count; i++)
            if (page.Items[i].Keyword == "SAY") lastSay = i;
        bool tapAdvance = exit is { Type: "goto" } && !hasChoice;

        var actions = new List<EosObject>();
        for (int i = 0; i < page.Items.Count; i++)
        {
            MarkerInstance it = page.Items[i];
            switch (it.Keyword)
            {
                case "IMAGE":
                    if (it.TrimmedPayload != ImageCommand.Hold)
                    {
                        EosObject? img = ResolveImage(it.TrimmedPayload, it.Line, it, assets, diags);
                        if (img is not null) actions.Add(img);
                    }
                    break;
                case "AUDIO":
                {
                    int? bpm = TryInt(it, "bpm");
                    int? loops = TryInt(it, "loops");
                    if (bpm is not null && loops is not null)
                    {
                        VerifyAudio(bpm.Value, it.Line, assets, diags);
                        actions.Add(AudioCommand.BuildAction(bpm.Value, loops.Value));
                    }
                    break;
                }
                case "NOTIFICATION":
                {
                    EosObject? notif = BuildNotification(it, diags);
                    if (notif is not null) actions.Add(notif);
                    break;
                }
                case "SAY":
                {
                    string? mode = it.Param("mode") ?? (tapAdvance && i == lastSay ? "pause" : "instant");
                    actions.Add(SayCommand.BuildAction(NormalizeSay(it.Payload), mode, it.Param("align"), it.Param("duration")));
                    break;
                }
                case "CHOICE":
                    actions.Add(BuildChoice(page, diags));
                    break;
            }
        }

        if (exit is { Type: "goto" }) actions.Add(EosActions.Goto(exit.Target));
        else if (exit is { Type: "end" }) actions.Add(EosActions.End());

        built.Add((page.Name, actions));
    }

    // ===================================================================================
    //  Timed page (each [METRONOME]/[PAUSE] block becomes its own EOS page)
    // ===================================================================================
    private sealed class Block
    {
        public required string Kind { get; init; } // "metro" | "pause"
        public int Bpm { get; init; }
        public double Secs { get; init; }
        public string? Img { get; set; }
        public int ImgLine { get; set; }
        public List<EosObject> Notifs { get; init; } = new();
        public List<string> Lines { get; } = new();
        public int Line { get; init; }
    }

    private void BuildTimed(PageDef page, ILocatorSource assets, List<(string, List<EosObject>)> built, DiagnosticSink diags)
    {
        var blocks = new List<Block>();
        string? pendImg = null;
        int pendImgLine = 0;
        var pendNotifs = new List<EosObject>();
        Exit? exit = null;

        foreach (MarkerInstance it in page.Items)
        {
            switch (it.Keyword)
            {
                case "IMAGE":
                    if (it.TrimmedPayload == ImageCommand.Hold) pendImg = null;
                    else { pendImg = it.TrimmedPayload; pendImgLine = it.Line; }
                    break;
                case "NOTIFICATION":
                {
                    EosObject? notif = BuildNotification(it, diags);
                    if (notif is not null) pendNotifs.Add(notif);
                    break;
                }
                case "METRONOME":
                {
                    int bpm = TryInt(it, "bpm") ?? 0;
                    double secs = TryDouble(it, "secs") ?? 0.0;
                    blocks.Add(new Block { Kind = "metro", Bpm = bpm, Secs = secs, Img = pendImg, ImgLine = pendImgLine, Line = it.Line, Notifs = pendNotifs });
                    pendImg = null; pendNotifs = new List<EosObject>();
                    break;
                }
                case "PAUSE":
                {
                    double secs = TryDouble(it, "secs") ?? 0.0;
                    blocks.Add(new Block { Kind = "pause", Bpm = 0, Secs = secs, Img = pendImg, ImgLine = pendImgLine, Line = it.Line, Notifs = pendNotifs });
                    pendImg = null; pendNotifs = new List<EosObject>();
                    break;
                }
                case "SAY":
                    if (blocks.Count == 0)
                        diags.Error(it.Line, DiagnosticKind.SayBeforeBlock, $"[SAY] before any [METRONOME]/[PAUSE] on page '{page.Name}'");
                    else
                        blocks[^1].Lines.Add(NormalizeSay(it.Payload));
                    break;
                case "GOTO":
                {
                    string t = it.TrimmedPayload;
                    if (t.Length == 0) diags.Error(it.Line, DiagnosticKind.MissingPayload, "[GOTO] has no target page key");
                    exit = new Exit("goto", t);
                    break;
                }
                case "END":
                    exit = new Exit("end", "");
                    break;
            }
        }

        if (exit is null)
        {
            diags.Error(page.Line, DiagnosticKind.TimedNoExit, $"timed page '{page.Name}' has no [GOTO]/[END] exit");
            exit = new Exit("end", "");
        }

        int n = blocks.Count;
        for (int i = 0; i < n; i++)
        {
            Block b = blocks[i];
            string pk = i == 0 ? page.Name : $"{page.Name}-{i + 1}";
            var a = new List<EosObject>();

            a.AddRange(b.Notifs);
            if (b.Img is not null)
            {
                EosObject? img = ResolveImage(b.Img, b.ImgLine, null, assets, diags);
                if (img is not null) a.Add(img);
            }
            if (b.Kind == "metro")
            {
                VerifyAudio(b.Bpm, b.Line, assets, diags);
                a.Add(MetronomeCommand.BuildAudio(b.Bpm, b.Secs));
            }

            if (b.Lines.Count == 0)
            {
                a.Add(EosActions.Timer($"{FmtNum(b.Secs)}s", "hidden"));
            }
            else
            {
                double per = Math.Round(b.Secs / b.Lines.Count, 1);
                if (per <= 0) per = 1;
                foreach (string ln in b.Lines)
                {
                    a.Add(SayCommand.BuildAction(ln, "instant", null, null));
                    a.Add(EosActions.Timer($"{FmtNum(per)}s", "hidden"));
                }
            }

            if (i < n - 1) a.Add(EosActions.Goto($"{page.Name}-{i + 2}"));
            else if (exit.Type == "end") a.Add(EosActions.End());
            else a.Add(EosActions.Goto(exit.Target)); // target may equal page.Name -> loop

            built.Add((pk, a));
        }
    }

    // ===================================================================================
    //  Choice
    // ===================================================================================
    private EosObject BuildChoice(PageDef page, DiagnosticSink diags)
    {
        var opts = new List<(string Label, string Target, string? Color)>();
        foreach (MarkerInstance it in page.Items)
        {
            if (it.Keyword != "OPTION") continue;
            string? target = it.Param("target");
            if (string.IsNullOrWhiteSpace(it.Payload))
                diags.Error(it.Line, DiagnosticKind.MissingPayload, "[OPTION] has no label text");
            opts.Add((it.Payload, target ?? "", it.Param("color")));
        }
        if (opts.Count == 0)
            diags.Error(page.Line, DiagnosticKind.ChoiceNoOptions, $"[CHOICE] on page '{page.Name}' has no [OPTION]s");
        return ChoiceCommand.BuildAction(opts);
    }

    // ===================================================================================
    //  Shared emit helpers
    // ===================================================================================
    private static EosObject? ResolveImage(string bucketFile, int line, MarkerInstance? marker, ILocatorSource assets, DiagnosticSink diags)
    {
        string? locator = assets.ResolveImageLocator(bucketFile);
        if (locator is null)
        {
            diags.Error(line, DiagnosticKind.UnresolvedImage,
                $"no locator for image '{bucketFile}' (not found in asset-map.json)",
                marker?.PayloadOffset is > 0 ? marker.PayloadOffset : null,
                marker is not null ? marker.PayloadLength : null);
            return null;
        }
        return ImageCommand.BuildAction(locator);
    }

    private static void VerifyAudio(int bpm, int line, ILocatorSource assets, DiagnosticSink diags)
    {
        string file = EosActions.MetronomeFile(bpm);
        if (assets.AudioFilesKnown && !assets.HasAudioFile(file))
            diags.Error(line, DiagnosticKind.MissingAudio, $"metronome audio '{file}' (bpm={bpm}) not found in Files/");
    }

    private static EosObject? BuildNotification(MarkerInstance it, DiagnosticSink diags)
    {
        string? id = it.Param("id");
        string? target = it.Param("target");
        if (string.IsNullOrWhiteSpace(it.Payload))
            diags.Error(it.Line, DiagnosticKind.MissingPayload, "[NOTIFICATION] has no button label text");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target)) return null;
        return NotificationCommand.BuildAction(id, it.Payload, target);
    }

    private static int? TryInt(MarkerInstance m, string name) =>
        int.TryParse(m.Param(name), out int v) ? v : null;

    private static double? TryDouble(MarkerInstance m, string name) =>
        double.TryParse(m.Param(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null;

    // ===================================================================================
    //  Notification auto-scoping (post-assembly) — mirrors Build-Tease.ps1 exactly.
    //  A notification created on a page is removed when navigating to a page that does not
    //  re-declare the same id, so it doesn't linger; a self-loop keeps it (no flicker).
    // ===================================================================================
    private static void ScopeNotifications(List<(string Name, List<EosObject> Actions)> built)
    {
        var pageNotifs = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach ((string name, List<EosObject> actions) in built)
        {
            var ids = new List<string>();
            foreach (EosObject act in actions)
                if (act.Get("notification.create") is EosObject nc && nc.Get("id") is EosString id)
                    ids.Add(id.Value);
            pageNotifs[name] = ids;
        }

        List<EosObject> RemovesFor(List<string> created, string? targetPage)
        {
            var r = new List<EosObject>();
            if (created.Count == 0) return r;
            List<string> keep = targetPage is not null && pageNotifs.TryGetValue(targetPage, out List<string>? k) ? k : new List<string>();
            foreach (string id in created)
                if (!keep.Contains(id)) r.Add(EosActions.NotificationRemove(id));
            return r;
        }

        for (int p = 0; p < built.Count; p++)
        {
            List<string> created = pageNotifs[built[p].Name];
            if (created.Count == 0) continue;

            var newActions = new List<EosObject>();
            foreach (EosObject act in built[p].Actions)
            {
                if (act.Get("goto") is EosObject g && g.Get("target") is EosString gt)
                {
                    newActions.AddRange(RemovesFor(created, gt.Value));
                    newActions.Add(act);
                }
                else if (act.ContainsKey("end"))
                {
                    newActions.AddRange(RemovesFor(created, null));
                    newActions.Add(act);
                }
                else if (act.Get("choice") is EosObject choice && choice.Get("options") is EosArray options)
                {
                    foreach (EosNode optNode in options.Items)
                    {
                        if (optNode is not EosObject opt || opt.Get("commands") is not EosArray cmds) continue;
                        string? tgt = NotifTarget(cmds);
                        opt.Add("commands", new EosArray().AddRange(RemovesFor(created, tgt)).AddRange(cmds.Items));
                    }
                    newActions.Add(act);
                }
                else if (act.Get("notification.create") is EosObject nc && nc.Get("buttonCommands") is EosArray bc)
                {
                    string? tgt = NotifTarget(bc);
                    nc.Add("buttonCommands", new EosArray().AddRange(RemovesFor(created, tgt)).AddRange(bc.Items));
                    newActions.Add(act);
                }
                else
                {
                    newActions.Add(act);
                }
            }
            built[p] = (built[p].Name, newActions);
        }
    }

    /// <summary>The nav target of a command list is its last goto (matches PS NotifTarget).</summary>
    private static string? NotifTarget(EosArray commands)
    {
        string? t = null;
        foreach (EosNode c in commands.Items)
            if (c is EosObject co && co.Get("goto") is EosObject g && g.Get("target") is EosString s)
                t = s.Value;
        return t;
    }

    // ===================================================================================
    //  Space preservation / say normalization (PS PreserveSpacing + NormalizeSay)
    // ===================================================================================
    private const int NbspTabWidth = 4;

    private static string PreserveSpacing(string line)
    {
        line = line.Replace("\t", Repeat("&nbsp;", NbspTabWidth));
        line = Regex.Replace(line, "^ +", m => Repeat("&nbsp;", m.Length));
        return Regex.Replace(line, " {2,}", m => Repeat("&nbsp;", m.Length));
    }

    private static string NormalizeSay(string payload)
    {
        payload = payload.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!payload.Contains('\n')) return PreserveSpacing(payload);

        string[] raw = payload.Split('\n');
        int start = 0, end = raw.Length - 1;
        while (start <= end && raw[start].Trim().Length == 0) start++;
        while (end >= start && raw[end].Trim().Length == 0) end--;

        var parts = new List<string>();
        for (int i = start; i <= end; i++)
            parts.Add(raw[i].Trim().Length == 0 ? "" : PreserveSpacing(raw[i]));
        return string.Join("<br>", parts);
    }

    private static string Repeat(string s, int n) => string.Concat(Enumerable.Repeat(s, n));

    /// <summary>Format a double like PowerShell's "{0}" (1.0 → "1", 12.5 → "12.5").</summary>
    private static string FmtNum(double d) => d.ToString(CultureInfo.InvariantCulture);

    private static IReadOnlyList<Diagnostic> Sort(IReadOnlyList<Diagnostic> items) =>
        items.OrderBy(d => d.Line).ThenBy(d => d.Severity == Severity.Error ? 0 : 1).ToList();

    // ===================================================================================
    //  Build (export) — Compile, then reuse the manifest and write tease.json on success.
    // ===================================================================================
    public static BuildResult Build(string teaseDir)
    {
        teaseDir = Path.GetFullPath(teaseDir);
        string teaseName = new DirectoryInfo(teaseDir).Name;
        string scriptPath = Path.Combine(teaseDir, "script.md");
        string teaseJsonPath = Path.Combine(teaseDir, "tease.json");

        if (!File.Exists(scriptPath)) throw new FileNotFoundException($"No script.md in '{teaseDir}'.", scriptPath);
        if (!File.Exists(teaseJsonPath)) throw new FileNotFoundException($"No tease.json (manifest stub) in '{teaseDir}'.", teaseJsonPath);

        string text = File.ReadAllText(scriptPath, Encoding.UTF8);
        var assets = AssetCatalog.Load(teaseDir);
        var compiler = new TeaseScriptCompiler();
        CompileResult result = compiler.Compile(text, assets);

        // Reuse the exported tease.json's galleries/files/editor verbatim; replace only pages/init/modules.
        var manifest = (EosObject)EosNode.From(
            System.Text.Json.JsonDocument.Parse(File.ReadAllText(teaseJsonPath)).RootElement);
        manifest.Add("pages", result.BuiltPages);
        manifest.Add("init", new EosString(""));
        manifest.Add("modules", new EosObject().Add("audio", new EosObject()).Add("notification", new EosObject()));
        string json = manifest.ToJson();

        bool ok = !result.HasErrors;
        if (ok) File.WriteAllText(teaseJsonPath, json, new UTF8Encoding(false));

        string report = FormatReport(teaseName, result, json, ok, teaseJsonPath);
        return new BuildResult
        {
            Ok = ok,
            Diagnostics = result.Diagnostics,
            Json = json,
            Report = report,
            OutputPath = ok ? teaseJsonPath : null,
        };
    }

    private static string FormatReport(string teaseName, CompileResult r, string json, bool ok, string outPath)
    {
        int navTargets = Regex.Matches(json, "\"target\":\"([^\"]+)\"").Select(m => m.Groups[1].Value).Distinct().Count();
        int imageLocators = Regex.Matches(json, "gallery:").Count;
        int audioLocators = Regex.Matches(json, "file:metronome").Count;

        var sb = new StringBuilder();
        sb.AppendLine($"Tease                   : {teaseName}");
        sb.AppendLine($"Logical pages in script : {r.LogicalPageCount}");
        sb.AppendLine($"EOS pages generated     : {r.EosPageCount}");
        sb.AppendLine($"Distinct nav targets    : {navTargets}");
        sb.AppendLine($"image locators          : {imageLocators} ; audio locators: {audioLocators}");

        var errors = r.Diagnostics.Where(d => d.Severity == Severity.Error).ToList();
        var warnings = r.Diagnostics.Where(d => d.Severity == Severity.Warning).ToList();
        if (r.Diagnostics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Diagnostics ({errors.Count} error(s), {warnings.Count} warning(s)) -- line numbers are script.md:");
            foreach (Diagnostic d in r.Diagnostics)
            {
                string where = d.Line > 0 ? $"script.md:{d.Line}" : "script.md";
                string tag = d.Severity == Severity.Error ? "ERROR" : "WARN ";
                sb.AppendLine($"  {tag}  {where,-16} {d.Message}");
            }
        }

        sb.AppendLine();
        if (!ok)
            sb.Append($"Build FAILED: {errors.Count} error(s). tease.json was NOT modified -- fix the lines above and re-run.");
        else if (warnings.Count > 0)
            sb.Append($"Build succeeded with {warnings.Count} warning(s). Wrote {outPath}");
        else
            sb.Append($"Build succeeded, no issues. Wrote {outPath}");
        return sb.ToString();
    }
}
