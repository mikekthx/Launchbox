# Launchbox Agent Routing Configuration

This document instructs Jules on which agents to trigger based on the files modified in a Pull Request.

## Routing Rules

- path: "Launchbox/Views/**/*.xaml"
  agents: ["Palette", "Bolt"]
  context: "WinUI XAML files. Palette should focus on AutomationProperties (accessibility) and VisualStateManager. Bolt should look for x:Bind optimizations and efficient UI rendering."

- path: "Launchbox/Views/**/*.xaml.cs"
  agents: ["Mason"]
  context: "UI Code-behind. Mason must strictly ensure no business logic or state management is trapped here, pushing it toward the ViewModels."

- path: "Launchbox/ViewModels/**/*.cs"
  agents: ["Mason", "Scribe"]
  context: "MVVM ViewModels. Focus on strict ICommand usage, INotifyPropertyChanged implementations, and clear XML documentation for bindings."

- path: "Launchbox/Services/**/*.cs"
  agents: ["Inspector", "Sentinel"]
  context: "Core background services handling the system tray, global hotkeys, and native Windows APIs. High risk for memory leaks. Inspector must enforce strict IDisposable patterns and event unsubscription."

- path: "Launchbox/Models/**/*.cs"
  agents: ["Scribe", "Bolt"]
  context: "App data structures. Focus on clear property naming, readable LINQ, and memory-efficient data types."

- path: "Launchbox/App.xaml.cs"
  agents: ["Inspector"]
  context: "Application lifecycle. Watch for unhandled exception risks during startup or shutdown."