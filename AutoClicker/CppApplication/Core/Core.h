#pragma once

#include <chrono>
#include <condition_variable>
#include <mutex>
#include <thread>

class Core final
{
public:
    Core() = default;
    ~Core() noexcept;

    Core(const Core&) = delete;
    Core& operator=(const Core&) = delete;

    void Update();
    void ToggleHotKeyBinding();

    int clickType = 0, gapMs = 100, shutDownMins = 0, hotKey = 0;
    bool isSettingHotKey = false; char hotKeyName[64] = "None";

private:
    void SetHotKey(int virtualKey);
    void LogHotKey(const char* action) const;
    void ToggleClicking();
    void Run();

    bool keyDown[256] = {};

    std::mutex mutex;
    std::condition_variable changed;
    std::thread worker;
    bool isRunning = false, isExiting = false;
    int activeClickType = 0, activeGapMs = 100, activeShutDownMins = 0;
    unsigned long long revision = 0;
    std::chrono::steady_clock::time_point started;
};