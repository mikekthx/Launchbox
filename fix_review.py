with open("SettingsWindow.xaml", "r") as f:
    content = f.read()

# Replace back to original
old_block = """                <!-- Hotkey -->
                <StackPanel Spacing="8">
                    <TextBlock x:Name="HotkeyHeader" x:Uid="Settings_HotkeyHeader" Style="{ThemeResource SubtitleTextBlockStyle}"/>
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <ComboBox x:Uid="Settings_HotkeyModifier" AutomationProperties.LabeledBy="{Binding ElementName=HotkeyHeader}" ItemsSource="{x:Bind ViewModel.Modifiers}" SelectedItem="{x:Bind ViewModel.SelectedModifier, Mode=TwoWay}" Width="100" />
                        <TextBlock Text="+" VerticalAlignment="Center" AutomationProperties.AccessibilityView="Raw"/>
                        <TextBox x:Uid="Settings_HotkeyKey" AutomationProperties.LabeledBy="{Binding ElementName=HotkeyHeader}" Text="{x:Bind ViewModel.HotkeyKeyString, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" Width="100" HorizontalContentAlignment="Center" />
                    </StackPanel>
                </StackPanel>"""

new_block = """                <!-- Hotkey -->
                <StackPanel Spacing="8">
                    <TextBlock x:Uid="Settings_HotkeyHeader" Style="{ThemeResource SubtitleTextBlockStyle}"/>
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <ComboBox x:Uid="Settings_HotkeyModifier" ItemsSource="{x:Bind ViewModel.Modifiers}" SelectedItem="{x:Bind ViewModel.SelectedModifier, Mode=TwoWay}" Width="100" />
                        <TextBlock Text="+" VerticalAlignment="Center" AutomationProperties.AccessibilityView="Raw"/>
                        <TextBox x:Uid="Settings_HotkeyKey" Text="{x:Bind ViewModel.HotkeyKeyString, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" Width="100" HorizontalContentAlignment="Center" />
                    </StackPanel>
                </StackPanel>"""

content = content.replace(old_block, new_block)

with open("SettingsWindow.xaml", "w") as f:
    f.write(content)

with open(".jules/palette.md", "r") as f:
    lines = f.readlines()

with open(".jules/palette.md", "w") as f:
    for line in lines:
        if line.startswith("## 2026-05-18 - Labeling Interactive Inputs Without Explicit Labels"):
            break
        f.write(line)

new_learning = """## 2026-05-18 - LabeledBy vs x:Uid Precedence
**Learning:** In UI Automation, `AutomationProperties.LabeledBy` takes precedence over `AutomationProperties.Name`. When controls already have specific accessible names defined via `x:Uid` in resource files, applying `LabeledBy` to a common header will incorrectly overwrite their specific names with the generic group name, causing a regression.
**Action:** Before applying `AutomationProperties.LabeledBy` to link inputs to a group header, always check if the controls already have dedicated accessible names via `x:Uid` or `AutomationProperties.Name`. Only apply `LabeledBy` if the control genuinely lacks its own accessible name.
"""
with open(".jules/palette.md", "a") as f:
    f.write(new_learning)

print("Reverted XAML and updated learning")
