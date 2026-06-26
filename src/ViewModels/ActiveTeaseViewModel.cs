using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MilovanaEosEditor;

/// <summary>
/// The editor for one open tease: the nav rail (which tool is selected, and whether it's collapsed)
/// plus the currently-hosted tool view-model. Opens on <see cref="ToolArea.Workflow"/> (the mockup
/// default). The three tool VMs are created up-front and swapped — cheap, and keeps their state.
/// </summary>
public partial class ActiveTeaseViewModel : ObservableObject
{
    public ActiveTeaseViewModel(TeaseInfo tease)
    {
        Tease = tease;
        Workflow = new WorkflowViewModel(tease, area => SelectedTool = area);
        TagImages = new TagImagesViewModel(tease);
        ScriptEditor = new ScriptEditorViewModel(tease);
        _currentTool = Workflow;
    }

    public TeaseInfo Tease { get; }

    public WorkflowViewModel Workflow { get; }
    public TagImagesViewModel TagImages { get; }
    public ScriptEditorViewModel ScriptEditor { get; }

    [ObservableProperty] private ToolArea _selectedTool = ToolArea.Workflow;
    [ObservableProperty] private object _currentTool;
    [ObservableProperty] private bool _railOpen = true;

    partial void OnSelectedToolChanged(ToolArea value) => CurrentTool = value switch
    {
        ToolArea.Workflow => Workflow,
        ToolArea.TagImages => TagImages,
        ToolArea.ScriptEditor => ScriptEditor,
        _ => Workflow,
    };

    [RelayCommand]
    private void ToggleRail() => RailOpen = !RailOpen;
}
