using System.Runtime.InteropServices;
using Windows.System;

namespace BOW.Services;

// WebView2's native child consumes some keys before XAML sees them. This hook is
// restricted to this window's UI thread, never the desktop or another application.
internal sealed class WindowShortcutHook : IDisposable
{
    private readonly HookProc _callback;
    private readonly nint _window;
    private readonly Func<VirtualKey, VirtualKeyModifiers, bool, bool> _dispatch;
    private nint _hook;

    internal WindowShortcutHook(nint window, Func<VirtualKey, VirtualKeyModifiers, bool, bool> dispatch)
    {
        _window = window;
        _dispatch = dispatch;
        _callback = ProcessMessage;
        _hook = SetWindowsHookEx(3, _callback, 0, GetCurrentThreadId());
        if (_hook == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private nint ProcessMessage(int code, nint removed, nint pointer)
    {
        if (code >= 0 && removed == 1)
        {
            var message = Marshal.PtrToStructure<Message>(pointer);
            if (message.Id is 0x100 or 0x104 && (message.Window == _window || IsChild(_window, message.Window)))
            {
                var modifiers = VirtualKeyModifiers.None;
                if (GetKeyState(0x11) < 0) modifiers |= VirtualKeyModifiers.Control;
                if (GetKeyState(0x12) < 0) modifiers |= VirtualKeyModifiers.Menu;
                if (GetKeyState(0x10) < 0) modifiers |= VirtualKeyModifiers.Shift;
                var repeat = ((long)message.Parameter & (1L << 30)) != 0;
                if (_dispatch((VirtualKey)(int)message.Key, modifiers, repeat))
                {
                    message.Id = 0; // WM_NULL prevents double invocation by XAML/Chromium.
                    Marshal.StructureToPtr(message, pointer, false);
                }
            }
        }
        return CallNextHookEx(_hook, code, removed, pointer);
    }

    public void Dispose()
    {
        if (_hook != 0) { UnhookWindowsHookEx(_hook); _hook = 0; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nuint Key;
        public nint Parameter;
        public uint Time;
        public int X, Y;
        public uint Private;
    }
    private delegate nint HookProc(int code, nint removed, nint message);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int type, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint removed, nint message);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint child);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
