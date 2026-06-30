using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilovanaEosEditor.Dsl;

namespace MilovanaEosEditor;

/// <summary>
/// The Script Editor tool: edits <c>script.md</c> directly and runs the shared
/// <see cref="TeaseScriptCompiler"/> on every (debounced) change for live validation + the page
/// outline, and on demand for the "Export to tease.json" build. The view owns the AvalonEdit control
/// (an imperative control); this view-model owns the file, the asset catalog, and the derived state
/// (problems, page outline, error/warning counts) and raises events the view reacts to.
/// </summary>
public sealed partial class ScriptEditorViewModel : ToolViewModel
{
    private readonly string _scriptPath;
    private readonly TeaseScriptCompiler _compiler = new();
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _analyzeTimer;
    private string _text;
    private bool _loaded;

    public ScriptEditorViewModel(TeaseInfo tease) : base(tease)
    {
        _scriptPath = Path.Combine(tease.FolderPath, "script.md");
        _text = File.Exists(_scriptPath) ? File.ReadAllText(_scriptPath) : "";
        Assets = AssetCatalog.Load(tease.FolderPath);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => FlushSave();
        _analyzeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _analyzeTimer.Tick += (_, _) => RunAnalyze();

        RunAnalyze();
        _loaded = true;
    }

    public override string Title => "Script Editor";
    public override string Subtitle => "Write the tease in script.md with inline previews and live validation.";

    /// <summary>Catalog of buckets/files/tags + Files listing, shared with completion, preview, picker.</summary>
    internal AssetCatalog Assets { get; private set; }

    /// <summary>Latest diagnostics (with spans) for the view's squiggle layer.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = Array.Empty<Diagnostic>();

    /// <summary>Declared page names, for goto/option/notification target completion.</summary>
    public IReadOnlyList<string> PageNames { get; private set; } = Array.Empty<string>();

    public ObservableCollection<ProblemItem> Problems { get; } = new();
    public ObservableCollection<PageNavItem> Pages { get; } = new();

    [ObservableProperty] private bool _hasUnsavedChanges;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private int _warningCount;

    /// <summary>The text the view should load into the editor once, at startup.</summary>
    public string InitialText => _text;

    /// <summary>Raised after each analyze so the view can refresh squiggles/colorizing.</summary>
    public event Action? AnalysisUpdated;

    /// <summary>Asks the view to scroll/caret to a 1-based line (problem/page jump).</summary>
    public event Action<int>? RequestScrollToLine;

    /// <summary>Raised after a successful or failed export build (the view shows the summary).</summary>
    public event Action<BuildResult>? ExportCompleted;

    /// <summary>Raised when an export can't even run (e.g. no manifest yet).</summary>
    public event Action<string>? ExportFailed;

    /// <summary>Called by the view on every edit. Schedules a debounced save + re-analyze.</summary>
    public void UpdateText(string text)
    {
        if (!_loaded) return;
        _text = text;
        HasUnsavedChanges = true;
        _saveTimer.Stop(); _saveTimer.Start();
        _analyzeTimer.Stop(); _analyzeTimer.Start();
    }

    /// <summary>Re-read assets after the map/tags change, then re-validate.</summary>
    public void RefreshAssets()
    {
        Assets = AssetCatalog.Load(Tease.FolderPath);
        RunAnalyze();
    }

    /// <summary>Persist now (called before export and when the editor loses focus / tool switches).</summary>
    public void FlushSave()
    {
        _saveTimer.Stop();
        if (!HasUnsavedChanges) return;
        File.WriteAllText(_scriptPath, _text);
        HasUnsavedChanges = false;
    }

    /// <summary>Update the active page highlight from the caret's line.</summary>
    public void SetCaretLine(int line)
    {
        foreach (PageNavItem p in Pages)
            p.IsActive = line >= p.Line && line <= p.EndLine;
    }

    private void RunAnalyze()
    {
        _analyzeTimer.Stop();
        CompileResult result = _compiler.Compile(_text, Assets);
        Diagnostics = result.Diagnostics;
        PageNames = result.Pages.Select(p => p.Name).ToList();
        ErrorCount = result.Diagnostics.Count(d => d.Severity == Severity.Error);
        WarningCount = result.Diagnostics.Count(d => d.Severity == Severity.Warning);

        RebuildProblems(result);
        RebuildOutline(result);
        AnalysisUpdated?.Invoke();
    }

    private void RebuildProblems(CompileResult result)
    {
        Problems.Clear();
        foreach (Diagnostic d in result.Diagnostics)
            Problems.Add(new ProblemItem(d));
    }

    private void RebuildOutline(CompileResult result)
    {
        Pages.Clear();
        foreach (PageOutline p in result.Pages)
        {
            bool hasProblem = result.Diagnostics.Any(d =>
                d.Severity == Severity.Error && d.Line >= p.Line && d.Line <= p.EndLine);
            Pages.Add(new PageNavItem(p.Name, p.Line, p.EndLine) { HasProblem = hasProblem });
        }
    }

    [RelayCommand]
    private void Export()
    {
        FlushSave();
        try
        {
            BuildResult result = TeaseScriptCompiler.Build(Tease.FolderPath);
            RunAnalyze();
            ExportCompleted?.Invoke(result);
        }
        catch (FileNotFoundException ex)
        {
            ExportFailed?.Invoke(ex.Message +
                "\n\nExport the stub tease.json from the Milovana editor first (workflow step 5).");
        }
        catch (Exception ex)
        {
            ExportFailed?.Invoke(ex.Message);
        }
    }

    [RelayCommand]
    private void JumpToProblem(ProblemItem? item)
    {
        if (item is { Line: > 0 }) RequestScrollToLine?.Invoke(item.Line);
    }

    [RelayCommand]
    private void JumpToPage(PageNavItem? item)
    {
        if (item is not null) RequestScrollToLine?.Invoke(item.Line);
    }
}

/// <summary>A row in the Problems panel (display + jump target). Wraps a compiler <see cref="Diagnostic"/>.</summary>
public sealed class ProblemItem
{
    public ProblemItem(Diagnostic d)
    {
        Severity = d.Severity;
        Message = d.Message;
        Line = d.Line;
        Location = d.Line > 0 ? $"script.md:{d.Line}" : "script.md";
    }

    public Severity Severity { get; }
    public bool IsError => Severity == Severity.Error;
    public string Message { get; }
    public int Line { get; }
    public string Location { get; }
}

/// <summary>A page in the left outline; <see cref="IsActive"/> follows the caret, <see cref="HasProblem"/>
/// flags an error inside the page's line range.</summary>
public sealed partial class PageNavItem : ObservableObject
{
    public PageNavItem(string name, int line, int endLine)
    {
        Name = name;
        Line = line;
        EndLine = endLine;
    }

    public string Name { get; }
    public int Line { get; }
    public int EndLine { get; }

    [ObservableProperty] private bool _hasProblem;
    [ObservableProperty] private bool _isActive;
}
