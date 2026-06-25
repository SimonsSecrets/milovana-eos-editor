using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MilovanaEosEditor;

/// <summary>
/// Lets elements that live outside the visual tree (e.g. items inside a <c>CompositeCollection</c>)
/// reach the DataContext. The browser uses it so the trailing "create new" ghost card can bind to the
/// view-model's <c>NewTeaseCommand</c> while sitting alongside the data-bound tease cards.
/// </summary>
public sealed class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
}

/// <summary>
/// True when the bound enum value equals the ConverterParameter name. Used to bind the nav-rail
/// RadioButtons' <c>IsChecked</c> to <c>ActiveTeaseViewModel.SelectedTool</c> (checking one sets it).
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is not null && value.ToString() == parameter.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b && parameter is not null
            ? Enum.Parse(targetType, parameter.ToString()!)
            : Binding.DoNothing;
}

/// <summary>Maps a bool to Visibility, with an optional <c>Invert</c> for "collapse when true".</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public bool UseHidden { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool on = value is bool b && b;
        if (Invert) on = !on;
        return on ? Visibility.Visible : (UseHidden ? Visibility.Hidden : Visibility.Collapsed);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible ? !Invert : Invert;
}
