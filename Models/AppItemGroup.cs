using Launchbox.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Launchbox.Models;

/// <summary>
/// A group of AppItems for a single folder. Supports collapse/expand by mutating the
/// inner collection (clear/restore) rather than returning new group instances.
/// Uses BulkObservableCollection to batch UI notifications during filter/collapse operations.
/// WinUI 3's CollectionViewSource hides groups with 0 items, so collapsed groups retain
/// a single invisible placeholder to keep the header visible — unless the active filter
/// produces zero matches, in which case the group is emptied so CVS hides it.
///
/// THREADING: All public methods that mutate the collection (ApplyFilter, IsCollapsed setter)
/// must be called from the UI thread only — ObservableCollection is not thread-safe.
/// </summary>
public class AppItemGroup : BulkObservableCollection<AppItem>
{
    /// <summary>Sentinel item kept in collapsed groups so CVS doesn't hide the header.</summary>
    private static readonly AppItem COLLAPSED_PLACEHOLDER = new() { Name = string.Empty, Path = string.Empty };

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
    /// For collapsed groups: evaluates the filter against _allItems. If zero items match,
    /// the group is emptied (CVS hides the header). If items match, the placeholder stays.
    /// </summary>
    public void ApplyFilter(string? filterText)
    {
        _activeFilter = filterText;

        if (_isCollapsed)
        {
            // Evaluate the filter to decide whether the collapsed header should be visible
            bool hasMatches = string.IsNullOrEmpty(filterText)
                || _allItems.Any(a => a.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase));

            if (hasMatches && Count == 0)
            {
                // Restore placeholder so header reappears
                ReplaceAll([COLLAPSED_PLACEHOLDER]);
            }
            else if (!hasMatches && Count > 0)
            {
                // No matches — hide the group entirely
                ReplaceAll([]);
            }
            return;
        }

        var source = string.IsNullOrEmpty(filterText)
            ? _allItems
            : _allItems.Where(a => a.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        // Minimize churn: only replace if the set actually changed
        if (source.SequenceEqual(this)) return;

        ReplaceAll(source);
    }

    private void ApplyCollapseState()
    {
        if (_isCollapsed)
        {
            // Evaluate filter to decide if collapsed header should be visible
            bool hasMatches = string.IsNullOrEmpty(_activeFilter)
                || _allItems.Any(a => a.Name.Contains(_activeFilter, StringComparison.OrdinalIgnoreCase));

            ReplaceAll(hasMatches ? [COLLAPSED_PLACEHOLDER] : []);
        }
        else
        {
            // Expand: re-apply the active filter (don't restore unfiltered _allItems)
            var source = string.IsNullOrEmpty(_activeFilter)
                ? _allItems
                : _allItems.Where(a => a.Name.Contains(_activeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            ReplaceAll(source);
        }
    }
}
