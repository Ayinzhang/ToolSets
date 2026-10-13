#include "Application.h"
#include "imgui_impl_glfw.h"
#include "imgui_impl_opengl3.h"

#include <GLFW/glfw3.h>
#include <cstdio>

int Application::Run()
{
    if (!Init()) return 1;

    while (!glfwWindowShouldClose(window))
    {
        glfwPollEvents();

        core.Update();

        int width = 0, height = 0;
        glfwGetFramebufferSize(window, &width, &height);
        if (width == 0 || height == 0) { glfwWaitEventsTimeout(0.1); continue; }

        ImGui_ImplOpenGL3_NewFrame();
        ImGui_ImplGlfw_NewFrame();
        ImGui::NewFrame();

        DrawUI();
        ImGui::Render();

        glViewport(0, 0, width, height);
        glClearColor(0.10f, 0.11f, 0.14f, 1.0f);
        glClear(GL_COLOR_BUFFER_BIT);

        ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());
        glfwSwapBuffers(window);
    }

    return 0;
}

bool Application::Init()
{
    glfwSetErrorCallback([](int code, const char* description) { std::fprintf(stderr, "GLFW error %d: %s\n", code, description); });

    if (!glfwInit()) return false;
    isGLFWInitialized = true;

    glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
    glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
    glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

    window = glfwCreateWindow(360, 64, "AutoClicker", nullptr, nullptr);

    if (!window) return false;

    glfwSetWindowSizeLimits(window, 360, 64, GLFW_DONT_CARE, GLFW_DONT_CARE);

    glfwMakeContextCurrent(window);
    glfwSwapInterval(1);

    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    isImGuiInitialized = true;

    ImGuiIO& io = ImGui::GetIO();
    io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
    io.IniFilename = nullptr;

    if (!ImGui_ImplGlfw_InitForOpenGL(window, true)) return false;
    isPlatformInitialized = true;

    if (!ImGui_ImplOpenGL3_Init("#version 330 core")) return false;
    isRendererInitialized = true;

    return true;
}

void Application::DrawUI()
{
    const ImGuiViewport* viewport = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos(viewport->WorkPos);
    ImGui::SetNextWindowSize(viewport->WorkSize);

    constexpr ImGuiWindowFlags flags = ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoSavedSettings;
    ImGui::Begin("AutoClicker", nullptr, flags);

    constexpr ImGuiTableFlags tableFlags = ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_PadOuterX;

    if (ImGui::BeginTable("layout", 4, tableFlags))
    {
        // Set column spacing
        ImGui::TableSetupColumn("##l1", ImGuiTableColumnFlags_WidthFixed, 50.0f);
        ImGui::TableSetupColumn("##c1", ImGuiTableColumnFlags_WidthFixed, 90.0f);
        ImGui::TableSetupColumn("##l2", ImGuiTableColumnFlags_WidthFixed, 100.0f);
        ImGui::TableSetupColumn("##c2", ImGuiTableColumnFlags_WidthFixed, 90.0f);

        // Row 1
        ImGui::TableNextRow();

        ImGui::TableSetColumnIndex(0);
        ImGui::TextUnformatted("Click");

        ImGui::TableSetColumnIndex(1);
        ImGui::SetNextItemWidth(90);
        ImGui::Combo("##click", &core.clickType, "Left\0Middle\0Right\0");
        ImGui::SetItemTooltip("Simu the L/M/R mouse click.");

        ImGui::TableSetColumnIndex(2);
        ImGui::TextUnformatted("Gap(ms)");

        ImGui::TableSetColumnIndex(3);
        ImGui::SetNextItemWidth(90);
        ImGui::InputInt("##gap", &core.gapMs, 0, 0);

        // Row 2
        ImGui::TableNextRow();

        ImGui::TableSetColumnIndex(0);
        ImGui::TextUnformatted("Hotkey");

        ImGui::TableSetColumnIndex(1);
        if (ImGui::Button(core.isSettingHotKey ? "Press a key" : core.hotKeyName, ImVec2(90, 0))) core.ToggleHotKeyBinding();
        ImGui::SetItemTooltip("Click to bind a key. Esc cancels. Works in the background.\nEnter and KeypadEnter share the same hotkey.");

        ImGui::TableSetColumnIndex(2);
        ImGui::TextUnformatted("PowerOff(mins)");

        ImGui::TableSetColumnIndex(3);
        ImGui::SetNextItemWidth(90);
        ImGui::InputInt("##poweroff", &core.powerOffMins, 0, 0);

        ImGui::EndTable();
    }

    ImGui::End();
}

Application::~Application() noexcept
{
    if (window) glfwMakeContextCurrent(window);
    if (isRendererInitialized) ImGui_ImplOpenGL3_Shutdown();
    if (isPlatformInitialized) ImGui_ImplGlfw_Shutdown();
    if (isImGuiInitialized) ImGui::DestroyContext();
    if (window) glfwDestroyWindow(window);
    if (isGLFWInitialized) glfwTerminate();
}