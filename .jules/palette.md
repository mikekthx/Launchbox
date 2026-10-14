## 2025-02-28 - Link Interactive Elements to Text Blocks for Accessibility
**Learning:** In WinUI 3 XAML settings or forms, interactive controls (like ComboBox or TextBox) should be explicitly linked to their visual headers to properly support screen readers.
**Action:** Assign an `x:Name` to the descriptive `TextBlock` and set `AutomationProperties.LabeledBy="{Binding ElementName=HeaderName}"` on the interactive control to provide necessary context to screen readers.
