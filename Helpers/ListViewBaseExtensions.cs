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
    // The field name must match the "[Name]Property" convention to avoid static readonly
    // UPPER_SNAKE_CASE analyzer warnings, but WinUI XAML property resolution is technically
    // based on the string registered below, not the C# field name. We disable IDE1006 locally.
#pragma warning disable IDE1006
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnCommandPropertyChanged));
#pragma warning restore IDE1006

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

#pragma warning disable IDE1006
    public static readonly DependencyProperty DragItemsCompletedCommandProperty =
        DependencyProperty.RegisterAttached(
            "DragItemsCompletedCommand",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnDragItemsCompletedCommandPropertyChanged));
#pragma warning restore IDE1006

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
        if (command != null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
