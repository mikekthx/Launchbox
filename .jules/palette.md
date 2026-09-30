## 2024-05-24 - Add LabeledBy to Hotkey Inputs
**Learning:** When creating grouped settings or forms in WinUI 3 XAML, descriptive text headers that conceptually group interactive inputs (like a ComboBox and TextBox for a hotkey) do not automatically provide context to screen readers focusing on those inputs.
**Action:** Always give the conceptual grouping header an `x:Name` and use `AutomationProperties.LabeledBy="{Binding ElementName=HeaderName}"` on the child interactive controls so screen readers announce the group context when the control receives focus.
