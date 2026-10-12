#include "Core.h"

#include <Windows.h>
#include <cstdio>
#include <system_error>

#pragma comment(lib, "user32.lib")
#pragma comment(lib, "advapi32.lib")

static void LogError(const char* operation, unsigned long error)
{
    char message[256] = {};
    std::snprintf(message, sizeof(message), "%s failed (error %lu).\n", operation, error);
    std::fputs(message, stderr);
    std::fflush(stderr);
    OutputDebugStringA(message);
}

static bool ClickMouse(int clickType)
{
    constexpr DWORD downFlags[] = {
        MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_RIGHTDOWN
    };
    constexpr DWORD upFlags[] = {
        MOUSEEVENTF_LEFTUP, MOUSEEVENTF_MIDDLEUP, MOUSEEVENTF_RIGHTUP
    };

    INPUT inputs[2] = {};
    inputs[0].type = inputs[1].type = INPUT_MOUSE;
    inputs[0].mi.dwFlags = downFlags[clickType];
    inputs[1].mi.dwFlags = upFlags[clickType];

    SetLastError(ERROR_SUCCESS);
    UINT sent = SendInput(2, inputs, sizeof(INPUT));
    if (sent == 2) return true;

    LogError("SendInput", GetLastError());
    if (sent == 1 && SendInput(1, &inputs[1], sizeof(INPUT)) != 1)
        LogError("Releasing mouse button", GetLastError());

    return false;
}

static bool ShutDownPC()
{
    HANDLE token = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &token))
    {
        LogError("OpenProcessToken", GetLastError());
        return false;
    }

    TOKEN_PRIVILEGES privileges = {}, previous = {};
    privileges.PrivilegeCount = 1;

    if (!LookupPrivilegeValueA(nullptr, "SeShutdownPrivilege", &privileges.Privileges[0].Luid))
    {
        LogError("LookupPrivilegeValue", GetLastError());
        CloseHandle(token);
        return false;
    }

    privileges.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
    DWORD previousSize = sizeof(previous);

    SetLastError(ERROR_SUCCESS);
    BOOL adjusted = AdjustTokenPrivileges(
        token, FALSE, &privileges, sizeof(previous), &previous, &previousSize);
    DWORD error = GetLastError();

    if (!adjusted || error != ERROR_SUCCESS)
    {
        LogError("Enabling shutdown privilege", error);
        CloseHandle(token);
        return false;
    }

    BOOL requested = ExitWindowsEx(
        EWX_POWEROFF, SHTDN_REASON_MAJOR_APPLICATION | SHTDN_REASON_FLAG_PLANNED);
    if (!requested) LogError("ExitWindowsEx", GetLastError());

    SetLastError(ERROR_SUCCESS);
    adjusted = AdjustTokenPrivileges(token, FALSE, &previous, 0, nullptr, nullptr);
    error = GetLastError();

    if (!adjusted || error != ERROR_SUCCESS)
        LogError("Restoring shutdown privilege", error);

    CloseHandle(token);
    return requested != FALSE;
}

static bool IsKeyboardKey(int key)
{
    if (key <= VK_XBUTTON2 || key == VK_SHIFT || key == VK_CONTROL || key == VK_MENU)
        return false;

    if (key == VK_PROCESSKEY || key == VK_PACKET) return false;

    return MapVirtualKeyA(key, MAPVK_VK_TO_VSC) != 0 ||
        key == VK_BROWSER_BACK || key == VK_BROWSER_FORWARD;
}

Core::~Core() noexcept
{
    {
        std::lock_guard<std::mutex> lock(mutex);
        isRunning = false;
        isExiting = true;
    }

    changed.notify_one();
    if (worker.joinable()) worker.join();
}

void Core::Update()
{
    if (clickType < 0 || clickType > 2)
    {
        LogError("Invalid mouse button", ERROR_INVALID_PARAMETER);
        clickType = 0;
    }

    if (gapMs < 1) gapMs = 1;
    if (shutDownMins < 0) shutDownMins = 0;

    {
        std::lock_guard<std::mutex> lock(mutex);

        if (activeClickType != clickType ||
            activeGapMs != gapMs ||
            activeShutDownMins != shutDownMins)
        {
            activeClickType = clickType;
            activeGapMs = gapMs;
            activeShutDownMins = shutDownMins;
            ++revision;
            changed.notify_one();
        }
    }

    bool isDown[256] = {};
    for (int key = VK_BACK; key < 256; ++key)
        if (IsKeyboardKey(key))
            isDown[key] = (GetAsyncKeyState(key) & 0x8000) != 0;

    if (isSettingHotKey)
    {
        if (isDown[VK_ESCAPE] && !keyDown[VK_ESCAPE])
            isSettingHotKey = false;
        else for (int key = VK_BACK; key < 256; ++key)
            if (isDown[key] && !keyDown[key])
            {
                SetHotKey(key);
                isSettingHotKey = false;
                LogHotKey("bound");
                break;
            }
    }
    else if (hotKey != 0 && isDown[hotKey] && !keyDown[hotKey])
        ToggleClicking();

    for (int key = 0; key < 256; ++key) keyDown[key] = isDown[key];
}

void Core::ToggleHotKeyBinding()
{
    isSettingHotKey = !isSettingHotKey;

    if (isSettingHotKey)
    {
        std::lock_guard<std::mutex> lock(mutex);
        isRunning = false;
        ++revision;
        changed.notify_one();
    }

    for (int key = 0; key < 256; ++key)
        keyDown[key] = IsKeyboardKey(key) &&
        (GetAsyncKeyState(key) & 0x8000) != 0;
}

void Core::ToggleClicking()
{
    std::lock_guard<std::mutex> lock(mutex);

    if (!worker.joinable())
    {
        try { worker = std::thread(&Core::Run, this); }
        catch (const std::system_error& error)
        {
            std::fprintf(stderr, "Unable to start clicking thread: %s\n", error.what());
            OutputDebugStringA(error.what());
            return;
        }
    }

    isRunning = !isRunning;
    if (isRunning) started = std::chrono::steady_clock::now();

    ++revision;
    LogHotKey(isRunning ? "started" : "stopped");
    changed.notify_one();
}

void Core::Run()
{
    using Clock = std::chrono::steady_clock;
    std::unique_lock<std::mutex> lock(mutex);

    while (!isExiting)
    {
        changed.wait(lock, [this] { return isRunning || isExiting; });
        if (isExiting) break;

        auto nextClick = Clock::now();
        auto currentRevision = revision;

        while (isRunning && !isExiting)
        {
            if (currentRevision != revision)
            {
                currentRevision = revision;
                nextClick = Clock::now();
            }

            auto now = Clock::now();
            auto shutdownAt = (Clock::time_point::max)();
            auto maxMinutes = std::chrono::duration_cast<std::chrono::minutes>(
                shutdownAt - started).count();

            if (activeShutDownMins > 0 && activeShutDownMins <= maxMinutes)
                shutdownAt = started + std::chrono::minutes(activeShutDownMins);

            if (now >= shutdownAt)
            {
                isRunning = false;

                if (ShutDownPC())
                {
                    std::fputs("PC shutdown requested.\n", stdout);
                    std::fflush(stdout);
                    OutputDebugStringA("PC shutdown requested.\n");
                }

                break;
            }

            if (now >= nextClick)
            {
                if (!ClickMouse(activeClickType))
                {
                    isRunning = false;
                    break;
                }

                nextClick = Clock::now() + std::chrono::milliseconds(activeGapMs);
            }

            auto wakeAt = nextClick < shutdownAt ? nextClick : shutdownAt;
            changed.wait_until(lock, wakeAt, [this, currentRevision] {
                return isExiting || !isRunning || revision != currentRevision;
                });
        }
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
        {
            LogError("GetKeyNameText", GetLastError());
            std::snprintf(hotKeyName, sizeof(hotKeyName), "VK 0x%02X", hotKey);
        }
    }
}

void Core::LogHotKey(const char* action) const
{
    char message[128] = {};
    std::snprintf(message, sizeof(message), "Hotkey %s: %s (VK=0x%02X)\n",
        action, hotKeyName, hotKey);
    std::fputs(message, stdout);
    std::fflush(stdout);
    OutputDebugStringA(message);
}