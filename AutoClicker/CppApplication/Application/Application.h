#pragma once

struct GLFWwindow;

class Application final
{
public:
    Application() = default;
    ~Application() noexcept;

    Application(const Application&) = delete;
    Application& operator=(const Application&) = delete;

    int Run();

private:
    bool Init();
    void DrawUI();

    GLFWwindow* window = nullptr;
    bool isGLFWInitialized = false, isImGuiInitialized = false, isPlatformInitialized = false, isRendererInitialized = false;
    int clickType = 0, gapMs = 100, hotKey = 0, shutDownMins = 0; bool isSettingHotKey = false;
};