import os

def replace_in_file(filepath, old_code, new_code):
    with open(filepath, 'r') as f:
        content = f.read()

    if old_code not in content:
        print(f"Failed to find target code in {filepath}")
        return False

    content = content.replace(old_code, new_code)

    with open(filepath, 'w') as f:
        f.write(content)

    print(f"Successfully updated {filepath}")
    return True

mainwindow_old = """                            ToolTipService.ToolTip="{x:Bind Name}"
                            AutomationProperties.Name="{x:Bind Name}"
                            Visibility="{Binding Name, Converter={StaticResource EmptyStringToCollapsedConverter}}">"""

mainwindow_new = """                            ToolTipService.ToolTip="{x:Bind Name}"
                            AutomationProperties.Name="{x:Bind Name}"
                            Visibility="{x:Bind Name, Converter={StaticResource EmptyStringToCollapsedConverter}, Mode=OneWay}">"""

replace_in_file("MainWindow.xaml", mainwindow_old, mainwindow_new)
