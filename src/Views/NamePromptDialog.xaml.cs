using System.Windows;

namespace MilovanaEosEditor;

/// <summary>Small modal that collects + validates a new tease name (the future folder name).</summary>
public partial class NamePromptDialog : Window
{
    public NamePromptDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    /// <summary>The validated, trimmed name. Only meaningful when the dialog returned <c>true</c>.</summary>
    public string EnteredName { get; private set; } = string.Empty;

    private void OnNameChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Clear a previous error as soon as the user edits.
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string? error = TeaseWorkspace.ValidateName(name);
        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        EnteredName = name;
        DialogResult = true;
    }
}
