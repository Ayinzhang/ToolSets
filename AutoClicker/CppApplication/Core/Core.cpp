#include "Core.h"

#include <Windows.h>
#include <cstdio>

#pragma comment(lib, "user32.lib")
#pragma comment(lib, "advapi32.lib")

static void Click(int clickType)
{
    constexpr DWORD downFlags[] = { MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_RIGHTDOWN };
    constexpr DWORD upFlags[] = { MOUSEEVENTF_LEFTUP, MOUSEEVENTF_MIDDLEUP, MOUSEEVENTF_RIGHTUP };

    INPUT inputs[2] = {};
    inputs[0].type = inputs[1].type = INPUT_MOUSE;
    inputs[0].mi.dwFlags = downFlags[clickType]; inputs[1].mi.dwFlags = upFlags[clickType];

    SendInput(2, inputs, sizeof(INPUT));
}

static void ShutDown()
{
    HANDLE token = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &token)) return;

    TOKEN_PRIVILEGES privileges = {}, previous = {}; privileges.PrivilegeCount = 1;
    if (!LookupPrivilegeValueA(nullptr, "SeShutdownPrivilege", &privileges.Privileges[0].Luid)) { CloseHandle(token); return; }

    privileges.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
    DWORD previousSize = sizeof(previous); SetLastError(ERROR_SUCCESS);
    BOOL adjusted = AdjustTokenPrivileges(token, FALSE, &privileges, sizeof(previous), &previous, &previousSize);
    if (!adjusted || GetLastError() != ERROR_SUCCESS) { CloseHandle(token); return; }
    
    ExitWindowsEx(EWX_POWEROFF, SHTDN_REASON_MAJOR_APPLICATION | SHTDN_REASON_FLAG_PLANNED);
    AdjustTokenPrivileges(token, FALSE, &previous, 0, nullptr, nullptr); CloseHandle(token);
}

static bool IsKeyboardKey(int key)
{
    if (key <= VK_XBUTTON2 || key == VK_SHIFT || key == VK_CONTROL || key == VK_MENU) return false;

    if (key == VK_PROCESSKEY || key == VK_PACKET) return false;

    return MapVirtualKeyA(key, MAPVK_VK_TO_VSC) != 0 || key == VK_BROWSER_BACK || key == VK_BROWSER_FORWARD;
}

void Core::Update()
{
    // Handle hotkey binding and clicking
    bool isDown[256] = {};
    for (int key = VK_BACK; key < 256; ++key)
        isDown[key] = IsKeyboardKey(key) && (GetAsyncKeyState(key) & 0x8000) != 0;

    if (isSettingHotKey)
    {
        if (isDown[VK_ESCAPE]) isSettingHotKey = false;
        else for (int key = VK_BACK; isSettingHotKey && key < 256; ++key)
            if (isDown[key] && !keyDown[key]) SetHotKey(key), isSettingHotKey = false;
    }
    else if (hotKey && isDown[hotKey] && !keyDown[hotKey]) ToggleClicking();

    for (int key = 0; key < 256; ++key) keyDown[key] = isDown[key];

    if (!isRunning) return;

    // Handle clicking and shutdown
    auto now = std::chrono::steady_clock::now();

    if (shutDownMins > 0 && now >= started + std::chrono::minutes(shutDownMins)) { isRunning = false; ShutDown(); return; }

    if (now >= nextClick) 
    { 
        Click(clickType); nextClick += std::chrono::milliseconds(gapMs); 
        if (now >= nextClick) nextClick = now + std::chrono::milliseconds(gapMs); // Reset if too far behind
    }
}

void Core::ToggleHotKeyBinding()
{
    isSettingHotKey = !isSettingHotKey;
    if (isSettingHotKey) isRunning = false;

    for (int key = 0; key < 256; ++key)
        keyDown[key] = IsKeyboardKey(key) && (GetAsyncKeyState(key) & 0x8000) != 0;
}

void Core::ToggleClicking()
{
    isRunning = !isRunning;

    if (isRunning)
    {
        started = std::chrono::steady_clock::now();
        nextClick = started;
    }
}

void Core::SetHotKey(int virtualKey)
{
    hotKey = virtualKey;

    if (hotKey >= VK_F1 && hotKey <= VK_F24)
        std::snprintf(hotKeyName, sizeof(hotKeyName), "F%d", hotKey - VK_F1 + 1);
    else if (hotKey >= VK_NUMPAD0 && hotKey <= VK_NUMPAD9)
        std::snprintf(hotKeyName, sizeof(hotKeyName), "Keypad%d", hotKey - VK_NUMPAD0);
    else if (hotKey == VK_BROWSER_BACK)
        std::snprintf(hotKeyName, sizeof(hotKeyName), "BrowserBack");
    else if (hotKey == VK_BROWSER_FORWARD)
        std::snprintf(hotKeyName, sizeof(hotKeyName), "BrowserForward");
    else
    {
        UINT scanCode = MapVirtualKeyA(hotKey, MAPVK_VK_TO_VSC_EX);
        LONG keyData = static_cast<LONG>((scanCode & 0xff) << 16);
        if ((scanCode & 0xff00) == 0xe000) keyData |= 1 << 24;

        if (!GetKeyNameTextA(keyData, hotKeyName, sizeof(hotKeyName)))
            std::snprintf(hotKeyName, sizeof(hotKeyName), "VK 0x%02X", hotKey);
    }
}