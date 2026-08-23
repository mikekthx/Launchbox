1. Address PR Review
   - Reviewer notes: `when Activate() throws on the existing-window fast path, the catch block logs and returns but leaves _settingsWindow pointing at the broken instance. Since SettingsWindow_Closed (which nulls the field) never fires in that path, every future OpenSettings() call repeats the same failure — the user is silently locked out of Settings until the app restarts. Suggest nulling _settingsWindow in the catch to allow recovery.`
   - Need to modify `Services/WindowService.cs`.
   - Update `OpenSettings()` catch block to null out `_settingsWindow`. Wait, the catch block handles BOTH the creation and the fast path. If creation fails, `_settingsWindow` could be null anyway, so setting it to null is fine. If `Activate()` throws on the fast path, we null it out, which allows the next call to try recreating it.

2. Implement Fix
   - Use Python script to modify `Services/WindowService.cs`.
   - `old_code`:
```csharp
        catch (Exception ex)
        {
            Trace.WriteLine($"Error opening settings: {PathSecurity.GetSafeExceptionMessage(ex)}");
        }
```
   - `new_code`:
```csharp
        catch (Exception ex)
        {
            Trace.WriteLine($"Error opening settings: {PathSecurity.GetSafeExceptionMessage(ex)}");
            _settingsWindow = null;
        }
```

3. Verify changes
   - Run `dotnet build` and `dotnet test`.

4. Submit the change
   - Update branch, commit, and push. Then reply to PR comments and submit.
