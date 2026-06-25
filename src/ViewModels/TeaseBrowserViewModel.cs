using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace MilovanaEosEditor;

/// <summary>
/// The tease browser. Lists only the teases the author has registered (created or added) — never an
/// auto-scan — and lets them open one, add an existing folder, or scaffold a new tease. Opening is
/// delegated back to the shell via the <see cref="_open"/> callback so one "active tease" is shared.
/// </summary>
public partial class TeaseBrowserViewModel : ObservableObject
{
    private readonly TeaseWorkspace _workspace;
    private readonly Action<TeaseInfo> _open;

    public TeaseBrowserViewModel(TeaseWorkspace workspace, Action<TeaseInfo> open)
    {
        _workspace = workspace;
        _open = open;
        Refresh();
    }

    public ObservableCollection<TeaseInfo> Teases { get; } = new();

    public bool IsEmpty => Teases.Count == 0;

    /// <summary>Reload the registered teases (e.g. after add/create, or when returning to the browser).</summary>
    public void Refresh()
    {
        Teases.Clear();
        foreach (TeaseInfo t in _workspace.GetTeases()) Teases.Add(t);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void OpenTease(TeaseInfo? tease)
    {
        if (tease is null) return;
        if (!tease.Exists)
        {
            MessageBoxResult r = MessageBox.Show(
                $"The folder for \"{tease.Name}\" no longer exists:\n{tease.FolderPath}\n\nRemove it from the workspace?",
                "Tease folder missing", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r == MessageBoxResult.Yes)
            {
                _workspace.Remove(tease.FolderPath);
                Refresh();
            }
            return;
        }
        _open(tease);
    }

    [RelayCommand]
    private void AddExisting()
    {
        var dlg = new OpenFolderDialog { Title = "Add an existing tease — pick its folder" };
        if (dlg.ShowDialog() != true) return;

        string folder = dlg.FolderName;
        if (!TeaseWorkspace.LooksLikeTease(folder))
        {
            MessageBoxResult r = MessageBox.Show(
                "This folder doesn't look like a tease (no tease.json, script.md, asset-content.json, or Gallery/).\n\nAdd it anyway?",
                "Add existing tease", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }

        TeaseInfo info = _workspace.Add(folder);
        Refresh();
        _open(info);
    }

    [RelayCommand]
    private void NewTease()
    {
        // 1) name (validated live in the dialog)
        var prompt = new NamePromptDialog { Owner = Application.Current?.MainWindow };
        if (prompt.ShowDialog() != true) return;
        string name = prompt.EnteredName;

        // 2) where to create it — default near an existing tease / the repo Teases folder
        string? suggested = _workspace.SuggestParentDirectory();
        var folderDlg = new OpenFolderDialog
        {
            Title = $"Create \"{name}\" in…",
            InitialDirectory = suggested is not null && Directory.Exists(suggested) ? suggested : string.Empty,
        };
        if (folderDlg.ShowDialog() != true) return;

        try
        {
            TeaseInfo info = _workspace.CreateTease(folderDlg.FolderName, name);
            Refresh();
            _open(info);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Couldn't create tease", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
