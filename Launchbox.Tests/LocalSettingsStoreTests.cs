using Launchbox.Services;
using System;
using System.Collections.Generic;
using Xunit;

namespace Launchbox.Tests;

public class LocalSettingsStoreTests
{
    private class MockSettingsContainer : ISettingsContainer
    {
        public Dictionary<string, object?> Store { get; } = new();
        public bool ThrowOnRead { get; set; }
        public bool ThrowOnWrite { get; set; }

        public bool TryGetValue(string key, out object? value)
        {
            if (ThrowOnRead)
            {
                throw new Exception("Simulated read failure");
            }
            return Store.TryGetValue(key, out value);
        }

        public void SetValue(string key, object? value)
        {
            if (ThrowOnWrite)
            {
                throw new Exception("Simulated write failure");
            }
            Store[key] = value;
        }

        public void SetValues(IReadOnlyDictionary<string, object?> values)
        {
            if (ThrowOnWrite)
            {
                throw new Exception("Simulated write failure");
            }
            foreach (var kvp in values)
            {
                Store[kvp.Key] = kvp.Value;
            }
        }
    }

    [Fact]
    public void TryGetValue_ReturnsValue_WhenExists()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer();
        mockContainer.Store["TestKey"] = "TestValue";
        var store = new LocalSettingsStore(mockContainer);

        // Act
        bool result = store.TryGetValue("TestKey", out var value);

        // Assert
        Assert.True(result);
        Assert.Equal("TestValue", value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalse_WhenNotExists()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer();
        var store = new LocalSettingsStore(mockContainer);

        // Act
        bool result = store.TryGetValue("NonExistentKey", out var value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValue_HandlesException_ReturnsFalse()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer { ThrowOnRead = true };
        var store = new LocalSettingsStore(mockContainer);

        // Act
        bool result = store.TryGetValue("TestKey", out var value);

        // Assert
        Assert.False(result);
        Assert.Null(value);
    }

    [Fact]
    public void SetValue_StoresValue_WhenSuccess()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer();
        var store = new LocalSettingsStore(mockContainer);

        // Act
        store.SetValue("TestKey", "TestValue");

        // Assert
        Assert.True(mockContainer.Store.ContainsKey("TestKey"));
        Assert.Equal("TestValue", mockContainer.Store["TestKey"]);
    }

    [Fact]
    public void SetValue_ReturnsTrue_OnSuccess()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer();
        var store = new LocalSettingsStore(mockContainer);

        // Act
        bool result = store.SetValue("TestKey", "TestValue");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SetValue_ReturnsFalse_OnException()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer { ThrowOnWrite = true };
        var store = new LocalSettingsStore(mockContainer);

        // Act
        bool result = store.SetValue("TestKey", "TestValue");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SetValues_StoresAllValues_WhenSuccess()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer();
        var store = new LocalSettingsStore(mockContainer);
        var values = new Dictionary<string, object?> { { "Key1", "Val1" }, { "Key2", 42 } };

        // Act
        bool result = store.SetValues(values);

        // Assert
        Assert.True(result);
        Assert.Equal("Val1", mockContainer.Store["Key1"]);
        Assert.Equal(42, mockContainer.Store["Key2"]);
    }

    [Fact]
    public void SetValues_ReturnsFalse_OnException()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer { ThrowOnWrite = true };
        var store = new LocalSettingsStore(mockContainer);
        var values = new Dictionary<string, object?> { { "Key1", "Val1" } };

        // Act
        bool result = store.SetValues(values);

        // Assert
        Assert.False(result);
    }
}
