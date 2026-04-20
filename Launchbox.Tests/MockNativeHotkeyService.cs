using Launchbox.Services;
using System;
using System.Collections.Generic;

namespace Launchbox.Tests;

public class MockNativeHotkeyService : INativeHotkeyService
{
    public record RegisterCall(IntPtr HWnd, int Id, uint Modifiers, uint VirtualKey);
    public record UnregisterCall(IntPtr HWnd, int Id);

    public Queue<bool> RegisterResults { get; } = new();
    public List<RegisterCall> RegisterCalls { get; } = [];
    public List<UnregisterCall> UnregisterCalls { get; } = [];

    public bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey)
    {
        RegisterCalls.Add(new RegisterCall(hWnd, id, modifiers, virtualKey));
        return RegisterResults.Count > 0 ? RegisterResults.Dequeue() : true;
    }

    public bool UnregisterHotKey(IntPtr hWnd, int id)
    {
        UnregisterCalls.Add(new UnregisterCall(hWnd, id));
        return true;
    }
}
