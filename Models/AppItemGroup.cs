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

    // True while internal ReplaceAll operations are running; suppresses the mutation guard
    // so the base class methods can be called without triggering NotSupportedException.
    // Also true during base-class construction (field initializer runs before base ctor).
    private bool _suppressGuard = true;

    public new void Add(AppItem item) =>
        throw new NotSupportedException(
            $"Direct Add/Insert bypasses _allItems and corrupts filter state. Use the {nameof(AppItemGroup)}-level APIs.");

    // Wraps ReplaceAll with the mutation guard suppressed, so ApplyFilter and ApplyCollapseState
    // can batch-replace the visible collection without the guard interfering.
    private void ReplaceAllGuarded(IEnumerable<AppItem> items)
    {
        _suppressGuard = true;
        try
        {
            ReplaceAll(items);
        }
        finally
        {
            _suppressGuard = false;
        }
    }

    protected override void InsertItem(int index, AppItem item)
    {
        if (!_suppressGuard)
            throw new NotSupportedException(
                $"Direct Add/Insert bypasses _allItems and corrupts filter state. Use the {nameof(AppItemGroup)}-level APIs.");
        base.InsertItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        if (!_suppressGuard)
            throw new NotSupportedException(
                $"Direct Remove bypasses _allItems and corrupts filter state. Use the {nameof(AppItemGroup)}-level APIs.");
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        if (!_suppressGuard)
            throw new NotSupportedException(
                $"Direct Clear bypasses _allItems and corrupts filter state. Use the {nameof(AppItemGroup)}-level APIs.");
        base.ClearItems();
    }

    protected override void SetItem(int index, AppItem item)
    {
        if (!_suppressGuard)
            throw new NotSupportedException(
                $"Indexer set bypasses _allItems and corrupts filter state. Use the {nameof(AppItemGroup)}-level APIs.");
        base.SetItem(index, item);
    }

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
        _suppressGuard = false; // guard now active; public mutation APIs will throw
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
            bool hasMatches = HasMatches(filterText);

            if (hasMatches && Count == 0)
            {
                // Restore placeholder so header reappears
                ReplaceAllGuarded([COLLAPSED_PLACEHOLDER]);
            }
            else if (!hasMatches && Count > 0)
            {
                // No matches — hide the group entirely
                ReplaceAllGuarded([]);
            }
            return;
        }

        var source = GetFilteredItems(filterText);

        // Minimize churn: only replace if the set actually changed
        if (source.SequenceEqual(this)) return;

        ReplaceAllGuarded(source);
    }

    private void ApplyCollapseState()
    {
        if (_isCollapsed)
        {
            // Evaluate filter to decide if collapsed header should be visible
            bool hasMatches = HasMatches(_activeFilter);

            ReplaceAllGuarded(hasMatches ? [COLLAPSED_PLACEHOLDER] : []);
        }
        else
        {
            // Expand: re-apply the active filter (don't restore unfiltered _allItems)
            var source = GetFilteredItems(_activeFilter);

            ReplaceAllGuarded(source);
        }
    }

    private bool HasMatches(string? filterText) =>
        string.IsNullOrEmpty(filterText) ||
        _allItems.Any(a => a.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase));

    private List<AppItem> GetFilteredItems(string? filterText) =>
        string.IsNullOrEmpty(filterText)
            ? _allItems // No filter: return the live list directly (ReplaceAll reads but never mutates it)
            : _allItems.Where(a => a.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase)).ToList();
}
