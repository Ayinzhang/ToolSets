using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public sealed class Core
{
    public int clickType = 0, gapMs = 100, powerOffMins = 0, hotKey = 0;
    public bool isSettingHotKey = false;
    public string hotKeyName = "None";

    bool isRunning;
    readonly bool[] keyDown = new bool[256];
    TimeSpan started, nextClick;

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
                    if (isDown[key] && !keyDown[key]) { SetHotKey(key); isSettingHotKey = false; }
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
    }

    void ToggleClicking()
    {
        isRunning = !isRunning;
        if (isRunning)
        {
            started = Stopwatch.GetElapsedTime(0);
            nextClick = started;
        }
    }

    void SetHotKey(int virtualKey)
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

    static bool IsKeyboardKey(int key)
    {
        if (key <= 6 || key == 16 || key == 17 || key == 18 || key == 229 || key == 231) return false;
        return MapVirtualKey((uint)key, 0) != 0 || key == 166 || key == 167;
    }

    static readonly uint[] downFlags = [0x0002, 0x0020, 0x0008], upFlags = [0x0004, 0x0040, 0x0010];

    static void Click(int clickType)
    {
        Input[] inputs = [new() { mouse = new() { flags = downFlags[clickType] } }, new() { mouse = new() { flags = upFlags[clickType] } }];
        SendInput(2, inputs, Marshal.SizeOf<Input>());
    }

    static void ShutDown()
    {
        OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out SafeAccessTokenHandle token);
        using (token)
        {
            var privileges = new TokenPrivileges { count = 1 };
            LookupPrivilegeValue(null, "SeShutdownPrivilege", out privileges.luid);
            privileges.attributes = 2;
            AdjustTokenPrivileges(token, false, ref privileges, Marshal.SizeOf<TokenPrivileges>(), out TokenPrivileges previous, out _);
            ExitWindowsEx(0x00000008, 0x00040000 | 0x80000000);
            RestoreTokenPrivileges(token, false, ref previous, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MouseInput
    {
        public int dx, dy;
        public uint data, flags, time;
        public UIntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Input { public uint type; public MouseInput mouse; }

    [StructLayout(LayoutKind.Sequential)]
    struct Luid { public uint low; public int high; }

    [StructLayout(LayoutKind.Sequential)]
    struct TokenPrivileges { public uint count; public Luid luid; public uint attributes; }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetKeyNameText(int keyData, StringBuilder name, int size);
    [DllImport("user32.dll")]
    static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token,
        [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivileges privileges, int size,
        out TokenPrivileges previous, out uint previousSize);
    [DllImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool RestoreTokenPrivileges(SafeAccessTokenHandle token,
        [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivileges privileges, int size,
        IntPtr previous, IntPtr previousSize);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ExitWindowsEx(uint flags, uint reason);
}