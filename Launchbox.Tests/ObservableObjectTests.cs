using System.Collections.Generic;
using Xunit;
using Launchbox.Helpers;

namespace Launchbox.Tests;

public class ObservableObjectTests
{
    private class TestObservableObject : ObservableObject
    {
        private string? _testProperty;
        public string? TestProperty
        {
            get => _testProperty;
            set => SetProperty(ref _testProperty, value);
        }

        public bool SetTestProperty(string? value)
        {
            return SetProperty(ref _testProperty, value, nameof(TestProperty));
        }

        public void RaisePropertyChanged(string? propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public void RaiseCallerMemberName()
        {
            OnPropertyChanged();
        }
    }

    [Fact]
    public void SetProperty_WhenValueChanges_UpdatesBackingFieldAndReturnsTrue()
    {
        // Arrange
        var obj = new TestObservableObject();
        var firedProperties = new List<string?>();
        obj.PropertyChanged += (s, e) => firedProperties.Add(e.PropertyName);

        // Act
        bool result = obj.SetTestProperty("NewValue");

        // Assert
        Assert.True(result);
        Assert.Equal("NewValue", obj.TestProperty);
        Assert.Single(firedProperties);
        Assert.Equal("TestProperty", firedProperties[0]);
    }

    [Fact]
    public void SetProperty_WhenValueIsSame_DoesNotUpdateFieldAndReturnsFalse()
    {
        // Arrange
        var obj = new TestObservableObject { TestProperty = "SameValue" };
        var firedProperties = new List<string?>();
        obj.PropertyChanged += (s, e) => firedProperties.Add(e.PropertyName);

        // Act
        bool result = obj.SetTestProperty("SameValue");

        // Assert
        Assert.False(result);
        Assert.Equal("SameValue", obj.TestProperty);
        Assert.Empty(firedProperties);
    }

    [Fact]
    public void PropertySetter_WhenValueChanges_FiresPropertyChanged()
    {
        // Arrange
        var obj = new TestObservableObject();
        var firedProperties = new List<string?>();
        obj.PropertyChanged += (s, e) => firedProperties.Add(e.PropertyName);

        // Act
        obj.TestProperty = "AnotherValue";

        // Assert
        Assert.Equal("AnotherValue", obj.TestProperty);
        Assert.Single(firedProperties);
        Assert.Equal("TestProperty", firedProperties[0]);
    }

    [Fact]
    public void PropertySetter_WhenValueIsSame_DoesNotFirePropertyChanged()
    {
        // Arrange
        var obj = new TestObservableObject { TestProperty = "InitialValue" };
        var firedProperties = new List<string?>();
        obj.PropertyChanged += (s, e) => firedProperties.Add(e.PropertyName);

        // Act
        obj.TestProperty = "InitialValue";

        // Assert
        Assert.Empty(firedProperties);
    }

    [Fact]
    public void OnPropertyChanged_WhenCalledDirectly_FiresEventWithCorrectName()
    {
        // Arrange
        var obj = new TestObservableObject();
        var firedProperties = new List<string?>();
        obj.PropertyChanged += (s, e) => firedProperties.Add(e.PropertyName);

        // Act
        obj.RaisePropertyChanged("ManualProperty");

        // Assert
        Assert.Single(firedProperties);
        Assert.Equal("ManualProperty", firedProperties[0]);
    }

    [Fact]
    public void OnPropertyChanged_WithoutPropertyName_UsesCallerMemberName()
    {
        // Arrange
        var obj = new TestObservableObject();
        var firedProperties = new List<string?>();
        obj.PropertyChanged += (s, e) => firedProperties.Add(e.PropertyName);

        // Act
        obj.RaiseCallerMemberName();

        // Assert
        Assert.Single(firedProperties);
        Assert.Equal("RaiseCallerMemberName", firedProperties[0]);
    }
}
