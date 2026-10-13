#pragma once
#include <chrono>

class Core final
{
public:
    Core() = default;
    ~Core() noexcept = default;

    Core(const Core&) = delete;
    Core& operator=(const Core&) = delete;

    void Update();
    void ToggleHotKeyBinding();

    int clickType = 0, gapMs = 100, powerOffMins = 0, hotKey = 0;
    bool isSettingHotKey = false; char hotKeyName[64] = "None";

private:
    void SetHotKey(int virtualKey);
    void ToggleClicking();

    bool isRunning = false, keyDown[256] = {};
    std::chrono::steady_clock::time_point started, nextClick;
};