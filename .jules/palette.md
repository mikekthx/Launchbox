## 2025-05-23 - App Item Accessibility
**Learning:** `AppItem` DataTemplates use a `StackPanel` container that lacks default accessibility properties.
**Action:** Always add `ToolTipService.ToolTip` and `AutomationProperties.Name` to the root container of DataTemplates for list items to ensure truncation is readable and screen readers have context.

## 2025-05-24 - WinUI 3 Window Resources
**Learning:** The `Window` class in WinUI 3 (Windows App SDK) does not expose a `Resources` property in XAML like WPF or UWP `Page`/`UserControl`.
**Action:** Define window-scoped resources within the root layout element (e.g., `<Grid.Resources>`) instead of `<Window.Resources>`.
## 2026-03-13 - Preserve Tooltip Shortcuts
**Learning:** Keyboard shortcut hints in tooltips (e.g., `(Alt+S)`) are standard Windows accessibility patterns that aid discoverability and should not be removed during routine UX enhancements.
**Action:** Preserve existing keyboard shortcut hints in XAML tooltips, even if they refer to dynamically configurable hotkeys, unless specifically tasked with implementing a dynamic tooltip binding.

## 2026-03-27 - Grid Toggle Button Accessibility
**Learning:** In custom WinUI layouts, using a `Grid` with a tapped command (e.g., for a collapsible group header) makes it function as a button but entirely lacks accessibility semantics by default. Furthermore, without a `Background` explicitly set (e.g., `Background="Transparent"`), empty space in the Grid does not receive hit test events, making the interaction area unpredictably small.
**Action:** Always ensure interactive layout panels (like Grids or StackPanels) have `Background="Transparent"` for proper hit-testing, and explicitly define `AutomationProperties.Name` and an appropriate `ToolTipService.ToolTip` so screen readers and mouse users understand the control's purpose. Note that `AutomationProperties.Role` is an HTML/ARIA concept and does not exist in WinUI 3; avoid using it to prevent XAML compilation errors.
## 2026-05-01 - Screen Reader Noise from Decorative Icons
**Learning:** Screen readers announce `SymbolIcon` and `FontIcon` elements by default, causing redundant reading when next to a text label, or reading unintelligible unicode characters in icon-only buttons.
**Action:** Always add `AutomationProperties.AccessibilityView="Raw"` to purely decorative `SymbolIcon` and `FontIcon` elements to remove them from the UI Automation tree and reduce screen reader noise.
## 2026-05-06 - Hit-Testing Empty Space in WinUI
**Learning:** In WinUI/UWP/WPF, interactive layout panels (such as `StackPanel` or `Grid`) with a null background do not register pointer events in their empty/transparent spaces.
**Action:** Explicitly set `Background="Transparent"` to ensure the entire bounds of the element are hit-testable, especially for drag-and-drop or click targets like an `AppItemTemplate`.
## 2026-05-15 - ProgressRing Visibility Binding
**Learning:** In WinUI 3, a `ProgressRing` automatically hides its visuals when `IsActive="False"`. It is not necessary to explicitly bind its `Visibility` property using a boolean-to-visibility converter unless the layout space it reserves needs to be reclaimed.
**Action:** When adding simple visual feedback using a `ProgressRing` alongside a button (e.g., in a `StackPanel`), simply bind the `IsActive` property to the async command's execution state without a redundant `Visibility` binding.
## 2026-06-03 - List Item Accessibility Context
**Learning:** Adding accessibility and tooltip properties to inner child elements of a `ListViewItem` DataTemplate forces users to hover/focus precisely on those elements. If properties are missing on the root, screen readers may read the raw text without semantic context, or worse, miss it if overridden improperly.
**Action:** Always apply `AutomationProperties.Name` and `ToolTipService.ToolTip` to the root container (e.g., `<Grid>`) of a `DataTemplate` in list controls. This ensures full row context is provided upon focus and tooltips trigger seamlessly across the entire hit target.
