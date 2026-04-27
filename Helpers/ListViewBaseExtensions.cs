using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System.Windows.Input;
using Windows.System;

namespace Launchbox.Helpers;

/// <summary>
/// Attached properties that route <see cref="ListViewBase"/> events to an
/// <see cref="ICommand"/>, enabling MVVM-style binding without code-behind.
/// </summary>
public static class ListViewBaseExtensions
{
    // WinUI DependencyProperty fields follow the [PropertyName]Property PascalCase convention,
    // which conflicts with this project's UPPER_SNAKE_CASE rule for static readonly fields.
#pragma warning disable IDE1006
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnCommandPropertyChanged));

    public static readonly DependencyProperty DragItemsCompletedCommandProperty =
        DependencyProperty.RegisterAttached(
            "DragItemsCompletedCommand",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnDragItemsCompletedCommandPropertyChanged));
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
            listViewBase.KeyDown -= OnKeyDown;

            if (e.NewValue is ICommand)
            {
                listViewBase.ItemClick += OnItemClick;
                listViewBase.KeyDown += OnKeyDown;
            }
        }
    }

    private static void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        // Always consume Enter at the grid level — prevent bubbling to parent scroll containers.
        e.Handled = true;
        if (sender is ListViewBase listViewBase)
        {
            var command = GetCommand(listViewBase);
            var focused = FocusManager.GetFocusedElement(listViewBase.XamlRoot) as SelectorItem;
            if (focused?.DataContext != null && command != null && command.CanExecute(focused.DataContext))
            {
                command.Execute(focused.DataContext);
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
