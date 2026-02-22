using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Launchbox.Helpers;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _isSuppressingNotifications;

    public void AddRange(IEnumerable<T> items)
    {
        _isSuppressingNotifications = true;
        try
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _isSuppressingNotifications = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        _isSuppressingNotifications = true;
        try
        {
            Clear();
            foreach (var item in items)
            {
                Add(item);
            }
        }
        finally
        {
            _isSuppressingNotifications = false;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_isSuppressingNotifications)
        {
            base.OnCollectionChanged(e);
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_isSuppressingNotifications)
        {
            base.OnPropertyChanged(e);
        }
    }
}
