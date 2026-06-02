// WinUI XAML attached-property resolution requires the field name to match "[Name]Property"
// (PascalCase), which conflicts with the project's static readonly UPPER_SNAKE_CASE rule.
#pragma warning disable IDE1006

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Windows.Input;

namespace Launchbox.Helpers;

/// <summary>
/// Attached properties that route <see cref="UIElement.Tapped"/> and <see cref="UIElement.KeyDown"/>
/// to an <see cref="ICommand"/>, enabling MVVM-style event binding without code-behind
/// for elements that do not natively support commands (like <see cref="Microsoft.UI.Xaml.Controls.Grid"/> or <see cref="Microsoft.UI.Xaml.Controls.TextBox"/>).
/// </summary>
public static class UIElementExtensions
{
    public static readonly DependencyProperty TappedCommandProperty =
        DependencyProperty.RegisterAttached(
            "TappedCommand",
            typeof(ICommand),
            typeof(UIElementExtensions),
            new PropertyMetadata(null, OnTappedCommandPropertyChanged));

    public static void SetTappedCommand(DependencyObject d, ICommand? value)
    {
        d.SetValue(TappedCommandProperty, value);
    }

    public static ICommand? GetTappedCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(TappedCommandProperty);
    }

    public static readonly DependencyProperty TappedCommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "TappedCommandParameter",
            typeof(object),
            typeof(UIElementExtensions),
            new PropertyMetadata(null));

    public static void SetTappedCommandParameter(DependencyObject d, object? value)
    {
        d.SetValue(TappedCommandParameterProperty, value);
    }

    public static object? GetTappedCommandParameter(DependencyObject d)
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

    public static readonly DependencyProperty KeyDownCommandProperty =
        DependencyProperty.RegisterAttached(
            "KeyDownCommand",
            typeof(ICommand),
            typeof(UIElementExtensions),
            new PropertyMetadata(null, OnKeyDownCommandPropertyChanged));

    public static void SetKeyDownCommand(DependencyObject d, ICommand? value)
    {
        d.SetValue(KeyDownCommandProperty, value);
    }

    public static ICommand? GetKeyDownCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(KeyDownCommandProperty);
    }

    private static void OnKeyDownCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            element.KeyDown -= Element_KeyDown;
            if (e.NewValue is ICommand)
            {
                element.KeyDown += Element_KeyDown;
            }
        }
    }

    private static void Element_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            var command = GetKeyDownCommand(element);
            if (command != null && command.CanExecute(e.Key))
            {
                // The ViewModel command must process VirtualKey, but standard ICommands return void.
                // However, since we know these specific keys (Enter, Down) are consumed by the ViewModel
                // and shouldn't propagate, we handle them here if the command successfully executes.
                if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.Down)
                {
                    command.Execute(e.Key);
                    e.Handled = true;
                }
            }
        }
    }
}
