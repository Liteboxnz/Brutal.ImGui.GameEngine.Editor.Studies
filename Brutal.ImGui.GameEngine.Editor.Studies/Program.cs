using Brutal;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.ImGuiApi.FreeType;
using Brutal.ImPlotApi;
using Brutal.Numerics;
using Brutal.OpenGlApi;

if (!Glfw.Init())
{
    var errorCode = Glfw.GetError(out var errorMessage);
    throw new InvalidOperationException($"Failed to initialize GLFW: {errorCode}, {errorMessage}");
}

var context = ImGui.CreateContext();
var plotContext = ImPlot.CreateContext();
ImPlot.SetImGuiContext(context);

try
{
    Run(args);
}
finally
{
    ImPlot.DestroyContext(plotContext);
    ImGui.DestroyContext(context);
    Glfw.Terminate();
}

static void Run(string[] args)
{
    var launcher = new StudySelectionScreen();

    Glfw.WindowHint(GlfwWindowHint.ContextVersionMajor, 4);
    Glfw.WindowHint(GlfwWindowHint.ContextVersionMinor, 1);
    Glfw.WindowHint(GlfwWindowHint.OpenGlProfile, Glfw.Constants.GLFW_OPENGL_CORE_PROFILE);

    if (Platform.IsMacOS)
        Glfw.WindowHint(GlfwWindowHint.OpenGlForwardCompat, 1);

    // Declared first so it's disposed last: the backends below need the window and its GL context.
    using var window = Glfw.CreateWindow(new GlfwWindow.CreateInfo
    {
        Title = launcher.WindowTitle,
        Size = new int2(1600, 900),
    });

    window.MakeContextCurrent();
    Glfw.SwapInterval(1); // Wait for vsync instead of drawing as fast as possible.

    unsafe
    {
        if (!Glad.Initialize(Glfw.PInvoke.GetProcAddress))
            throw new InvalidOperationException("Failed to initialize OpenGL");
    }

    window.OnKey += (handle, key, _, _, _) =>
    {
        if (key == GlfwKey.Escape)
            handle.ShouldClose = true;
    };

    var io = ImGui.GetIO();
    io.IniFilename = null; // Don't write imgui.ini.
    io.ConfigFlags |= ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.NavEnableKeyboard;

    // Rasterize fonts with FreeType instead of the built-in stb_truetype.
    var fonts = io.Fonts;
    fonts.SetFontLoader(ImGuiFreeType.GetFontLoader());
    if (args.Length > 0)
        fonts.AddFontFromFileTTF(args[0], 24f); // Optional: dotnet run -- path/to/font.ttf.

    using var glfwBackend = new GlfwBackend(window, GlfwBackend.ClientApi.OpenGL);
    using var openGlBackend = new OpenGlBackend();

    launcher.Setup();
    IEngineEditorStudy? study = null;

    OpenGl.ClearColor(float4.Rgb(0.1f, 0.1f, 0.12f));

    while (!window.ShouldClose)
    {
        Glfw.PollEvents();

        glfwBackend.NewFrame();
        openGlBackend.NewFrame();
        ImGui.NewFrame();
        // Recycle the scratch memory that interpolated ImGui strings ($"...") use, once per frame.
        ImString.SharedStorage.Reset();

        if (study is null)
        {
            launcher.Draw(
                subject =>
                {
                    study = EngineEditorStudyFactory.Create(subject);
                    window.Title = study.WindowTitle;
                    study.Setup();
                },
                () => window.ShouldClose = true);
        }
        else
        {
            study.Draw(
                () => window.ShouldClose = true,
                () =>
                {
                    study = null;
                    window.Title = launcher.WindowTitle;
                    launcher.Setup();
                });
        }

        ImGui.Render();

        OpenGl.Clear(GlClearBit.Color);
        openGlBackend.RenderDrawData(ImGui.GetDrawData());
        window.SwapBuffers();
    }
}
