## 2024-05-24 - Moving Window Visibility Logic to ViewModels
**Learning:** In WinUI 3, window management methods like `AppWindow.Hide()` often tempt developers to write logic in code-behind. By exposing `Hide()` through `IWindowService`, we can orchestrate window visibility directly from the ViewModel (e.g., hiding after launch), maintaining strict MVVM separation and enabling better unit testing.
**Action:** Always check if UI-specific actions (Close, Hide, Minimize) are driving business logic flow. If so, abstract them into a Service (like `IWindowService`) and inject them into the ViewModel.
## 2024-03-16 - Replacing KeyDown events with KeyboardAccelerators for strict MVVM
**Learning:** Simple key events like `KeyDown="SearchBox_KeyDown"` in WinUI 3 code-behind can be cleanly eliminated by leveraging the `UIElement.KeyboardAccelerators` collection. This allows routing keys directly to `[RelayCommand]` methods in the ViewModel.
**Action:** When asked to migrate simple input event handlers out of XAML code-behind, prefer declarative `KeyboardAccelerators` to invoke commands instead of adding event triggers or keeping logic in `.xaml.cs`.
