using CommunityToolkit.Mvvm.ComponentModel;

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

public sealed class WorkflowViewModel : ToolViewModel
{
    public WorkflowViewModel(TeaseInfo tease) : base(tease) { }
    public override string Title => "Workflow";
    public override string Subtitle => "Pipeline state and one-click automations for this tease.";
}

public sealed class TagImagesViewModel : ToolViewModel
{
    public TagImagesViewModel(TeaseInfo tease) : base(tease) { }
    public override string Title => "Tag Images";
    public override string Subtitle => "Review galleries and tag each image (subject, pace, explicitness).";
}

public sealed class ScriptEditorViewModel : ToolViewModel
{
    public ScriptEditorViewModel(TeaseInfo tease) : base(tease) { }
    public override string Title => "Script Editor";
    public override string Subtitle => "Write the tease in script.md with inline previews and live validation.";
}
