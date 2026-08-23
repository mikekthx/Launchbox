1. **Convert `{Binding}` to `{x:Bind}` in `MainWindow.xaml`**
   - Replace the remaining reflection-based `{Binding}` usages with compiled `{x:Bind}` where possible for better performance and compile-time validation.
   - For `AppItemTemplate` in `MainWindow.xaml`, change bindings on the `StackPanel` like `Visibility="{Binding Name, Converter={StaticResource EmptyStringToCollapsedConverter}}"` to `Visibility="{x:Bind Name, Mode=OneWay, Converter={StaticResource EmptyStringToCollapsedConverter}}"`.
   - Update `GroupStyle.HeaderTemplate` bindings in `MainWindow.xaml` for `AppItemGroup` (e.g. `AutomationProperties.ItemStatus`, `Glyph`) to use `{x:Bind IsCollapsed, Mode=OneWay, ...}`.
   - We will not change `ElementName` bindings in `DataTemplate` as they are cross-scope and x:Bind does not support this.

2. **Verify Changes**
   - Run `dotnet build` to ensure the XAML compiles correctly with the new `{x:Bind}` usages.
   - Run `dotnet test` to ensure tests still pass.

3. **Complete pre-commit steps to ensure proper testing, verification, review, and reflection are done.**
   - Run pre_commit_instructions tool and follow it.
   - Note the learning in the `.jules/bolt.md` file.

4. **Submit PR**
   - Create a PR using the `submit` tool with a descriptive title and body following Bolt's format.
