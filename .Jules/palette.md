## 2025-05-23 - App Item Accessibility
**Learning:** `AppItem` DataTemplates use a `StackPanel` container that lacks default accessibility properties.
**Action:** Always add `ToolTipService.ToolTip` and `AutomationProperties.Name` to the root container of DataTemplates for list items to ensure truncation is readable and screen readers have context.
