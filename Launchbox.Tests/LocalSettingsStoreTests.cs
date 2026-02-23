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
    public void SetValue_HandlesException_DoesNotThrow()
    {
        // Arrange
        var mockContainer = new MockSettingsContainer { ThrowOnWrite = true };
        var store = new LocalSettingsStore(mockContainer);

        // Act
        var exception = Record.Exception(() => store.SetValue("TestKey", "TestValue"));

        // Assert
        Assert.Null(exception);
    }
}
