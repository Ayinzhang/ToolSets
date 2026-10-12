#pragma once
#include "Core.h"

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
    Core core; GLFWwindow* window = nullptr;
    bool isGLFWInitialized = false, isImGuiInitialized = false, isPlatformInitialized = false, isRendererInitialized = false;

    bool Init();
    void DrawUI();
};