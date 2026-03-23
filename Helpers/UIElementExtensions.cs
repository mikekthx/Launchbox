// Suppress IDE1006 (Naming rule violation) because WinUI XAML requires DependencyProperty
// identifier fields to end exactly with "Property" rather than following standard uppercase static conventions.
#pragma warning disable IDE1006

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Windows.Input;

namespace Launchbox.Helpers;

public static class UIElementExtensions
{
    public static readonly DependencyProperty TappedCommandProperty =
        DependencyProperty.RegisterAttached(
            "TappedCommand",
            typeof(ICommand),
            typeof(UIElementExtensions),
            new PropertyMetadata(null, OnTappedCommandPropertyChanged));

    public static void SetTappedCommand(DependencyObject d, ICommand value)
    {
        d.SetValue(TappedCommandProperty, value);
    }

    public static ICommand GetTappedCommand(DependencyObject d)
    {
        return (ICommand)d.GetValue(TappedCommandProperty);
    }

    public static readonly DependencyProperty TappedCommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "TappedCommandParameter",
            typeof(object),
            typeof(UIElementExtensions),
            new PropertyMetadata(null));

    public static void SetTappedCommandParameter(DependencyObject d, object value)
    {
        d.SetValue(TappedCommandParameterProperty, value);
    }

    public static object GetTappedCommandParameter(DependencyObject d)
    {
        return d.GetValue(TappedCommandParameterProperty);
    }

    private static void OnTappedCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            element.Tapped -= Element_Tapped;
            if (e.NewValue is ICommand)
            {
                element.Tapped += Element_Tapped;
            }
        }
    }

    private static void Element_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            var command = GetTappedCommand(element);
            var parameter = GetTappedCommandParameter(element);

            if (command != null && command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }
    }
}
