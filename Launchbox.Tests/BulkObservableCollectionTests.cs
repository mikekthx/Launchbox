using Launchbox.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Xunit;

namespace Launchbox.Tests;

public class BulkObservableCollectionTests
{
    [Fact]
    public void AddRange_AddsItemsAndFiresEventsOnce()
    {
        // Arrange
        var collection = new BulkObservableCollection<int>();
        var collectionChangedCount = 0;
        var propertyChangedCount = 0;
        var propertiesChanged = new List<string?>();

        collection.CollectionChanged += (s, e) =>
        {
            collectionChangedCount++;
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
        };

        ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) =>
        {
            propertyChangedCount++;
            propertiesChanged.Add(e.PropertyName);
        };

        // Act
        collection.AddRange(new[] { 1, 2, 3 });

        // Assert
        Assert.Equal(3, collection.Count);
        Assert.Equal(1, collection[0]);
        Assert.Equal(2, collection[1]);
        Assert.Equal(3, collection[2]);

        Assert.Equal(1, collectionChangedCount);
        Assert.Equal(2, propertyChangedCount);
        Assert.Contains("Count", propertiesChanged);
        Assert.Contains("Item[]", propertiesChanged);
    }

    [Fact]
    public void ReplaceAll_ReplacesItemsAndFiresEventsOnce()
    {
        // Arrange
        var collection = new BulkObservableCollection<int> { 1, 2, 3 };
        var collectionChangedCount = 0;
        var propertyChangedCount = 0;
        var propertiesChanged = new List<string?>();

        collection.CollectionChanged += (s, e) =>
        {
            collectionChangedCount++;
            Assert.Equal(NotifyCollectionChangedAction.Reset, e.Action);
        };

        ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) =>
        {
            propertyChangedCount++;
            propertiesChanged.Add(e.PropertyName);
        };

        // Act
        collection.ReplaceAll(new[] { 4, 5 });

        // Assert
        Assert.Equal(2, collection.Count);
        Assert.Equal(4, collection[0]);
        Assert.Equal(5, collection[1]);

        Assert.Equal(1, collectionChangedCount);
        Assert.Equal(2, propertyChangedCount);
        Assert.Contains("Count", propertiesChanged);
        Assert.Contains("Item[]", propertiesChanged);
    }

    [Fact]
    public void AddRange_WhenEmpty_StillFiresEvents()
    {
        // Arrange
        var collection = new BulkObservableCollection<int>();
        var collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) => collectionChangedCount++;

        // Act
        collection.AddRange(Array.Empty<int>());

        // Assert
        Assert.Empty(collection);
        Assert.Equal(1, collectionChangedCount);
    }

    [Fact]
    public void ReplaceAll_WhenEmpty_ClearsAndFiresEvents()
    {
        // Arrange
        var collection = new BulkObservableCollection<int> { 1, 2 };
        var collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) => collectionChangedCount++;

        // Act
        collection.ReplaceAll(Array.Empty<int>());

        // Assert
        Assert.Empty(collection);
        Assert.Equal(1, collectionChangedCount);
    }

    private IEnumerable<int> ThrowingEnumerator()
    {
        yield return 1;
        throw new InvalidOperationException("Test exception");
    }

    [Fact]
    public void AddRange_WhenEnumeratorThrows_ResetsSuppression()
    {
        // Arrange
        var collection = new BulkObservableCollection<int>();
        var collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) => collectionChangedCount++;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => collection.AddRange(ThrowingEnumerator()));

        Assert.Single(collection); // 1 was yielded before the exception
        Assert.Equal(1, collectionChangedCount); // Finally block still executed and fired events

        // Verify suppression is off by adding normally
        collectionChangedCount = 0;
        collection.Add(2);
        Assert.Equal(1, collectionChangedCount);
    }

    [Fact]
    public void ReplaceAll_WhenEnumeratorThrows_ResetsSuppression()
    {
        // Arrange
        var collection = new BulkObservableCollection<int> { 10, 20 };
        var collectionChangedCount = 0;

        collection.CollectionChanged += (s, e) => collectionChangedCount++;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => collection.ReplaceAll(ThrowingEnumerator()));

        Assert.Single(collection); // Collection was cleared, then 1 was added
        Assert.Equal(1, collectionChangedCount); // Finally block still executed

        // Verify suppression is off by adding normally
        collectionChangedCount = 0;
        collection.Add(2);
        Assert.Equal(1, collectionChangedCount);
    }

    [Fact]
    public void NormalAdd_FiresEventsNormally()
    {
        // Arrange
        var collection = new BulkObservableCollection<int>();
        var collectionChangedCount = 0;
        var propertyChangedCount = 0;

        collection.CollectionChanged += (s, e) =>
        {
            collectionChangedCount++;
            Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
        };

        ((INotifyPropertyChanged)collection).PropertyChanged += (s, e) => propertyChangedCount++;

        // Act
        collection.Add(1);

        // Assert
        Assert.Single(collection);
        Assert.Equal(1, collectionChangedCount);
        Assert.Equal(2, propertyChangedCount); // Count and Item[]
    }
}
