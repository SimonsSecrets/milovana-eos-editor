using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MilovanaEosEditor;

/// <summary>
/// Root view-model for the app shell. Holds the one shared "active tease" context and swaps the body
/// between the tease browser and the active-tease editor. The header shows the tease name + a Switch
/// button only while a tease is open (mirrors the mockup's browser-vs-editor chrome).
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly TeaseWorkspace _workspace;
    private readonly TeaseBrowserViewModel _browser;

    public ShellViewModel() : this(new TeaseWorkspace()) { }

    public ShellViewModel(TeaseWorkspace workspace)
    {
        _workspace = workspace;
        _browser = new TeaseBrowserViewModel(_workspace, OpenTease);
        _body = _browser;
    }

    [ObservableProperty] private object _body;
    [ObservableProperty] private TeaseInfo? _activeTease;

    public bool HasActiveTease => ActiveTease is not null;

    private void OpenTease(TeaseInfo tease)
    {
        ActiveTease = tease;
        Body = new ActiveTeaseViewModel(tease);
    }

    [RelayCommand]
    private void SwitchToBrowser()
    {
        ActiveTease = null;
        _browser.Refresh();
        Body = _browser;
    }

    partial void OnActiveTeaseChanged(TeaseInfo? value) => OnPropertyChanged(nameof(HasActiveTease));
}
