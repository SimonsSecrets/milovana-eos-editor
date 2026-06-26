using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MilovanaEosEditor;

/// <summary>The three per-tease tools selectable from the nav rail.</summary>
public enum ToolArea
{
    Workflow,
    TagImages,
    ScriptEditor,
}

/// <summary>
/// Base for the per-tease tool view-models. The tool main views are placeholders for now — they get
/// real content in follow-up tasks (migrating the legacy tagging UI into Tag Images, building the
/// Workflow dashboard and the Script Editor). Each just carries the active tease + display text.
/// </summary>
public abstract class ToolViewModel : ObservableObject
{
    protected ToolViewModel(TeaseInfo tease) => Tease = tease;

    public TeaseInfo Tease { get; }
    public abstract string Title { get; }
    public abstract string Subtitle { get; }
}

/// <summary>State of a single pipeline step, derived from which files exist on disk.</summary>
public enum WorkflowStepState
{
    Done,     // the step's condition is satisfied
    Current,  // first not-yet-done step — what the author should act on next
    Todo,     // a later, not-yet-reached step
}

/// <summary>Whether a step offers an action button, and of which flavour.</summary>
public enum WorkflowActionKind
{
    None,  // no button
    Open,  // navigates to another tool (enabled)
    Run,   // an automation the tool will perform (disabled until the backend lands)
}

/// <summary>
/// One row of the Workflow pipeline timeline. A plain bindable record — the view-model computes all
/// of these up-front from <see cref="TeaseInfo"/>, so the template just reads pre-derived values.
/// </summary>
public sealed class WorkflowStep
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required string Who { get; init; }
    public required string Info { get; init; }
    public required string Signal { get; init; }
    public required WorkflowStepState State { get; init; }
    public bool IsLast { get; init; }

    public WorkflowActionKind ActionKind { get; init; } = WorkflowActionKind.None;
    public string ActionLabel { get; init; } = string.Empty;
    public IRelayCommand? ActionCommand { get; init; }

    public bool IsCurrent => State == WorkflowStepState.Current;
    public bool ActionEnabled => ActionKind == WorkflowActionKind.Open;

    /// <summary>Glyph inside the dot: ✓ when done, the step number while still to-do, blank when current.</summary>
    public string Glyph => State switch
    {
        WorkflowStepState.Done => "✓",
        WorkflowStepState.Current => string.Empty,
        _ => Number.ToString(),
    };
}

public sealed class WorkflowViewModel : ToolViewModel
{
    public WorkflowViewModel(TeaseInfo tease, Action<ToolArea> navigate) : base(tease)
    {
        Steps = BuildSteps(navigate);
    }

    public override string Title => "Workflow";
    public override string Subtitle => "Pipeline state and one-click automations for this tease.";

    public IReadOnlyList<WorkflowStep> Steps { get; }

    /// <summary>
    /// Builds the nine pipeline steps, deriving each step's "done" purely from files on disk (the
    /// page's subtitle promises exactly this). The first step that isn't done becomes the current
    /// step; everything after it is to-do.
    /// </summary>
    private IReadOnlyList<WorkflowStep> BuildSteps(Action<ToolArea> navigate)
    {
        var t = Tease;
        bool hasBuckets = t.BucketCount > 0;
        bool hasImages = t.ImageCount > 0;
        bool hasManifest = t.HasAssetMap || t.HasTease; // a map can only exist if the manifest was exported

        // (title, who, info, done, signal-when-done, signal-when-not, action)
        var specs = new (string Title, string Who, string Info, bool Done, string DoneSignal, string PendingSignal, WorkflowActionKind Action, string ActionLabel, ToolArea? Target)[]
        {
            ("Create the tease", "Author",
                "Set up the local tease folder with its Gallery/ and Files/ subfolders, ready to receive the script and assets.",
                t.Exists, "folder created", "folder missing", WorkflowActionKind.None, "", null),

            ("Write the script", "Author + Claude",
                "Author the tease page by page in script.md using the marker DSL ([PAGE], [SAY], [IMAGE], [METRONOME], [CHOICE], [GOTO]…); image markers note bucket intent only — exact filenames come later.",
                t.HasScript, "script.md present", "no script yet", WorkflowActionKind.Open, "Script Editor", ToolArea.ScriptEditor),

            ("Plan assets", "Claude → Author",
                "With Claude, agree the themed gallery buckets (one per mood/pace, e.g. solo-sensual → machine-hard) and the audio files the script needs.",
                hasBuckets, $"{t.BucketCount} bucket{(t.BucketCount == 1 ? "" : "s")}", "no buckets yet", WorkflowActionKind.None, "", null),

            ("Set up & upload assets", "Author",
                "Create a Gallery/<bucket>/ folder per bucket plus Files/, add the exact image and audio sources, then upload matching galleries in the Milovana editor.",
                hasImages, $"{t.ImageCount} image{(t.ImageCount == 1 ? "" : "s")} in {t.BucketCount} bucket{(t.BucketCount == 1 ? "" : "s")}", "no images yet", WorkflowActionKind.None, "", null),

            ("Export the manifest", "Author · in Milovana",
                "In the Milovana editor, export the stub tease.json so the tool has the site's gallery/file manifest. This is a manual step done on the website.",
                hasManifest, "manifest present", "not exported", WorkflowActionKind.None, "", null),

            ("Generate the asset map", "Tool",
                "Run the tool to SHA-1-join the manifest against your local files into asset-map.json (also seeds empty tag stubs for any new images). Idempotent and safe to re-run.",
                t.HasAssetMap, "asset-map.json", "not generated", WorkflowActionKind.Run, "Re-run", null),

            ("Tag & verify images", "Claude → Author",
                "Vision-tag each image (subject, pace, explicitness) into asset-content.json; Claude proposes the tags and you review and confirm them.",
                t.HasContent, "tags present", "not tagged", WorkflowActionKind.Open, "Tag Images", ToolArea.TagImages),

            ("Generate the tease", "Tool",
                "Resolve every script [IMAGE]/audio reference to a locator (joining tags with the asset map) and compile script.md into the final playable tease.json.",
                t.HasTease, "tease.json", "not generated", WorkflowActionKind.Run, "Generate", null),

            ("Upload & test", "Author",
                "Upload tease.json to Milovana and play it through to confirm everything works; loop back to earlier steps on feedback.",
                false, "plays correctly", "plays correctly", WorkflowActionKind.None, "", null),
        };

        // The current step is the first one whose condition isn't satisfied; if all detectable steps
        // are done, the final "Upload & test" step becomes current.
        int currentIndex = Array.FindIndex(specs, s => !s.Done);
        if (currentIndex < 0) currentIndex = specs.Length - 1;

        var steps = new List<WorkflowStep>(specs.Length);
        for (int i = 0; i < specs.Length; i++)
        {
            var spec = specs[i];
            WorkflowStepState state = spec.Done
                ? WorkflowStepState.Done
                : i == currentIndex ? WorkflowStepState.Current : WorkflowStepState.Todo;

            IRelayCommand? command = spec.Action switch
            {
                WorkflowActionKind.Open when spec.Target is ToolArea target => new RelayCommand(() => navigate(target)),
                _ => null,
            };

            steps.Add(new WorkflowStep
            {
                Number = i + 1,
                Title = spec.Title,
                Who = spec.Who,
                Info = spec.Info,
                Signal = spec.Done ? spec.DoneSignal : spec.PendingSignal,
                State = state,
                IsLast = i == specs.Length - 1,
                ActionKind = spec.Action,
                ActionLabel = spec.ActionLabel,
                ActionCommand = command,
            });
        }

        return steps;
    }
}

public sealed class ScriptEditorViewModel : ToolViewModel
{
    public ScriptEditorViewModel(TeaseInfo tease) : base(tease) { }
    public override string Title => "Script Editor";
    public override string Subtitle => "Write the tease in script.md with inline previews and live validation.";
}
