using System.Runtime.InteropServices;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Internal;
using Brutal.ImGuizmoAPI;
using Brutal.ImPlotApi;
using Brutal.Numerics;

/// <summary>
/// ImGui shell that copies Unity Editor's usual layout: menu, play-mode
/// toolbar, Hierarchy, Scene/Game, Inspector, Project, Console, Profiler.
/// Engine behaviour is stubbed so you can study the ImGui calls in isolation.
/// </summary>
sealed class UnityEngineEditorStudy
{
    static readonly string[] Layers = ["Default", "TransparentFX", "Ignore Raycast", "Water", "UI"];
    static readonly string[] ConsoleFilters = ["All", "Info", "Warning", "Error"];

    readonly UnityStubScene _scene = UnityStubScene.CreateSampleScene();
    readonly List<ConsoleLine> _console = [];
    readonly float[] _fpsHistory = new float[180];
    readonly List<float4x4> _cubeMatrices = [];

    int _selectedId;
    int _gizmoMode; // 0 hand, 1 move, 2 rotate, 3 scale
    int _gizmoSpace; // 0 local, 1 world
    int _consoleFilter;
    bool _playing;
    bool _paused;
    bool _dockLayoutBuilt;
    bool _selectSceneTab;
    bool _useSnap;
    float _playTime;
    int _fpsWrite;
    int _fpsCount;
    float _sceneFocusDistance = 12f;
    float3 _sceneTarget = new(0f, 0.5f, 0f);
    float4x4 _sceneView;
    float4x4 _sceneView3D;
    bool _scene2D;
    bool _showProjectSettings;
    int _projectSettingsPage;
    float _sceneCameraFov = 60f;
    float _sceneCameraNear = 0.3f;
    float _sceneCameraFar = 1000f;
    string? _selectedAsset;
    bool _assetStubToggle = true;
    bool _assetStubSrgb = true;
    bool _assetStubMips = true;
    float _assetStubSmoothness = 0.5f;
    float3 _assetStubColor = new(0.8f, 0.8f, 0.8f);

    public string WindowTitle => "Unity Editor — ImGui Study";

    public void Setup()
    {
        ApplyUnityLikeTheme();
        _sceneView = float4x4.CreateLookAt(new float3(7f, 5.5f, -8f), _sceneTarget, new float3(0f, 1f, 0f));
        _gizmoMode = 1;
        var player = FindByName("Player");
        if (player is not null)
            SelectGameObject(player.Id);

        Log(ConsoleSeverity.Info, "Unity editor study ready. Scene gizmos are ImGuizmo; the rest is stubbed.");
        Log(ConsoleSeverity.Info, "Scene: RMB look + WASD/QE fly, MMB pan, Alt+LMB orbit, Alt+RMB or wheel dolly, F to focus.");
        Log(ConsoleSeverity.Warning, "Play Mode does not run a player loop — it only advances stub animation.");
    }

    public void Draw(Action requestClose)
    {
        var io = ImGui.GetIO();
        var dt = io.DeltaTime;
        if (!ImGuizmo.IsUsing())
            TickPlayMode(dt);
        RecordFps(io.Framerate);

        ImGuizmo.BeginFrame();

        DrawHostWindow(requestClose);
        DrawHierarchy();
        DrawSceneView();
        DrawGameView();
        DrawInspector();
        DrawProject();
        DrawConsole();
        DrawProfiler(io.Framerate);
        DrawProjectSettings();

        // DockBuilder selects the last window docked into a node. Focus Scene after
        // every panel has been submitted so the center tabs open on Scene, not Profiler.
        if (_selectSceneTab)
        {
            ImGui.SetWindowFocus("Scene"u8);
            _selectSceneTab = false;
        }
    }

    void TickPlayMode(float dt)
    {
        if (!_playing || _paused)
            return;

        _playTime += dt;
        var player = FindByName("Player");
        if (player is null)
            return;

        // Stub "gameplay": orbit the player so Inspector values visibly change.
        var t = _playTime;
        player.Position = new float3(MathF.Sin(t) * 1.5f, 1f, MathF.Cos(t) * 1.5f);
        player.Rotation = new float3(0f, t * 40f, 0f);
    }

    StubGameObject? FindByName(string name)
    {
        foreach (var root in _scene.Roots)
        {
            var found = FindByName(root, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    static StubGameObject? FindByName(StubGameObject node, string name)
    {
        if (node.Name == name)
            return node;

        foreach (var child in node.Children)
        {
            var found = FindByName(child, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    void RecordFps(float fps)
    {
        _fpsHistory[_fpsWrite] = fps;
        _fpsWrite = (_fpsWrite + 1) % _fpsHistory.Length;
        if (_fpsCount < _fpsHistory.Length)
            _fpsCount++;
    }

    void DrawHostWindow(Action requestClose)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.SetNextWindowViewport(viewport.ID);

        const ImGuiWindowFlags hostFlags =
            ImGuiWindowFlags.MenuBar
            | ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNavFocus;

        // Rounding/border/padding must be set before Begin so the host window itself is flush.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(0f, 0f));
        ImGui.Begin("Unity Editor Host"u8, hostFlags);
        ImGui.PopStyleVar(3);

        DrawMainMenu(requestClose);
        DrawToolbar();

        var dockspaceId = ImGui.GetID("UnityDockSpace"u8);
        if (!_dockLayoutBuilt)
        {
            BuildUnityDockLayout(dockspaceId, ImGui.GetContentRegionAvail());
            _dockLayoutBuilt = true;
            _selectSceneTab = true;
        }

        ImGui.DockSpace(dockspaceId);
        ImGui.End();
    }

    void DrawMainMenu(Action requestClose)
    {
        if (!ImGui.BeginMenuBar())
            return;

        if (ImGui.BeginMenu("File"u8))
        {
            if (ImGui.MenuItem("New Scene"u8, "Ctrl+N"u8))
                Log(ConsoleSeverity.Info, "File > New Scene (stub)");
            if (ImGui.MenuItem("Open Scene"u8, "Ctrl+O"u8))
                Log(ConsoleSeverity.Info, "File > Open Scene (stub)");
            if (ImGui.MenuItem("Save"u8, "Ctrl+S"u8))
                Log(ConsoleSeverity.Info, $"Saved scene '{_scene.Name}' (stub)");
            ImGui.Separator();
            if (ImGui.MenuItem("Exit"u8, "Alt+F4"u8))
                requestClose();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"u8))
        {
            ImGui.MenuItem("Undo"u8, "Ctrl+Z"u8, false, false);
            ImGui.MenuItem("Redo"u8, "Ctrl+Y"u8, false, false);
            ImGui.Separator();
            if (ImGui.MenuItem("Play"u8, "Ctrl+P"u8))
                TogglePlay();
            ImGui.Separator();
            if (ImGui.MenuItem("Project Settings..."u8))
                _showProjectSettings = true;
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Assets"u8))
        {
            ImGui.MenuItem("Create"u8, ImString.Empty, false, false);
            if (ImGui.MenuItem("Refresh"u8, "Ctrl+R"u8))
                Log(ConsoleSeverity.Info, "Asset database refresh (stub)");
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("GameObject"u8))
        {
            if (ImGui.MenuItem("Create Empty"u8, "Ctrl+Shift+N"u8))
                CreateEmpty();
            if (ImGui.BeginMenu("3D Object"u8))
            {
                ImGui.MenuItem("Cube"u8, ImString.Empty, false, false);
                ImGui.MenuItem("Sphere"u8, ImString.Empty, false, false);
                ImGui.MenuItem("Plane"u8, ImString.Empty, false, false);
                ImGui.EndMenu();
            }

            ImGui.EndMenu();
        }

        ImGui.EndMenuBar();
    }

    void DrawToolbar()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(8f, 6f));
        ImGui.BeginChild("UnityToolbar"u8, new float2(0f, ImGui.GetFrameHeightWithSpacing() + 8f), ImGuiChildFlags.Borders);
        ImGui.PopStyleVar();

        var iconSize = new float2(ImGui.GetFrameHeight());
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new float2(2f, 0f));
        DrawGizmoToolButton("##Hand"u8, iconSize, 0, ToolbarIcon.Hand, "Hand (Q)"u8);
        ImGui.SameLine();
        DrawGizmoToolButton("##Move"u8, iconSize, 1, ToolbarIcon.Move, "Move (W)"u8);
        ImGui.SameLine();
        DrawGizmoToolButton("##Rotate"u8, iconSize, 2, ToolbarIcon.Rotate, "Rotate (E)"u8);
        ImGui.SameLine();
        DrawGizmoToolButton("##Scale"u8, iconSize, 3, ToolbarIcon.Scale, "Scale (R)"u8);
        ImGui.PopStyleVar();
        ImGui.SameLine();
        ImGui.TextDisabled("|"u8);
        ImGui.SameLine();
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new float2(2f, 0f));
        ImGui.SetNextItemWidth(88f);
        ImGui.Combo("##GizmoSpace"u8, ref _gizmoSpace, "Local\0World\0"u8);
        ImGui.SetItemTooltip("Coordinate space"u8);
        ImGui.SameLine();
        if (DrawToolbarIconButton("##Snap"u8, iconSize, enabled: true, _useSnap, ToolbarIcon.Snap))
            _useSnap = !_useSnap;
        ImGui.SetItemTooltip(_useSnap ? "Snap on"u8 : "Snap off"u8);
        ImGui.PopStyleVar();

        var playWidth = iconSize.X * 3f + 4f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX() + 16f, (ImGui.GetWindowWidth() - playWidth) * 0.5f));

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new float2(2f, 0f));
        if (DrawToolbarIconButton("##Play"u8, iconSize, enabled: true, _playing && !_paused, ToolbarIcon.Play))
            TogglePlay();
        ImGui.SetItemTooltip(_playing ? "Stop"u8 : "Play"u8);

        ImGui.SameLine();
        ImGui.BeginDisabled(!_playing);
        if (DrawToolbarIconButton("##Pause"u8, iconSize, _playing, _paused, ToolbarIcon.Pause))
        {
            _paused = !_paused;
            Log(ConsoleSeverity.Info, _paused ? "Paused (stub)" : "Resumed (stub)");
        }

        ImGui.SetItemTooltip("Pause"u8);
        ImGui.SameLine();
        if (DrawToolbarIconButton("##Step"u8, iconSize, _playing, false, ToolbarIcon.Step))
        {
            _paused = true;
            TickPlayMode(1f / 60f);
            Log(ConsoleSeverity.Info, "Frame step (stub)");
        }

        ImGui.SetItemTooltip("Step"u8);
        ImGui.EndDisabled();
        ImGui.PopStyleVar();

        ImGui.SameLine();
        ImGui.TextDisabled($"  {_scene.Name}{(UnsavedMarker())}");

        ImGui.EndChild();
    }

    string UnsavedMarker() => _playing ? "  [Playing]" : "";

    void DrawGizmoToolButton(ImString id, float2 size, int mode, ToolbarIcon icon, ImString tooltip)
    {
        if (DrawToolbarIconButton(id, size, enabled: true, _gizmoMode == mode, icon))
            _gizmoMode = mode;
        ImGui.SetItemTooltip(tooltip);
    }

    static bool DrawToolbarIconButton(ImString id, float2 size, bool enabled, bool active, ToolbarIcon icon)
    {
        var pressed = ImGui.InvisibleButton(id, size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var hovered = ImGui.IsItemHovered();
        var held = ImGui.IsItemActive();

        var background = active
            ? new float4(0.18f, 0.42f, 0.72f, 1f)
            : held
                ? new float4(0.2f, 0.2f, 0.2f, 1f)
                : hovered && enabled
                    ? new float4(0.34f, 0.34f, 0.34f, 1f)
                    : new float4(0.26f, 0.26f, 0.26f, 1f);
        var glyph = !enabled
            ? new float4(0.5f, 0.5f, 0.5f, 1f)
            : new float4(0.92f, 0.92f, 0.92f, 1f);

        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, max, Color(background), 3f);
        DrawToolbarIcon(draw, min, max, icon, Color(glyph));
        return pressed;
    }

    static void DrawToolbarIcon(ImDrawListPtr draw, float2 min, float2 max, ToolbarIcon icon, ImColor8 color)
    {
        var center = (min + max) * 0.5f;
        var extent = (max.X - min.X) * 0.28f;

        switch (icon)
        {
            case ToolbarIcon.Hand:
                DrawHandIcon(draw, center, extent, color);
                break;
            case ToolbarIcon.Move:
                DrawMoveIcon(draw, center, extent, color);
                break;
            case ToolbarIcon.Rotate:
                DrawRotateIcon(draw, center, extent, color);
                break;
            case ToolbarIcon.Scale:
                DrawScaleIcon(draw, center, extent, color);
                break;
            case ToolbarIcon.Snap:
                DrawSnapIcon(draw, center, extent, color);
                break;
            case ToolbarIcon.Pause:
            {
                var barWidth = extent * 0.38f;
                var gap = extent * 0.42f;
                var top = center.Y - extent;
                var bottom = center.Y + extent;
                draw.AddRectFilled(new float2(center.X - gap - barWidth, top), new float2(center.X - gap, bottom), color, 1f);
                draw.AddRectFilled(new float2(center.X + gap, top), new float2(center.X + gap + barWidth, bottom), color, 1f);
                break;
            }
            case ToolbarIcon.Step:
            {
                var left = center.X - extent * 1.05f;
                var right = center.X + extent * 0.45f;
                draw.AddTriangleFilled(
                    new float2(left, center.Y - extent),
                    new float2(left, center.Y + extent),
                    new float2(right, center.Y),
                    color);
                var barLeft = center.X + extent * 0.62f;
                draw.AddRectFilled(
                    new float2(barLeft, center.Y - extent),
                    new float2(barLeft + extent * 0.38f, center.Y + extent),
                    color,
                    1f);
                break;
            }
            default:
                draw.AddTriangleFilled(
                    new float2(center.X - extent * 0.75f, center.Y - extent),
                    new float2(center.X - extent * 0.75f, center.Y + extent),
                    new float2(center.X + extent, center.Y),
                    color);
                break;
        }
    }

    static void DrawHandIcon(ImDrawListPtr draw, float2 center, float extent, ImColor8 color)
    {
        var palm = extent * 0.85f;
        draw.AddRectFilled(
            new float2(center.X - palm * 0.55f, center.Y - extent * 0.05f),
            new float2(center.X + palm * 0.7f, center.Y + extent * 1.05f),
            color,
            2.5f);

        for (var i = 0; i < 4; i++)
        {
            var x = center.X - palm * 0.5f + i * (palm * 0.32f);
            var height = i is 1 or 2 ? extent * 1.2f : extent * 0.95f;
            draw.AddRectFilled(
                new float2(x, center.Y - height),
                new float2(x + palm * 0.22f, center.Y + extent * 0.1f),
                color,
                1.8f);
        }

        draw.AddRectFilled(
            new float2(center.X - palm * 1.05f, center.Y + extent * 0.05f),
            new float2(center.X - palm * 0.35f, center.Y + extent * 0.5f),
            color,
            1.8f);
    }

    static void DrawMoveIcon(ImDrawListPtr draw, float2 center, float extent, ImColor8 color)
    {
        var tip = extent * 1.15f;
        var thickness = 1.6f;
        draw.AddLine(new float2(center.X - tip, center.Y), new float2(center.X + tip, center.Y), color, thickness);
        draw.AddLine(new float2(center.X, center.Y - tip), new float2(center.X, center.Y + tip), color, thickness);
        DrawArrowHead(draw, new float2(center.X + tip, center.Y), new float2(1f, 0f), extent * 0.45f, color);
        DrawArrowHead(draw, new float2(center.X - tip, center.Y), new float2(-1f, 0f), extent * 0.45f, color);
        DrawArrowHead(draw, new float2(center.X, center.Y - tip), new float2(0f, -1f), extent * 0.45f, color);
        DrawArrowHead(draw, new float2(center.X, center.Y + tip), new float2(0f, 1f), extent * 0.45f, color);
        var box = extent * 0.22f;
        draw.AddRectFilled(new float2(center.X - box, center.Y - box), new float2(center.X + box, center.Y + box), color);
    }

    static void DrawRotateIcon(ImDrawListPtr draw, float2 center, float extent, ImColor8 color)
    {
        var radius = extent * 1.05f;
        var start = 0.55f;
        var end = MathF.PI * 1.7f;
        draw.PathArcTo(center, radius, start, end, 18);
        draw.PathStroke(color, ImDrawFlags.None, 1.8f);
        var tip = new float2(center.X + MathF.Cos(end) * radius, center.Y + MathF.Sin(end) * radius);
        var tangent = new float2(-MathF.Sin(end), MathF.Cos(end));
        DrawArrowHead(draw, tip, tangent, extent * 0.5f, color);
    }

    static void DrawScaleIcon(ImDrawListPtr draw, float2 center, float extent, ImColor8 color)
    {
        var inner = extent * 0.42f;
        draw.AddRect(
            new float2(center.X - inner, center.Y - inner),
            new float2(center.X + inner, center.Y + inner),
            color,
            0f,
            ImDrawFlags.None,
            1.6f);
        var outTip = extent * 1.15f;
        DrawArrowHead(draw, new float2(center.X + outTip, center.Y + outTip), new float2(1f, 1f), extent * 0.42f, color);
        DrawArrowHead(draw, new float2(center.X - outTip, center.Y - outTip), new float2(-1f, -1f), extent * 0.42f, color);
        DrawArrowHead(draw, new float2(center.X + outTip, center.Y - outTip), new float2(1f, -1f), extent * 0.42f, color);
        DrawArrowHead(draw, new float2(center.X - outTip, center.Y + outTip), new float2(-1f, 1f), extent * 0.42f, color);
    }

    static void DrawSnapIcon(ImDrawListPtr draw, float2 center, float extent, ImColor8 color)
    {
        var width = extent * 0.55f;
        var top = center.Y - extent * 0.15f;
        var bottom = center.Y + extent * 1.05f;
        var pole = extent * 0.28f;
        draw.AddRectFilled(new float2(center.X - width - pole, top), new float2(center.X - width, bottom), color, 1.2f);
        draw.AddRectFilled(new float2(center.X + width, top), new float2(center.X + width + pole, bottom), color, 1.2f);
        draw.PathArcTo(new float2(center.X, top), width + pole * 0.5f, MathF.PI, MathF.PI * 2f, 14);
        draw.PathStroke(color, ImDrawFlags.None, pole);
        draw.AddLine(
            new float2(center.X + width + pole * 1.6f, center.Y - extent * 0.55f),
            new float2(center.X + width + pole * 2.4f, center.Y - extent * 1.05f),
            color,
            1.4f);
        draw.AddLine(
            new float2(center.X + width + pole * 1.8f, center.Y - extent * 0.15f),
            new float2(center.X + width + pole * 2.7f, center.Y - extent * 0.45f),
            color,
            1.4f);
    }

    static void DrawArrowHead(ImDrawListPtr draw, float2 tip, float2 direction, float size, ImColor8 color)
    {
        var length = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (length < 0.0001f)
            return;

        var dir = direction / length;
        var n = new float2(-dir.Y, dir.X);
        var basePoint = tip - dir * size;
        draw.AddTriangleFilled(tip, basePoint + n * (size * 0.55f), basePoint - n * (size * 0.55f), color);
    }

    static ImColor8 Color(float4 value) => (ImColor8)ImGui.GetColorU32(value);

    void TogglePlay()
    {
        if (_playing)
        {
            _playing = false;
            _paused = false;
            _playTime = 0f;
            Log(ConsoleSeverity.Info, "Exited Play Mode (stub)");
            return;
        }

        _playing = true;
        _paused = false;
        Log(ConsoleSeverity.Info, "Entered Play Mode (stub)");
    }

    void CreateEmpty()
    {
        var created = new StubGameObject("GameObject");
        _scene.Roots.Add(created);
        SelectGameObject(created.Id);
        Log(ConsoleSeverity.Info, "Created empty GameObject (stub)");
    }

    static void BuildUnityDockLayout(ImGuiID dockspaceId, float2 size)
    {
        // DockBuilder is still technically internal, but it is the usual way
        // to reproduce a known editor layout on first launch.
        ImGui.Internal.DockBuilderRemoveNode(dockspaceId);
        ImGui.Internal.DockBuilderAddNode(dockspaceId, (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.DockSpace);
        ImGui.Internal.DockBuilderSetNodeSize(dockspaceId, size);

        var dockMain = dockspaceId;
        ImGui.Internal.DockBuilderSplitNode(dockMain, ImGuiDir.Left, 0.18f, out var dockLeft, out dockMain);
        ImGui.Internal.DockBuilderSplitNode(dockMain, ImGuiDir.Right, 0.26f, out var dockRight, out dockMain);
        ImGui.Internal.DockBuilderSplitNode(dockMain, ImGuiDir.Down, 0.28f, out var dockBottom, out dockMain);
        ImGui.Internal.DockBuilderSplitNode(dockBottom, ImGuiDir.Right, 0.45f, out var dockConsole, out var dockProject);

        ImGui.Internal.DockBuilderDockWindow("Hierarchy"u8, dockLeft);
        ImGui.Internal.DockBuilderDockWindow("Inspector"u8, dockRight);
        ImGui.Internal.DockBuilderDockWindow("Game"u8, dockMain);
        ImGui.Internal.DockBuilderDockWindow("Profiler"u8, dockMain);
        ImGui.Internal.DockBuilderDockWindow("Scene"u8, dockMain);
        ImGui.Internal.DockBuilderDockWindow("Project"u8, dockProject);
        ImGui.Internal.DockBuilderDockWindow("Console"u8, dockConsole);
        ImGui.Internal.DockBuilderFinish(dockspaceId);
    }

    void DrawHierarchy()
    {
        if (!ImGui.Begin("Hierarchy"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled($"Scene: {_scene.Name}");
        ImGui.Separator();

        foreach (var root in _scene.Roots)
            DrawHierarchyNode(root);

        ImGui.End();
    }

    void DrawHierarchyNode(StubGameObject node)
    {
        var flags = ImGuiTreeNodeFlags.OpenOnArrow
                    | ImGuiTreeNodeFlags.SpanAvailWidth
                    | ImGuiTreeNodeFlags.DrawLinesToNodes;
        if (node.Id == _selectedId)
            flags |= ImGuiTreeNodeFlags.Selected;
        if (node.Children.Count == 0)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

        ImGui.PushStyleColor(ImGuiCol.Text, node.Active
            ? new float4(0.92f, 0.92f, 0.92f, 1f)
            : new float4(0.55f, 0.55f, 0.55f, 1f));

        var opened = ImGui.TreeNodeEx($"{node.Name}##{node.Id}", flags);
        ImGui.PopStyleColor();

        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            SelectGameObject(node.Id);

        if (!opened || node.Children.Count == 0)
            return;

        foreach (var child in node.Children)
            DrawHierarchyNode(child);

        ImGui.TreePop();
    }

    void DrawSceneView()
    {
        if (!ImGui.Begin("Scene"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled($"Scene  |  {GizmoName(_gizmoMode)}  |  {(_gizmoSpace == 0 ? "Local" : "World")}  |  FOV {_sceneCameraFov:0}");
        Draw3DViewport(new float4(0.18f, 0.19f, 0.2f, 1f), _sceneView, sceneTools: true);
        ImGui.End();
    }

    void DrawGameView()
    {
        if (!ImGui.Begin("Game"u8))
        {
            ImGui.End();
            return;
        }

        var cameraObject = FindByName("Main Camera");
        var camera = cameraObject?.Components.OfType<StubCameraComponent>().FirstOrDefault();
        var fov = camera?.FieldOfView ?? 60f;
        ImGui.TextDisabled($"Display 1  |  Free Aspect  |  FOV {fov:0}  |  Scale 1x");
        Draw3DViewport(new float4(0.12f, 0.13f, 0.14f, 1f), BuildGameView(cameraObject), sceneTools: false);
        ImGui.End();
    }

    void Draw3DViewport(float4 background, float4x4 view, bool sceneTools)
    {
        var avail = ImGui.GetContentRegionAvail();
        if (avail.X < 32f || avail.Y < 32f)
            return;

        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(avail);
        var draw = ImGui.GetWindowDrawList();
        var max = new float2(origin.X + avail.X, origin.Y + avail.Y);
        draw.AddRectFilled(origin, max, ImGui.GetColorU32(background));

        var gameCamera = FindByName("Main Camera")?.Components.OfType<StubCameraComponent>().FirstOrDefault();
        var aspect = avail.X / MathF.Max(avail.Y, 1f);
        var sceneOrtho = sceneTools && _scene2D;
        var projection = sceneTools
            ? BuildSceneProjection(aspect)
            : BuildProjection(aspect, gameCamera);

        ImGuizmo.SetDrawlist(draw);
        ImGuizmo.SetRect(origin.X, origin.Y, avail.X, avail.Y);
        ImGuizmo.SetOrthographic(sceneOrtho || (!sceneTools && (gameCamera?.Orthographic ?? false)));
        if (sceneTools)
        {
            var grid = sceneOrtho
                ? float4x4.CreateRotationX(-MathF.PI / 2f)
                : float4x4.Identity;
            ImGuizmo.DrawGrid(view, projection, grid, 20f);
        }

        CollectCubeMatrices();
        if (_cubeMatrices.Count > 0)
            ImGuizmo.DrawCubes(view, projection, CollectionsMarshal.AsSpan(_cubeMatrices));
        if (sceneTools && !_scene2D)
            DrawSceneCameraGizmos(draw, view, projection, origin, avail, aspect);

        var overlayHovered = false;
        if (sceneTools)
        {
            overlayHovered = DrawSceneViewModeToggle(origin);
            var cameraChord = SceneCameraChordActive();
            if (!cameraChord)
                ManipulateSelected(view, projection);
            if (!overlayHovered && !cameraChord)
                TryPickSceneObject(view, projection, origin, avail);
            if (!overlayHovered)
                HandleSceneNavigation();
            if (!_scene2D && !cameraChord)
            {
                var cubeSize = new float2(96f, 96f);
                var cubePos = new float2(max.X - cubeSize.X - 8f, origin.Y + 8f);
                ImGuizmo.ViewManipulate(ref _sceneView, _sceneFocusDistance, cubePos, cubeSize, 0x10101010u);
                SyncSceneTargetFromView();
            }
        }

        if (!sceneTools)
        {
            draw.AddText(
                new float2(origin.X + 10f, origin.Y + 8f),
                ImGui.GetColorU32(new float4(1f, 1f, 1f, 0.65f)),
                _playing ? "Game (playing)" : "Game (edit mode)");
        }
    }

    bool DrawSceneViewModeToggle(float2 origin)
    {
        ImGui.SetCursorScreenPos(origin + new float2(8f, 8f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new float4(0.12f, 0.12f, 0.13f, 0.86f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(4f, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new float2(2f, 0f));
        ImGui.BeginChild("SceneViewMode"u8, new float2(84f, ImGui.GetFrameHeight() + 8f), ImGuiChildFlags.Borders);

        if (DrawSceneModeButton("2D"u8, _scene2D))
            SetScene2D(true);
        ImGui.SameLine();
        if (DrawSceneModeButton("3D"u8, !_scene2D))
            SetScene2D(false);

        var hovered = ImGui.IsWindowHovered();
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
        return hovered;
    }

    static bool DrawSceneModeButton(ImString label, bool active)
    {
        if (active)
            ImGui.PushStyleColor(ImGuiCol.Button, new float4(0.18f, 0.42f, 0.72f, 1f));
        var pressed = ImGui.Button(label, new float2(36f, 0f));
        if (active)
            ImGui.PopStyleColor();
        return pressed && !active;
    }

    void SetScene2D(bool enabled)
    {
        if (_scene2D == enabled)
            return;

        if (enabled)
        {
            _sceneView3D = _sceneView;
            _scene2D = true;
            Apply2DSceneView();
            Log(ConsoleSeverity.Info, "Scene view: 2D");
            return;
        }

        _scene2D = false;
        _sceneView = _sceneView3D;
        SyncSceneTargetFromView();
        Log(ConsoleSeverity.Info, "Scene view: 3D");
    }

    void Apply2DSceneView()
        => _sceneView = float4x4.CreateLookAt(
            _sceneTarget + new float3(0f, 0f, -_sceneFocusDistance),
            _sceneTarget,
            new float3(0f, 1f, 0f));

    void DrawSceneCameraGizmos(ImDrawListPtr draw, float4x4 view, float4x4 projection, float2 origin, float2 size, float aspect)
    {
        foreach (var root in _scene.Roots)
            DrawSceneCameraGizmos(root, draw, view, projection, origin, size, aspect);
    }

    void DrawSceneCameraGizmos(StubGameObject node, ImDrawListPtr draw, float4x4 view, float4x4 projection, float2 origin, float2 size, float aspect)
    {
        var camera = node.Components.OfType<StubCameraComponent>().FirstOrDefault();
        if (node.Active && camera is not null)
            DrawCameraSymbol(draw, node, camera, view, projection, origin, size, aspect);

        foreach (var child in node.Children)
            DrawSceneCameraGizmos(child, draw, view, projection, origin, size, aspect);
    }

    void DrawCameraSymbol(ImDrawListPtr draw, StubGameObject node, StubCameraComponent camera, float4x4 view, float4x4 projection, float2 origin, float2 size, float aspect)
    {
        var world = GetWorldMatrix(node);
        var viewProjection = view * projection;
        var selected = node.Id == _selectedId;
        var color = Color(selected
            ? new float4(0.45f, 0.85f, 1f, 1f)
            : new float4(0.82f, 0.82f, 0.82f, 1f));
        var thickness = selected ? 2.2f : 1.5f;

        var body = new float3[]
        {
            new(-0.18f, -0.12f, -0.16f),
            new(0.18f, -0.12f, -0.16f),
            new(0.18f, 0.12f, -0.16f),
            new(-0.18f, 0.12f, -0.16f),
            new(-0.18f, -0.12f, 0.16f),
            new(0.18f, -0.12f, 0.16f),
            new(0.18f, 0.12f, 0.16f),
            new(-0.18f, 0.12f, 0.16f),
        };
        DrawProjectedEdges(draw, world, viewProjection, origin, size, body, [
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7),
        ], color, thickness);

        var fov = camera.Orthographic ? 60f : camera.FieldOfView;
        var tan = MathF.Tan(fov * (MathF.PI / 180f) * 0.5f);
        var near = 0.22f;
        var far = 1.15f;
        var nearH = near * tan;
        var nearW = nearH * aspect;
        var farH = far * tan;
        var farW = farH * aspect;
        var frustum = new float3[]
        {
            new(-nearW, -nearH, near),
            new(nearW, -nearH, near),
            new(nearW, nearH, near),
            new(-nearW, nearH, near),
            new(-farW, -farH, far),
            new(farW, -farH, far),
            new(farW, farH, far),
            new(-farW, farH, far),
        };
        DrawProjectedEdges(draw, world, viewProjection, origin, size, frustum, [
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7),
        ], color, thickness);

        var up = new float3[]
        {
            new(0f, 0.12f, -0.02f),
            new(-0.08f, 0.28f, -0.02f),
            new(0.08f, 0.28f, -0.02f),
        };
        DrawProjectedEdges(draw, world, viewProjection, origin, size, up, [(0, 1), (1, 2), (2, 0)], color, thickness);
    }

    void DrawProjectedEdges(ImDrawListPtr draw, float4x4 world, float4x4 viewProjection, float2 origin, float2 size, float3[] points, (int A, int B)[] edges, ImColor8 color, float thickness)
    {
        var screens = new float2[points.Length];
        var visible = new bool[points.Length];
        for (var i = 0; i < points.Length; i++)
            visible[i] = ProjectToScreen(TransformPoint(world, points[i]), viewProjection, origin, size, out screens[i]);

        foreach (var (a, b) in edges)
        {
            if (visible[a] && visible[b])
                draw.AddLine(screens[a], screens[b], color, thickness);
        }
    }

    static bool ProjectToScreen(float3 world, float4x4 viewProjection, float2 origin, float2 size, out float2 screen)
    {
        var clip = new float4(
            world.X * viewProjection.X.X + world.Y * viewProjection.Y.X + world.Z * viewProjection.Z.X + viewProjection.W.X,
            world.X * viewProjection.X.Y + world.Y * viewProjection.Y.Y + world.Z * viewProjection.Z.Y + viewProjection.W.Y,
            world.X * viewProjection.X.Z + world.Y * viewProjection.Y.Z + world.Z * viewProjection.Z.Z + viewProjection.W.Z,
            world.X * viewProjection.X.W + world.Y * viewProjection.Y.W + world.Z * viewProjection.Z.W + viewProjection.W.W);
        if (clip.W <= 0.0001f)
        {
            screen = default;
            return false;
        }

        var ndcX = clip.X * 0.5f / clip.W + 0.5f;
        var ndcY = 1f - (clip.Y * 0.5f / clip.W + 0.5f);
        screen = new float2(origin.X + ndcX * size.X, origin.Y + ndcY * size.Y);
        return ndcX is > -0.2f and < 1.2f && ndcY is > -0.2f and < 1.2f;
    }

    void TryPickSceneObject(float4x4 view, float4x4 projection, float2 origin, float2 size)
    {
        if (!ImGui.IsWindowHovered()
            || !ImGui.IsMouseClicked(ImGuiMouseButton.Left, false)
            || ImGuizmo.IsUsing()
            || ImGuizmo.IsUsingViewManipulate()
            || ImGuizmo.IsViewManipulateHovered()
            || ImGuizmo.GetHoveredHandleType() != ImGuizmoMoveType.MT_NONE)
            return;

        var mouse = ImGui.GetIO().MousePos;
        if (mouse.X < origin.X || mouse.Y < origin.Y || mouse.X > origin.X + size.X || mouse.Y > origin.Y + size.Y)
            return;

        ImGuizmo.ComputeMouseRay(view, projection, mouse, origin, size, out var rayOrigin, out var rayDirection);
        if (rayDirection.LengthSquared() < 0.0001f)
            return;

        rayDirection = float3.Normalize(rayDirection);
        var bestDistance = float.MaxValue;
        var bestId = 0;
        foreach (var root in _scene.Roots)
            PickSceneObject(root, rayOrigin, rayDirection, ref bestDistance, ref bestId);

        SelectGameObject(bestId);
    }

    void PickSceneObject(StubGameObject node, float3 rayOrigin, float3 rayDirection, ref float bestDistance, ref int bestId)
    {
        if (node.Active && node.Components.OfType<StubMeshRendererComponent>().Any())
            TryPickOrientedBox(node, rayOrigin, rayDirection, new float3(-0.5f, -0.5f, -0.5f), new float3(0.5f, 0.5f, 0.5f), ref bestDistance, ref bestId);

        if (!_scene2D && node.Active && node.Components.OfType<StubCameraComponent>().Any())
            TryPickOrientedBox(node, rayOrigin, rayDirection, new float3(-0.22f, -0.16f, -0.18f), new float3(0.22f, 0.16f, 0.7f), ref bestDistance, ref bestId);

        foreach (var child in node.Children)
            PickSceneObject(child, rayOrigin, rayDirection, ref bestDistance, ref bestId);
    }

    void TryPickOrientedBox(StubGameObject node, float3 rayOrigin, float3 rayDirection, float3 min, float3 max, ref float bestDistance, ref int bestId)
    {
        var model = GetWorldMatrix(node);
        if (!float4x4.Invert(model, out var inverse)
            || !IntersectLocalCube(rayOrigin, rayDirection, inverse, model, min, max, out var distance)
            || distance >= bestDistance)
            return;

        bestDistance = distance;
        bestId = node.Id;
    }

    static bool IntersectLocalCube(float3 rayOrigin, float3 rayDirection, float4x4 worldToLocal, float4x4 localToWorld, float3 min, float3 max, out float distance)
    {
        distance = float.MaxValue;
        var origin = TransformPoint(worldToLocal, rayOrigin);
        var direction = TransformVector(worldToLocal, rayDirection);
        if (!IntersectRayAabb(origin, direction, min, max, out var t))
            return false;

        var localHit = origin + direction * t;
        var worldHit = TransformPoint(localToWorld, localHit);
        var alongRay = worldHit - rayOrigin;
        if (float3.Dot(alongRay, rayDirection) < 0f)
            return false;

        distance = alongRay.Length();
        return true;
    }

    static bool IntersectRayAabb(float3 origin, float3 direction, float3 min, float3 max, out float t)
    {
        t = 0f;
        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;
        if (!ClipAxis(origin.X, direction.X, min.X, max.X, ref tMin, ref tMax)
            || !ClipAxis(origin.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax)
            || !ClipAxis(origin.Z, direction.Z, min.Z, max.Z, ref tMin, ref tMax)
            || tMax < 0f)
            return false;

        t = tMin >= 0f ? tMin : tMax;
        return true;
    }

    static bool ClipAxis(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 0.0000001f)
            return origin >= min && origin <= max;

        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;
        if (t1 > t2)
            (t1, t2) = (t2, t1);
        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }

    static float3 TransformPoint(float4x4 matrix, float3 value)
        => new(
            value.X * matrix.X.X + value.Y * matrix.Y.X + value.Z * matrix.Z.X + matrix.W.X,
            value.X * matrix.X.Y + value.Y * matrix.Y.Y + value.Z * matrix.Z.Y + matrix.W.Y,
            value.X * matrix.X.Z + value.Y * matrix.Y.Z + value.Z * matrix.Z.Z + matrix.W.Z);

    static float3 TransformVector(float4x4 matrix, float3 value)
        => new(
            value.X * matrix.X.X + value.Y * matrix.Y.X + value.Z * matrix.Z.X,
            value.X * matrix.X.Y + value.Y * matrix.Y.Y + value.Z * matrix.Z.Y,
            value.X * matrix.X.Z + value.Y * matrix.Y.Z + value.Z * matrix.Z.Z);

    void ManipulateSelected(float4x4 view, float4x4 projection)
    {
        if (_gizmoMode == 0)
            return;

        var selected = _scene.Find(_selectedId);
        if (selected is null)
            return;

        var operation = _gizmoMode switch
        {
            2 => ImGuizmoOperation.Rotate,
            3 => ImGuizmoOperation.Scale,
            _ => ImGuizmoOperation.Translate,
        };
        var mode = _gizmoSpace == 0 ? ImGuizmoMode.Local : ImGuizmoMode.World;
        float3? snap = !_useSnap
            ? null
            : _gizmoMode switch
            {
                2 => new float3(15f, 15f, 15f),
                3 => new float3(0.1f, 0.1f, 0.1f),
                _ => new float3(0.5f, 0.5f, 0.5f),
            };

        var matrix = GetWorldMatrix(selected);
        ImGuizmo.PushId(selected.Id);
        if (ImGuizmo.Manipulate(view, projection, operation, mode, ref matrix, snap))
            SetWorldMatrix(selected, matrix);

        ImGuizmo.PopId();
    }

    static bool SceneCameraChordActive()
    {
        var alt = ImGui.GetIO().KeyAlt;
        return ImGui.IsMouseDown(ImGuiMouseButton.Right)
               || ImGui.IsMouseDown(ImGuiMouseButton.Middle)
               || (alt && ImGui.IsMouseDown(ImGuiMouseButton.Left));
    }

    void HandleSceneNavigation()
    {
        var rmb = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        var mmb = ImGui.IsMouseDown(ImGuiMouseButton.Middle);
        var lmb = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if ((!ImGui.IsWindowHovered() && !rmb && !mmb)
            || ImGuizmo.IsUsing()
            || ImGuizmo.IsUsingViewManipulate()
            || ImGuizmo.IsViewManipulateHovered())
            return;

        var io = ImGui.GetIO();
        var alt = io.KeyAlt;
        var delta = io.MouseDelta;

        if (_scene2D)
        {
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Middle)
                || ImGui.IsMouseDragging(ImGuiMouseButton.Right)
                || (_gizmoMode == 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                || (alt && ImGui.IsMouseDragging(ImGuiMouseButton.Left)))
                PanSceneCamera(delta);
            DollySceneCamera(io.MouseWheel);
            return;
        }

        if (alt && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            OrbitSceneCamera(delta);
        else if (alt && rmb)
            DollySceneCamera(-delta.Y * 0.04f);
        else if (mmb)
            PanSceneCamera(delta);
        else if (lmb && rmb)
            TruckSceneCamera(delta);
        else if (rmb)
        {
            if (delta.X != 0f || delta.Y != 0f)
                LookSceneCamera(delta);
            FlySceneCamera(io.DeltaTime);
        }
        else if (_gizmoMode == 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            PanSceneCamera(delta);

        DollySceneCamera(io.MouseWheel);

        if (ImGui.IsWindowHovered() && ImGui.IsKeyPressed(ImGuiKey.F, false))
            FocusSelectedInScene();
    }

    void LookSceneCamera(float2 mouseDelta)
    {
        var eye = SceneCameraPosition();
        var forward = _sceneTarget - eye;
        var distance = MathF.Max(0.5f, forward.Length());
        var yaw = MathF.Atan2(forward.X, forward.Z);
        var pitch = MathF.Asin(Math.Clamp(forward.Y / distance, -1f, 1f));
        yaw += mouseDelta.X * 0.005f;
        pitch = Math.Clamp(pitch - mouseDelta.Y * 0.005f, -1.4f, 1.4f);
        var direction = new float3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch));
        _sceneTarget = eye + direction * _sceneFocusDistance;
        _sceneView = float4x4.CreateLookAt(eye, _sceneTarget, new float3(0f, 1f, 0f));
    }

    void FlySceneCamera(float dt)
    {
        if (!float4x4.Invert(_sceneView, out var inverse))
            return;

        var right = new float3(inverse.X.X, inverse.X.Y, inverse.X.Z);
        var up = new float3(0f, 1f, 0f);
        var forward = float3.Normalize(new float3(-inverse.Z.X, -inverse.Z.Y, -inverse.Z.Z));
        var move = float3.Zero;
        if (ImGui.IsKeyDown(ImGuiKey.W))
            move += forward;
        if (ImGui.IsKeyDown(ImGuiKey.S))
            move -= forward;
        if (ImGui.IsKeyDown(ImGuiKey.D))
            move += right;
        if (ImGui.IsKeyDown(ImGuiKey.A))
            move -= right;
        if (ImGui.IsKeyDown(ImGuiKey.E))
            move += up;
        if (ImGui.IsKeyDown(ImGuiKey.Q))
            move -= up;
        if (move.LengthSquared() < 0.0001f)
            return;

        var speed = _sceneFocusDistance * (ImGui.GetIO().KeyShift ? 2.4f : 0.9f) * MathF.Max(dt, 0.0001f);
        var translation = float3.Normalize(move) * speed;
        var eye = SceneCameraPosition() + translation;
        _sceneTarget += translation;
        _sceneView = float4x4.CreateLookAt(eye, _sceneTarget, new float3(0f, 1f, 0f));
    }

    void TruckSceneCamera(float2 mouseDelta)
    {
        if (!float4x4.Invert(_sceneView, out var inverse))
            return;

        var right = new float3(inverse.X.X, inverse.X.Y, inverse.X.Z);
        var forward = float3.Normalize(new float3(-inverse.Z.X, -inverse.Z.Y, -inverse.Z.Z));
        var speed = _sceneFocusDistance * 0.0015f;
        var translation = forward * (-mouseDelta.Y * speed) + right * (mouseDelta.X * speed);
        var eye = SceneCameraPosition() + translation;
        _sceneTarget += translation;
        _sceneView = float4x4.CreateLookAt(eye, _sceneTarget, new float3(0f, 1f, 0f));
    }

    void DollySceneCamera(float amount)
    {
        if (amount == 0f)
            return;

        _sceneFocusDistance = Math.Clamp(_sceneFocusDistance * (1f - amount * 0.1f), 1.5f, 80f);
        RebuildSceneView();
    }

    void FocusSelectedInScene()
    {
        var selected = _scene.Find(_selectedId);
        if (selected is null)
            return;

        ImGuizmo.DecomposeMatrixToComponents(GetWorldMatrix(selected), out var worldPosition, out _, out var worldScale);
        _sceneTarget = worldPosition;
        _sceneFocusDistance = Math.Clamp(MathF.Max(worldScale.Length(), 1f) * 4f, 3f, 40f);
        RebuildSceneView();
    }

    void OrbitSceneCamera(float2 mouseDelta)
    {
        var eye = SceneCameraPosition();
        var offset = eye - _sceneTarget;
        var distance = MathF.Max(0.5f, offset.Length());
        var yaw = MathF.Atan2(offset.X, offset.Z);
        var pitch = MathF.Asin(Math.Clamp(offset.Y / distance, -1f, 1f));
        yaw += mouseDelta.X * 0.005f;
        pitch = Math.Clamp(pitch + mouseDelta.Y * 0.005f, -1.4f, 1.4f);
        var direction = new float3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch));
        _sceneFocusDistance = distance;
        _sceneView = float4x4.CreateLookAt(_sceneTarget + direction * distance, _sceneTarget, new float3(0f, 1f, 0f));
    }

    void PanSceneCamera(float2 mouseDelta)
    {
        if (!float4x4.Invert(_sceneView, out var inverse))
            return;

        var right = new float3(inverse.X.X, inverse.X.Y, inverse.X.Z);
        var up = new float3(inverse.Y.X, inverse.Y.Y, inverse.Y.Z);
        var speed = _sceneFocusDistance * 0.0015f;
        _sceneTarget -= right * (mouseDelta.X * speed) + up * (mouseDelta.Y * speed);
        RebuildSceneView();
    }

    void RebuildSceneView()
    {
        var eye = SceneCameraPosition();
        var offset = eye - _sceneTarget;
        if (offset.LengthSquared() < 0.0001f)
            offset = new float3(0f, 0.3f, -1f);
        if (_scene2D)
        {
            Apply2DSceneView();
            return;
        }

        var direction = float3.Normalize(offset);
        _sceneView = float4x4.CreateLookAt(_sceneTarget + direction * _sceneFocusDistance, _sceneTarget, new float3(0f, 1f, 0f));
    }

    void SyncSceneTargetFromView()
    {
        if (!float4x4.Invert(_sceneView, out var inverse))
            return;

        var eye = inverse.Translation;
        var forward = float3.Normalize(new float3(-inverse.Z.X, -inverse.Z.Y, -inverse.Z.Z));
        _sceneTarget = eye + forward * _sceneFocusDistance;
    }

    float3 SceneCameraPosition()
    {
        float4x4.Invert(_sceneView, out var inverse);
        return inverse.Translation;
    }

    float4x4 BuildGameView(StubGameObject? cameraObject)
    {
        if (cameraObject is null)
            return _sceneView;

        ImGuizmo.DecomposeMatrixToComponents(GetWorldMatrix(cameraObject), out var position, out var rotation, out _);
        var yaw = rotation.Y * (MathF.PI / 180f);
        var pitch = rotation.X * (MathF.PI / 180f);
        var forward = new float3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            -MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch));
        if (forward.LengthSquared() < 0.0001f)
            forward = new float3(0f, 0f, 1f);
        return float4x4.CreateLookAt(position, position + float3.Normalize(forward), new float3(0f, 1f, 0f));
    }

    float4x4 BuildSceneProjection(float aspect)
    {
        var near = MathF.Max(_sceneCameraNear, 0.01f);
        var far = MathF.Max(_sceneCameraFar, near + 0.01f);
        if (_scene2D)
            return float4x4.CreateOrthographic(_sceneFocusDistance * aspect, _sceneFocusDistance, near, far);

        var fov = Math.Clamp(_sceneCameraFov, 1f, 179f) * (MathF.PI / 180f);
        return float4x4.CreatePerspectiveFieldOfView(fov, aspect, near, far);
    }

    static float4x4 BuildProjection(float aspect, StubCameraComponent? camera)
    {
        var near = camera?.NearClip ?? 0.3f;
        var far = camera?.FarClip ?? 1000f;
        if (camera?.Orthographic == true)
            return float4x4.CreateOrthographic(10f * aspect, 10f, near, far);

        var fov = (camera?.FieldOfView ?? 60f) * (MathF.PI / 180f);
        return float4x4.CreatePerspectiveFieldOfView(fov, aspect, near, far);
    }

    void DrawProjectSettings()
    {
        if (!_showProjectSettings)
            return;

        ImGui.SetNextWindowSize(new float2(520f, 360f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Project Settings"u8, ref _showProjectSettings))
        {
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(220f);
        ImGui.Combo("Category"u8, ref _projectSettingsPage, "Scene Camera\0Quality\0Graphics\0"u8);
        ImGui.Separator();

        switch (_projectSettingsPage)
        {
            case 0:
                ImGui.TextDisabled("Scene view camera only. Game view uses Main Camera."u8);
                ImGui.SliderFloat("Field of View"u8, ref _sceneCameraFov, 1f, 179f);
                ImGui.DragFloat("Near Clip"u8, ref _sceneCameraNear, 0.01f, 0.01f, 10f);
                ImGui.DragFloat("Far Clip"u8, ref _sceneCameraFar, 1f, 1f, 5000f);
                ImGui.TextDisabled(_scene2D
                    ? "2D mode uses orthographic projection; FOV applies in 3D."
                    : "Perspective projection for the Scene tab.");
                break;
            case 1:
                ImGui.TextDisabled("Quality settings are stubbed."u8);
                break;
            default:
                ImGui.TextDisabled("Graphics settings are stubbed."u8);
                break;
        }

        ImGui.End();
    }

    void CollectCubeMatrices()
    {
        _cubeMatrices.Clear();
        foreach (var root in _scene.Roots)
            CollectCubeMatrices(root);
    }

    void CollectCubeMatrices(StubGameObject node)
    {
        if (node.Active && node.Components.OfType<StubMeshRendererComponent>().Any())
            _cubeMatrices.Add(GetWorldMatrix(node));

        foreach (var child in node.Children)
            CollectCubeMatrices(child);
    }

    static float4x4 GetLocalMatrix(StubGameObject node)
        => ImGuizmo.RecomposeMatrixFromComponents(node.Position, node.Rotation, node.Scale);

    static float4x4 GetWorldMatrix(StubGameObject node)
    {
        var local = GetLocalMatrix(node);
        return node.Parent is null ? local : local * GetWorldMatrix(node.Parent);
    }

    static void SetWorldMatrix(StubGameObject node, float4x4 world)
    {
        var local = world;
        if (node.Parent is not null && float4x4.Invert(GetWorldMatrix(node.Parent), out var inverseParent))
            local = world * inverseParent;

        ImGuizmo.DecomposeMatrixToComponents(local, out var translation, out var rotation, out var scale);
        node.Position = translation;
        node.Rotation = rotation;
        node.Scale = scale;
    }

    static string GizmoName(int mode) => mode switch
    {
        1 => "Move",
        2 => "Rotate",
        3 => "Scale",
        _ => "Hand",
    };

    void DrawInspector()
    {
        if (!ImGui.Begin("Inspector"u8))
        {
            ImGui.End();
            return;
        }

        if (_selectedAsset is not null)
        {
            DrawAssetInspector(_selectedAsset);
            ImGui.End();
            return;
        }

        var selected = _scene.Find(_selectedId);
        if (selected is null)
        {
            ImGui.TextDisabled("Select a GameObject in the Hierarchy or an asset in Project."u8);
            ImGui.End();
            return;
        }

        ImGui.PushID("Inspector"u8);
        ImGui.PushID(selected.Id);

        var active = selected.Active;
        if (ImGui.Checkbox("##Inspector.Active"u8, ref active))
            selected.Active = active;
        ImGui.SameLine();
        ImGui.Text(selected.Name);
        ImGui.SameLine();
        ImGui.TextDisabled($"  Instance ID {selected.Id}");

        var layerIndex = Math.Max(0, Array.IndexOf(Layers, selected.Layer));
        if (DrawCombo("Layer##Inspector"u8, ref layerIndex, Layers))
            selected.Layer = Layers[layerIndex];

        ImGui.Separator();
        ImGui.PushID("Transform"u8);
        if (ImGui.CollapsingHeader("Transform##Inspector"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            var position = selected.Position;
            var rotation = selected.Rotation;
            var scale = selected.Scale;
            ImGui.PushItemWidth(-1f);
            if (selected.Parent is not null)
                ImGui.TextDisabled($"Parent  {selected.Parent.Name}");
            if (ImGui.DragFloat3("Position##Inspector.Transform"u8, ref position, 0.05f))
                selected.Position = position;
            if (ImGui.DragFloat3("Rotation##Inspector.Transform"u8, ref rotation, 0.5f))
                selected.Rotation = rotation;
            if (ImGui.DragFloat3("Scale##Inspector.Transform"u8, ref scale, 0.05f))
                selected.Scale = scale;
            ImGui.PopItemWidth();
        }

        ImGui.PopID();

        for (var i = 0; i < selected.Components.Count; i++)
        {
            ImGui.PushID(i);
            DrawComponent(selected.Components[i]);
            ImGui.PopID();
        }

        ImGui.Spacing();
        if (ImGui.Button("Add Component##Inspector"u8, new float2(-1f, 0f)))
            Log(ConsoleSeverity.Warning, "Add Component menu is stubbed.");

        ImGui.PopID();
        ImGui.PopID();
        ImGui.End();
    }

    static void DrawComponent(StubComponent component)
    {
        ImGui.PushID(component.TypeName);
        var enabled = component.Enabled;
        var flags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap;
        if (!ImGui.CollapsingHeader($"{component.TypeName}##Inspector.Component", flags))
        {
            ImGui.PopID();
            return;
        }

        ImGui.Checkbox("Enabled##Inspector.Component"u8, ref enabled);
        component.Enabled = enabled;

        switch (component)
        {
            case StubCameraComponent camera:
                var fov = camera.FieldOfView;
                var near = camera.NearClip;
                var far = camera.FarClip;
                var ortho = camera.Orthographic;
                ImGui.SliderFloat("Field of View##Inspector.Camera"u8, ref fov, 1f, 179f);
                ImGui.DragFloat("Near##Inspector.Camera"u8, ref near, 0.01f, 0.01f, 10f);
                ImGui.DragFloat("Far##Inspector.Camera"u8, ref far, 1f, 1f, 5000f);
                ImGui.Checkbox("Orthographic##Inspector.Camera"u8, ref ortho);
                camera.FieldOfView = fov;
                camera.NearClip = near;
                camera.FarClip = far;
                camera.Orthographic = ortho;
                break;

            case StubMeshRendererComponent renderer:
                var meshColor = renderer.Color;
                var shadows = renderer.CastShadows;
                ImGui.ColorEdit3("Albedo##Inspector.MeshRenderer"u8, ref meshColor);
                ImGui.Checkbox("Cast Shadows##Inspector.MeshRenderer"u8, ref shadows);
                renderer.Color = meshColor;
                renderer.CastShadows = shadows;
                break;

            case StubMonoBehaviourComponent script:
                var speed = script.MoveSpeed;
                var jump = script.JumpHeight;
                ImGui.DragFloat("Move Speed##Inspector.Script"u8, ref speed, 0.05f, 0f, 30f);
                ImGui.DragFloat("Jump Height##Inspector.Script"u8, ref jump, 0.05f, 0f, 10f);
                script.MoveSpeed = speed;
                script.JumpHeight = jump;
                break;
        }

        ImGui.PopID();
    }

    static bool DrawCombo(ImString label, ref int index, string[] items)
    {
        var preview = items[Math.Clamp(index, 0, items.Length - 1)];
        if (!ImGui.BeginCombo(label, preview))
            return false;

        var changed = false;
        for (var i = 0; i < items.Length; i++)
        {
            var selected = i == index;
            if (ImGui.Selectable(items[i], selected) && !selected)
            {
                index = i;
                changed = true;
            }

            if (selected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
        return changed;
    }

    void DrawProject()
    {
        if (!ImGui.Begin("Project"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled("Assets  (stub folders, no files on disk)"u8);
        ImGui.Separator();

        DrawAssetFolder("Scenes", ["SampleScene.unity"]);
        DrawAssetFolder("Materials", ["Lit.mat", "Ground.mat"]);
        DrawAssetFolder("Textures", ["Default.png"]);

        ImGui.End();
    }

    void DrawAssetFolder(string folder, string[] assets)
    {
        if (!ImGui.TreeNode(folder))
            return;

        foreach (var asset in assets)
        {
            var path = $"{folder}/{asset}";
            var isScene = asset.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
            if (ImGui.Selectable(asset, !isScene && _selectedAsset == path) && !isScene)
            {
                _selectedAsset = path;
                _selectedId = 0;
            }
        }

        ImGui.TreePop();
    }

    void DrawAssetInspector(string path)
    {
        var slash = path.LastIndexOf('/');
        var name = slash >= 0 ? path[(slash + 1)..] : path;
        var extension = Path.GetExtension(name);

        ImGui.Text(name);
        ImGui.TextDisabled(AssetTypeName(extension));
        ImGui.Separator();
        ImGui.Text($"Path  Assets/{path}");
        ImGui.TextDisabled($"GUID  {AssetStubGuid(path)}");
        ImGui.TextDisabled($"Imported  stub, not on disk");

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Preview##Inspector.Asset"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            switch (extension.ToLowerInvariant())
            {
                case ".cs":
                    ImGui.Text("Importer  MonoImporter"u8);
                    ImGui.TextDisabled("Class  MonoBehaviour"u8);
                    ImGui.TextDisabled("Assembly  Assembly-CSharp"u8);
                    ImGui.Checkbox("Default References"u8, ref _assetStubToggle);
                    break;
                case ".prefab":
                    ImGui.Text("Importer  PrefabImporter"u8);
                    ImGui.TextDisabled("Root  Player"u8);
                    ImGui.TextDisabled("Components  Transform, Mesh Renderer, PlayerController"u8);
                    ImGui.TextDisabled("Static  false"u8);
                    break;
                case ".mat":
                    ImGui.Text("Importer  ShaderGraph / Standard"u8);
                    ImGui.TextDisabled("Shader  Universal Render Pipeline/Lit"u8);
                    ImGui.ColorEdit3("Albedo##Inspector.Asset.Material"u8, ref _assetStubColor);
                    ImGui.SliderFloat("Smoothness##Inspector.Asset.Material"u8, ref _assetStubSmoothness, 0f, 1f);
                    break;
                case ".png":
                    ImGui.Text("Importer  TextureImporter"u8);
                    ImGui.TextDisabled("Type  Default"u8);
                    ImGui.TextDisabled("Size  512 x 512"u8);
                    ImGui.TextDisabled("Format  RGBA 8 bit"u8);
                    ImGui.Checkbox("sRGB"u8, ref _assetStubSrgb);
                    ImGui.Checkbox("Generate Mip Maps"u8, ref _assetStubMips);
                    break;
                default:
                    ImGui.TextDisabled("No importer preview for this asset."u8);
                    break;
            }
        }

        ImGui.Spacing();
        ImGui.BeginDisabled();
        ImGui.Button("Open##Inspector.Asset"u8, new float2(-1f, 0f));
        ImGui.EndDisabled();
    }

    static string AssetTypeName(string extension) => extension.ToLowerInvariant() switch
    {
        ".cs" => "Mono Script",
        ".prefab" => "Prefab",
        ".mat" => "Material",
        ".png" => "Texture 2D",
        _ => "Asset",
    };

    static string AssetStubGuid(string path)
    {
        var hash = path.GetHashCode(StringComparison.Ordinal);
        return $"{(uint)hash:x8}-{(uint)~hash:x8}-stubguid";
    }

    void SelectGameObject(int id)
    {
        _selectedId = id;
        _selectedAsset = null;
    }

    void DrawConsole()
    {
        if (!ImGui.Begin("Console"u8))
        {
            ImGui.End();
            return;
        }

        if (ImGui.Button("Clear"u8))
            _console.Clear();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        ImGui.Combo("Filter"u8, ref _consoleFilter, "All\0Info\0Warning\0Error\0"u8);

        ImGui.Separator();
        if (ImGui.BeginChild("ConsoleLog"u8))
        {
            foreach (var line in _console)
            {
                if (_consoleFilter != 0 && (int)line.Severity != _consoleFilter)
                    continue;

                ImGui.TextColored(line.Color, $"[{line.Severity}] {line.Message}");
            }
        }

        ImGui.EndChild();
        ImGui.End();
    }

    void DrawProfiler(float fps)
    {
        if (!ImGui.Begin("Profiler"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.Text($"CPU  {fps:0.0} FPS   |   Play time {_playTime:0.00}s   |   last {_fpsCount} frames");
        if (ImPlot.BeginPlot("Frame rate"u8, new float2(-1f, -1f)))
        {
            var count = Math.Max(_fpsCount, 1);
            ImPlot.SetupAxisLimits(ImAxis.X1, 0, count - 1, ImPlotCond.Always);
            ImPlot.SetupAxis(ImAxis.Y1, "FPS"u8, ImPlotAxisFlags.AutoFit);
            var offset = _fpsCount == _fpsHistory.Length ? _fpsWrite : 0;
            ImPlot.PlotLine("FPS"u8, _fpsHistory.AsSpan(0, count), 1.0, 0.0, ImPlotLineFlags.None, offset);
            ImPlot.EndPlot();
        }

        ImGui.End();
    }

    void Log(ConsoleSeverity severity, string message)
        => _console.Add(new ConsoleLine(severity, message));

    static void ApplyUnityLikeTheme()
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.PopupRounding = 2f;
        style.FrameRounding = 2f;
        style.GrabRounding = 2f;
        style.TabRounding = 2f;
        style.ScrollbarRounding = 3f;
        style.WindowBorderSize = 1f;
        style.FramePadding = new float2(8f, 4f);
        style.ItemSpacing = new float2(8f, 6f);

        Set(style, ImGuiCol.WindowBg, 0.22f, 0.22f, 0.22f);
        Set(style, ImGuiCol.ChildBg, 0.22f, 0.22f, 0.22f);
        Set(style, ImGuiCol.PopupBg, 0.18f, 0.18f, 0.18f);
        Set(style, ImGuiCol.TitleBg, 0.16f, 0.16f, 0.16f);
        Set(style, ImGuiCol.TitleBgActive, 0.16f, 0.16f, 0.16f);
        Set(style, ImGuiCol.MenuBarBg, 0.2f, 0.2f, 0.2f);
        Set(style, ImGuiCol.Header, 0.28f, 0.28f, 0.28f);
        Set(style, ImGuiCol.HeaderHovered, 0.32f, 0.4f, 0.48f);
        Set(style, ImGuiCol.HeaderActive, 0.24f, 0.4f, 0.55f);
        Set(style, ImGuiCol.Tab, 0.18f, 0.18f, 0.18f);
        Set(style, ImGuiCol.TabSelected, 0.24f, 0.24f, 0.24f);
        Set(style, ImGuiCol.TabHovered, 0.3f, 0.38f, 0.46f);
        Set(style, ImGuiCol.Button, 0.28f, 0.28f, 0.28f);
        Set(style, ImGuiCol.ButtonHovered, 0.34f, 0.34f, 0.34f);
        Set(style, ImGuiCol.FrameBg, 0.16f, 0.16f, 0.16f);
        Set(style, ImGuiCol.Separator, 0.12f, 0.12f, 0.12f);
        Set(style, ImGuiCol.DockingEmptyBg, 0.17f, 0.17f, 0.17f);
    }

    static void Set(ImGuiStylePtr style, ImGuiCol col, float r, float g, float b)
        => style.Colors[(int)col] = new float4(r, g, b, 1f);

    readonly record struct ConsoleLine(ConsoleSeverity Severity, string Message)
    {
        public float4 Color => Severity switch
        {
            ConsoleSeverity.Error => new float4(1f, 0.45f, 0.4f, 1f),
            ConsoleSeverity.Warning => new float4(1f, 0.82f, 0.35f, 1f),
            _ => new float4(0.85f, 0.85f, 0.85f, 1f),
        };
    }

    enum ConsoleSeverity
    {
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    enum ToolbarIcon
    {
        Play,
        Pause,
        Step,
        Hand,
        Move,
        Rotate,
        Scale,
        Snap,
    }
}
