using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics.CodeAnalysis;
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

    public static readonly DependencyProperty EnterCommandProperty =
        DependencyProperty.RegisterAttached(
            "EnterCommand",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnEnterCommandPropertyChanged));
#pragma warning restore IDE1006

    // ExcludeFromCodeCoverage rationale: every member below is WinUI attached-property
    // plumbing — DP get/set or event handlers that route to ListViewBase. None can be
    // exercised from this project's file-linked xUnit setup without a live WinUI host
    // (which throws COMException in unpackaged test contexts). The pure decision logic
    // is hoisted into TryExecuteEnterCommand, which IS covered by unit tests.

    [ExcludeFromCodeCoverage]
    public static void SetCommand(DependencyObject d, ICommand value)
    {
        d.SetValue(CommandProperty, value);
    }

    [ExcludeFromCodeCoverage]
    public static ICommand GetCommand(DependencyObject d)
    {
        return (ICommand)d.GetValue(CommandProperty);
    }

    [ExcludeFromCodeCoverage]
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

    [ExcludeFromCodeCoverage]
    private static void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (sender is ListViewBase listViewBase)
        {
            var command = GetCommand(listViewBase);
            if (command is not null && command.CanExecute(e.ClickedItem))
            {
                command.Execute(e.ClickedItem);
            }
        }
    }

    [ExcludeFromCodeCoverage]
    public static void SetDragItemsCompletedCommand(DependencyObject d, ICommand value)
    {
        d.SetValue(DragItemsCompletedCommandProperty, value);
    }

    [ExcludeFromCodeCoverage]
    public static ICommand GetDragItemsCompletedCommand(DependencyObject d)
    {
        return (ICommand)d.GetValue(DragItemsCompletedCommandProperty);
    }

    [ExcludeFromCodeCoverage]
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

    [ExcludeFromCodeCoverage]
    private static void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        var command = GetDragItemsCompletedCommand(sender);
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    [ExcludeFromCodeCoverage]
    public static void SetEnterCommand(DependencyObject d, ICommand value)
    {
        d.SetValue(EnterCommandProperty, value);
    }

    [ExcludeFromCodeCoverage]
    public static ICommand GetEnterCommand(DependencyObject d)
    {
        return (ICommand)d.GetValue(EnterCommandProperty);
    }

    [ExcludeFromCodeCoverage]
    private static void OnEnterCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewBase listViewBase)
        {
            listViewBase.KeyDown -= OnListViewBaseKeyDown;

            if (e.NewValue is ICommand)
            {
                listViewBase.KeyDown += OnListViewBaseKeyDown;
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private static void OnListViewBaseKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        if (sender is ListViewBase listViewBase)
        {
            // Cast to FrameworkElement instead of GridViewItem to support any ListViewBase
            // and cases where focus lands on a child element inside the item template.
            var focused = FocusManager.GetFocusedElement(listViewBase.XamlRoot) as FrameworkElement;
            var command = GetEnterCommand(listViewBase);

            if (TryExecuteEnterCommand(e.Key, command, focused?.DataContext))
            {
                // Only consume Enter if the command actually executes.
                e.Handled = true;
            }
        }
    }

    // Extracted as an internal method for isolated unit testing without requiring
    // the WinUI host to instantiate FocusManager or KeyRoutedEventArgs.
    internal static bool TryExecuteEnterCommand(VirtualKey key, ICommand? command, object? dataContext)
    {
        if (key != VirtualKey.Enter) return false;

        if (command is not null && dataContext is not null && command.CanExecute(dataContext))
        {
            command.Execute(dataContext);
            return true;
        }

        return false;
    }
}
