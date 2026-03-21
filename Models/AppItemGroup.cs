using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Launchbox.Models;

/// <summary>
/// A group of AppItems for a single folder. Supports collapse/expand by mutating the
/// inner ObservableCollection (clear/restore) rather than returning new group instances.
/// WinUI 3's CollectionViewSource hides groups with 0 items, so collapsed groups retain
/// a single invisible placeholder to keep the header visible.
///
/// THREADING: All public methods that mutate the collection (ApplyFilter, IsCollapsed setter)
/// must be called from the UI thread only — ObservableCollection is not thread-safe.
/// </summary>
public class AppItemGroup : ObservableCollection<AppItem>
{
    /// <summary>Sentinel item kept in collapsed groups so CVS doesn't hide the header.</summary>
    private static readonly AppItem CollapsedPlaceholder = new() { Name = string.Empty, Path = string.Empty };

    public string Label { get; }

    /// <summary>
    /// Stable identity for this group — the folder's original path.
    /// Used for collapse-state matching when labels are duplicated or filtered copies are in play.
    /// </summary>
    public string FolderPath { get; }

    // Backup of the full item list — populated on construction, used for expand/restore
    private readonly List<AppItem> _allItems;

    // Tracks the active filter text so expand can re-apply it
    private string? _activeFilter;

    private bool _isCollapsed;
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsCollapsed)));
                ApplyCollapseState();
            }
        }
    }

    public AppItemGroup(string label, string folderPath, IEnumerable<AppItem> items) : base(items)
    {
        Label = label;
        FolderPath = folderPath;
        _allItems = [.. this]; // snapshot at construction
    }

    /// <summary>
    /// Replaces the visible items with a filtered subset. Preserves the backup for expand.
    /// For collapsed groups: stores the filter but does not change visible items (placeholder stays).
    /// If the filter produces 0 matches and the group is not collapsed, the group is emptied
    /// (CVS will hide the header, which is correct — no matching items means no group to show).
    /// </summary>
    public void ApplyFilter(string? filterText)
    {
        _activeFilter = filterText;

        if (_isCollapsed) return; // collapsed groups keep placeholder; filter applied on expand

        var source = string.IsNullOrEmpty(filterText)
            ? _allItems
            : _allItems.Where(a => a.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        // Minimize churn: only replace if the set actually changed
        if (source.SequenceEqual(this)) return;

        Clear();
        foreach (var item in source) Add(item);
    }

    private void ApplyCollapseState()
    {
        Clear();
        if (_isCollapsed)
        {
            // WinUI 3 CVS hides 0-item groups — keep one placeholder so header stays visible
            Add(CollapsedPlaceholder);
        }
        else
        {
            // Expand: re-apply the active filter (don't restore unfiltered _allItems)
            var source = string.IsNullOrEmpty(_activeFilter)
                ? _allItems
                : _allItems.Where(a => a.Name.Contains(_activeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var item in source) Add(item);
        }
    }
}
