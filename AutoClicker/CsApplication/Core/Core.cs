using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public sealed class Core
{
    public int clickType = 0, gapMs = 100, powerOffMins = 0, hotKey = 0;
    public bool isSettingHotKey = false;
    public string hotKeyName = "None";

    private bool isRunning;
    private readonly bool[] keyDown = new bool[256];
    private TimeSpan started, nextClick;

    public void Update()
    {
        Span<bool> isDown = stackalloc bool[256];
        isDown.Clear();
        for (int key = 8; key < 256; ++key)
            isDown[key] = IsKeyboardKey(key) && (GetAsyncKeyState(key) & 0x8000) != 0;

        if (isSettingHotKey)
        {
            if (isDown[27]) isSettingHotKey = false;
            else for (int key = 8; isSettingHotKey && key < 256; ++key)
                if (isDown[key] && !keyDown[key])
                {
                    SetHotKey(key); isSettingHotKey = false;
                }
        }
        else if (hotKey != 0 && isDown[hotKey] && !keyDown[hotKey]) ToggleClicking();

        isDown.CopyTo(keyDown);
        if (!isRunning) return;

        TimeSpan now = Stopwatch.GetElapsedTime(0);
        if (powerOffMins > 0 && now >= started + TimeSpan.FromMinutes(powerOffMins))
        {
            isRunning = false; ShutDown(); return;
        }

        if (now >= nextClick)
        {
            Click(clickType);
            nextClick += TimeSpan.FromMilliseconds(gapMs);
            if (now >= nextClick) nextClick = now + TimeSpan.FromMilliseconds(gapMs);
        }
    }

    public void ToggleHotKeyBinding()
    {
        isSettingHotKey = !isSettingHotKey;
        if (isSettingHotKey) isRunning = false;

        for (int key = 0; key < 256; ++key)
            keyDown[key] = IsKeyboardKey(key) && (GetAsyncKeyState(key) & 0x8000) != 0;
    }

    private void ToggleClicking()
    {
        isRunning = !isRunning;
        if (isRunning)
        {
            started = Stopwatch.GetElapsedTime(0);
            nextClick = started;
        }
    }

    private void SetHotKey(int virtualKey)
    {
        hotKey = virtualKey;
        if (hotKey >= 112 && hotKey <= 135) hotKeyName = $"F{hotKey - 111}";
        else if (hotKey >= 96 && hotKey <= 105) hotKeyName = $"Keypad{hotKey - 96}";
        else if (hotKey == 166) hotKeyName = "BrowserBack";
        else if (hotKey == 167) hotKeyName = "BrowserForward";
        else
        {
            uint scanCode = MapVirtualKey((uint)hotKey, 4);
            int keyData = (int)((scanCode & 0xff) << 16);
            if ((scanCode & 0xff00) == 0xe000) keyData |= 1 << 24;
            var name = new StringBuilder(64);
            hotKeyName = GetKeyNameText(keyData, name, name.Capacity) != 0 ? name.ToString() : $"VK 0x{hotKey:X2}";
        }
    }

    private static bool IsKeyboardKey(int key)
    {
        if (key <= 6 || key == 16 || key == 17 || key == 18 || key == 229 || key == 231) return false;
        return MapVirtualKey((uint)key, 0) != 0 || key == 166 || key == 167;
    }

    private static readonly uint[] downFlags = [0x0002, 0x0020, 0x0008], upFlags = [0x0004, 0x0040, 0x0010];

    private static void Click(int clickType)
    {
        if ((uint)clickType >= downFlags.Length) throw new ArgumentOutOfRangeException(nameof(clickType));
        Input[] inputs = [new() { mouse = new() { flags = downFlags[clickType] } },
            new() { mouse = new() { flags = upFlags[clickType] } }];
        uint sent = SendInput(2, inputs, Marshal.SizeOf<Input>());
        if (sent == 2) return;

        int error = Marshal.GetLastWin32Error();
        // A partial insertion must not leave the mouse button held down.
        if (sent == 1) SendInput(1, [inputs[1]], Marshal.SizeOf<Input>());
        throw new Win32Exception(error, $"SendInput inserted {sent}/2 events (error {error}). A higher-integrity window may block input.");
    }

    private static void ShutDown()
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out SafeAccessTokenHandle token))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcessToken failed.");

        using (token)
        {
            var privileges = new TokenPrivileges { count = 1 };
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out privileges.luid))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "LookupPrivilegeValue failed.");

            privileges.attributes = 2;
            bool adjusted = AdjustTokenPrivileges(token, false, ref privileges, Marshal.SizeOf<TokenPrivileges>(),
                out TokenPrivileges previous, out _);
            int error = Marshal.GetLastWin32Error();
            if (!adjusted || error != 0)
                throw new Win32Exception(error, "Unable to enable SeShutdownPrivilege.");

            bool requested = false, restored;
            int shutdownError = 0, restoreError;
            try
            {
                requested = ExitWindowsEx(0x00000008, 0x00040000 | 0x80000000);
                shutdownError = Marshal.GetLastWin32Error();
            }
            finally
            {
                restored = RestoreTokenPrivileges(token, false, ref previous, 0, IntPtr.Zero, IntPtr.Zero);
                restoreError = Marshal.GetLastWin32Error();
            }
            if (!requested) throw new Win32Exception(shutdownError, "ExitWindowsEx failed.");
            if (!restored || restoreError != 0)
                throw new Win32Exception(restoreError, "Unable to restore shutdown privileges.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx, dy;
        public uint data, flags, time;
        public UIntPtr extraInfo;
    }

    // MOUSEINPUT is the largest INPUT union member on both x86 and x64.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint type; public MouseInput mouse; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid { public uint low; public int high; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges { public uint count; public Luid luid; public uint attributes; }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int keyData, StringBuilder name, int size);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token,
        [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivileges privileges, int size,
        out TokenPrivileges previous, out uint previousSize);
    [DllImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RestoreTokenPrivileges(SafeAccessTokenHandle token,
        [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivileges privileges, int size,
        IntPtr previous, IntPtr previousSize);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ExitWindowsEx(uint flags, uint reason);
}
