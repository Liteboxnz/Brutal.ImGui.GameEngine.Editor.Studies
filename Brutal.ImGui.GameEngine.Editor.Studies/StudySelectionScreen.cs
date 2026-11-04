using Brutal.ImGuiApi;
using Brutal.Numerics;

/// <summary>
/// First screen after launch. Pick a study from the factory catalog; the host
/// then constructs that editor and runs it until you return here.
/// </summary>
sealed class StudySelectionScreen
{
    public string WindowTitle => "Game Engine Editor Studies";

    public void Setup() => ApplyLauncherTheme();

    public void Draw(Action<StudySubjects> startStudy, Action requestClose)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.SetNextWindowViewport(viewport.ID);

        const ImGuiWindowFlags hostFlags =
            ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNavFocus;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.Begin("Study Launcher Host"u8, hostFlags);
        ImGui.PopStyleVar(2);

        var catalog = EngineEditorStudyFactory.Catalog;
        var cardSize = new float2(360f, 188f);
        var gap = 20f;
        var cardsWidth = catalog.Count * cardSize.X + Math.Max(0, catalog.Count - 1) * gap;
        var origin = (ImGui.GetContentRegionAvail() - new float2(cardsWidth, cardSize.Y + 88f)) * 0.5f;
        origin.X = MathF.Max(24f, origin.X);
        origin.Y = MathF.Max(24f, origin.Y);
        ImGui.SetCursorPos(origin);

        ImGui.BeginGroup();
        ImGui.Text("Game Engine Editor Studies"u8);
        ImGui.TextDisabled("Choose a stubbed editor layout to study Dear ImGui docking, widgets, and gizmos."u8);
        ImGui.Dummy(new float2(0f, 16f));

        for (var i = 0; i < catalog.Count; i++)
        {
            if (i > 0)
                ImGui.SameLine(0f, gap);
            DrawStudyCard(catalog[i], cardSize, startStudy);
        }

        ImGui.Dummy(new float2(0f, 20f));
        if (ImGui.Button("Quit"u8, new float2(120f, 0f)))
            requestClose();
        ImGui.EndGroup();

        ImGui.End();
    }

    void DrawStudyCard(StudyCatalogEntry entry, float2 size, Action<StudySubjects> startStudy)
    {
        ImGui.PushID((int)entry.Subject);
        ImGui.BeginGroup();
        ImGui.BeginChild($"card{entry.Subject}", size, ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);

        ImGui.Dummy(new float2(0f, 8f));
        ImGui.Text(entry.Title);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + size.X - 24f);
        ImGui.TextDisabled(entry.Summary);
        ImGui.PopTextWrapPos();

        ImGui.Dummy(new float2(0f, 12f));
        if (ImGui.Button("Launch"u8, new float2(-1f, 36f)))
            startStudy(entry.Subject);

        ImGui.EndChild();
        ImGui.EndGroup();
        ImGui.PopID();
    }

    static void ApplyLauncherTheme()
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding = 0f;
        style.ChildRounding = 6f;
        style.FrameRounding = 4f;
        style.GrabRounding = 4f;
        style.WindowBorderSize = 0f;
        style.ChildBorderSize = 1f;
        style.FramePadding = new float2(12f, 6f);
        style.ItemSpacing = new float2(10f, 8f);

        Set(style, ImGuiCol.WindowBg, 0.09f, 0.1f, 0.12f);
        Set(style, ImGuiCol.ChildBg, 0.14f, 0.15f, 0.18f);
        Set(style, ImGuiCol.Button, 0.18f, 0.38f, 0.62f);
        Set(style, ImGuiCol.ButtonHovered, 0.24f, 0.48f, 0.78f);
        Set(style, ImGuiCol.ButtonActive, 0.14f, 0.32f, 0.55f);
        Set(style, ImGuiCol.Border, 0.28f, 0.3f, 0.34f);
        Set(style, ImGuiCol.Text, 0.92f, 0.93f, 0.95f);
    }

    static void Set(ImGuiStylePtr style, ImGuiCol col, float r, float g, float b)
        => style.Colors[(int)col] = new float4(r, g, b, 1f);
}
