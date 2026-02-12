## 2025-05-23 - App Item Accessibility
**Learning:** `AppItem` DataTemplates use a `StackPanel` container that lacks default accessibility properties.
**Action:** Always add `ToolTipService.ToolTip` and `AutomationProperties.Name` to the root container of DataTemplates for list items to ensure truncation is readable and screen readers have context.

## 2025-05-24 - WinUI 3 Window Resources
**Learning:** The `Window` class in WinUI 3 (Windows App SDK) does not expose a `Resources` property in XAML like WPF or UWP `Page`/`UserControl`.
**Action:** Define window-scoped resources within the root layout element (e.g., `<Grid.Resources>`) instead of `<Window.Resources>`.
