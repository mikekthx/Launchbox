// WinUI XAML attached-property resolution requires the field name to match "[Name]Property"
// (PascalCase), which conflicts with the project's static readonly UPPER_SNAKE_CASE rule.
#pragma warning disable IDE1006

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace Launchbox.Helpers;

/// <summary>
/// Attached properties that route <see cref="ListViewBase"/> events to an
/// <see cref="ICommand"/>, enabling MVVM-style binding without code-behind.
/// </summary>
public static class ListViewBaseExtensions
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnCommandPropertyChanged));

    public static void SetCommand(DependencyObject d, ICommand value)
    {
        d.SetValue(CommandProperty, value);
    }

    public static ICommand GetCommand(DependencyObject d)
    {
        return (ICommand)d.GetValue(CommandProperty);
    }

    private static void OnCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewBase listViewBase)
        {
            // Unsubscribe first to prevent duplicate handler registration if the command changes.
            listViewBase.ItemClick -= OnItemClick;

            if (e.NewValue is ICommand)
            {
                listViewBase.ItemClick += OnItemClick;
            }
        }
    }

    private static void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (sender is ListViewBase listViewBase)
        {
            var command = GetCommand(listViewBase);
            if (command != null && command.CanExecute(e.ClickedItem))
            {
                command.Execute(e.ClickedItem);
            }
        }
    }

    public static readonly DependencyProperty DragItemsCompletedCommandProperty =
        DependencyProperty.RegisterAttached(
            "DragItemsCompletedCommand",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnDragItemsCompletedCommandPropertyChanged));

    public static void SetDragItemsCompletedCommand(DependencyObject d, ICommand value)
    {
        d.SetValue(DragItemsCompletedCommandProperty, value);
    }

    public static ICommand GetDragItemsCompletedCommand(DependencyObject d)
    {
        return (ICommand)d.GetValue(DragItemsCompletedCommandProperty);
    }

    private static void OnDragItemsCompletedCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewBase listViewBase)
        {
            listViewBase.DragItemsCompleted -= OnDragItemsCompleted;

            if (e.NewValue is ICommand)
            {
                listViewBase.DragItemsCompleted += OnDragItemsCompleted;
            }
        }
    }

    private static void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        var command = GetDragItemsCompletedCommand(sender);
        if (command != null && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }
}
