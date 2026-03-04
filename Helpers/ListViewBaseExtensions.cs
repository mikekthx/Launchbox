using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace Launchbox.Helpers;

public static class ListViewBaseExtensions
{
    public static readonly DependencyProperty COMMAND_PROPERTY =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ListViewBaseExtensions),
            new PropertyMetadata(null, OnCommandPropertyChanged));

    public static void SetCommand(DependencyObject d, ICommand value)
    {
        d.SetValue(COMMAND_PROPERTY, value);
    }

    public static ICommand GetCommand(DependencyObject d)
    {
        return (ICommand)d.GetValue(COMMAND_PROPERTY);
    }

    private static void OnCommandPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListViewBase listViewBase)
        {
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
}
