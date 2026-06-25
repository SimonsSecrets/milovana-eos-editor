using System.Windows;

namespace MilovanaEosEditor;

/// <summary>
/// The application shell. A thin host: it owns the <see cref="ShellViewModel"/> and renders the header
/// chrome + whichever body (tease browser or active-tease editor) the shell selects. All behaviour
/// lives in the view-models; this code-behind only wires the DataContext.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellViewModel();
    }
}
