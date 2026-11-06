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
    static readonly string[] Tags = ["Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController"];
    static readonly string[] Layers = ["Default", "TransparentFX", "Ignore Raycast", "Water", "UI"];
    static readonly string[] LightTypes = ["Directional", "Point", "Spot"];
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

    public string WindowTitle => "Unity Editor — ImGui Study";

    public void Setup()
    {
        ApplyUnityLikeTheme();
        _sceneView = float4x4.CreateLookAt(new float3(7f, 5.5f, -8f), _sceneTarget, new float3(0f, 1f, 0f));
        _gizmoMode = 1;
        var player = FindByName("Player");
        if (player is not null)
            _selectedId = player.Id;

        Log(ConsoleSeverity.Info, "Unity editor study ready. Scene gizmos are ImGuizmo; the rest is stubbed.");
        Log(ConsoleSeverity.Info, "Scene: W/E/R tools, Local/World, snap. MMB or Hand+LMB orbit, RMB pan, wheel zoom.");
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
        ImGui.RadioButton("Local"u8, ref _gizmoSpace, 0);
        ImGui.SameLine();
        ImGui.RadioButton("World"u8, ref _gizmoSpace, 1);
        ImGui.SameLine();
        ImGui.Checkbox("Snap"u8, ref _useSnap);

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
        _selectedId = created.Id;
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
            _selectedId = node.Id;

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

        ImGui.TextDisabled($"Scene  |  {GizmoName(_gizmoMode)}  |  {(_gizmoSpace == 0 ? "Local" : "World")}  |  ImGuizmo");
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

        var camera = FindByName("Main Camera")?.Components.OfType<StubCameraComponent>().FirstOrDefault();
        var aspect = avail.X / MathF.Max(avail.Y, 1f);
        var sceneOrtho = sceneTools && _scene2D;
        var projection = sceneOrtho
            ? float4x4.CreateOrthographic(_sceneFocusDistance * aspect, _sceneFocusDistance, camera?.NearClip ?? 0.3f, camera?.FarClip ?? 1000f)
            : BuildProjection(aspect, camera);

        ImGuizmo.SetDrawlist(draw);
        ImGuizmo.SetRect(origin.X, origin.Y, avail.X, avail.Y);
        ImGuizmo.SetOrthographic(sceneOrtho || (!sceneTools && (camera?.Orthographic ?? false)));
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

        var overlayHovered = false;
        if (sceneTools)
        {
            ManipulateSelected(view, projection);
            overlayHovered = DrawSceneViewModeToggle(origin);
            if (!overlayHovered)
                HandleSceneNavigation();
            if (!_scene2D)
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

        var matrix = ImGuizmo.RecomposeMatrixFromComponents(selected.Position, selected.Rotation, selected.Scale);
        ImGuizmo.PushId(selected.Id);
        if (ImGuizmo.Manipulate(view, projection, operation, mode, ref matrix, snap))
        {
            ImGuizmo.DecomposeMatrixToComponents(matrix, out var translation, out var rotation, out var scale);
            selected.Position = translation;
            selected.Rotation = rotation;
            selected.Scale = scale;
        }

        ImGuizmo.PopId();
    }

    void HandleSceneNavigation()
    {
        if (!ImGui.IsWindowHovered() || ImGuizmo.IsUsing() || ImGuizmo.IsUsingViewManipulate() || ImGuizmo.IsViewManipulateHovered())
            return;

        var io = ImGui.GetIO();
        var pan = ImGui.IsMouseDragging(ImGuiMouseButton.Right)
                  || (_scene2D && ImGui.IsMouseDragging(ImGuiMouseButton.Middle))
                  || (_gizmoMode == 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left));
        var orbit = !_scene2D && ImGui.IsMouseDragging(ImGuiMouseButton.Middle);
        if (orbit)
            OrbitSceneCamera(io.MouseDelta);
        else if (pan)
            PanSceneCamera(io.MouseDelta);

        if (io.MouseWheel != 0f)
        {
            _sceneFocusDistance = Math.Clamp(_sceneFocusDistance * (1f - io.MouseWheel * 0.1f), 1.5f, 80f);
            RebuildSceneView();
        }
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

        var yaw = cameraObject.Rotation.Y * (MathF.PI / 180f);
        var pitch = cameraObject.Rotation.X * (MathF.PI / 180f);
        var forward = new float3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            -MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch));
        if (forward.LengthSquared() < 0.0001f)
            forward = new float3(0f, 0f, 1f);
        return float4x4.CreateLookAt(cameraObject.Position, cameraObject.Position + float3.Normalize(forward), new float3(0f, 1f, 0f));
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

    void CollectCubeMatrices()
    {
        _cubeMatrices.Clear();
        foreach (var root in _scene.Roots)
            CollectCubeMatrices(root);
    }

    void CollectCubeMatrices(StubGameObject node)
    {
        if (node.Active && node.Components.OfType<StubMeshRendererComponent>().Any())
            _cubeMatrices.Add(ImGuizmo.RecomposeMatrixFromComponents(node.Position, node.Rotation, node.Scale));

        foreach (var child in node.Children)
            CollectCubeMatrices(child);
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

        var selected = _scene.Find(_selectedId);
        if (selected is null)
        {
            ImGui.TextDisabled("Select a GameObject in the Hierarchy."u8);
            ImGui.End();
            return;
        }

        var active = selected.Active;
        if (ImGui.Checkbox("##active"u8, ref active))
            selected.Active = active;
        ImGui.SameLine();
        ImGui.Text(selected.Name);
        ImGui.SameLine();
        ImGui.TextDisabled($"  Instance ID {selected.Id}");

        var tagIndex = Math.Max(0, Array.IndexOf(Tags, selected.Tag));
        var layerIndex = Math.Max(0, Array.IndexOf(Layers, selected.Layer));
        if (DrawCombo("Tag"u8, ref tagIndex, Tags))
            selected.Tag = Tags[tagIndex];
        ImGui.SameLine();
        if (DrawCombo("Layer"u8, ref layerIndex, Layers))
            selected.Layer = Layers[layerIndex];

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Transform"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            var position = selected.Position;
            var rotation = selected.Rotation;
            var scale = selected.Scale;
            ImGui.PushItemWidth(-1f);
            if (ImGui.DragFloat3("Position"u8, ref position, 0.05f))
                selected.Position = position;
            if (ImGui.DragFloat3("Rotation"u8, ref rotation, 0.5f))
                selected.Rotation = rotation;
            if (ImGui.DragFloat3("Scale"u8, ref scale, 0.05f))
                selected.Scale = scale;
            ImGui.PopItemWidth();
        }

        foreach (var component in selected.Components)
            DrawComponent(component);

        ImGui.Spacing();
        if (ImGui.Button("Add Component"u8, new float2(-1f, 0f)))
            Log(ConsoleSeverity.Warning, "Add Component menu is stubbed.");

        ImGui.End();
    }

    static void DrawComponent(StubComponent component)
    {
        var enabled = component.Enabled;
        var flags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap;
        if (!ImGui.CollapsingHeader($"{component.TypeName}", flags))
            return;

        ImGui.Checkbox("Enabled"u8, ref enabled);
        component.Enabled = enabled;

        switch (component)
        {
            case StubCameraComponent camera:
                var fov = camera.FieldOfView;
                var near = camera.NearClip;
                var far = camera.FarClip;
                var ortho = camera.Orthographic;
                ImGui.SliderFloat("Field of View"u8, ref fov, 1f, 179f);
                ImGui.DragFloat("Near"u8, ref near, 0.01f, 0.01f, 10f);
                ImGui.DragFloat("Far"u8, ref far, 1f, 1f, 5000f);
                ImGui.Checkbox("Orthographic"u8, ref ortho);
                camera.FieldOfView = fov;
                camera.NearClip = near;
                camera.FarClip = far;
                camera.Orthographic = ortho;
                break;

            case StubLightComponent light:
                var type = light.TypeIndex;
                var intensity = light.Intensity;
                var color = light.Color;
                DrawCombo("Type"u8, ref type, LightTypes);
                ImGui.DragFloat("Intensity"u8, ref intensity, 0.02f, 0f, 8f);
                ImGui.ColorEdit3("Color"u8, ref color);
                light.TypeIndex = type;
                light.Intensity = intensity;
                light.Color = color;
                break;

            case StubMeshRendererComponent renderer:
                var meshColor = renderer.Color;
                var shadows = renderer.CastShadows;
                ImGui.ColorEdit3("Albedo"u8, ref meshColor);
                ImGui.Checkbox("Cast Shadows"u8, ref shadows);
                renderer.Color = meshColor;
                renderer.CastShadows = shadows;
                break;

            case StubMonoBehaviourComponent script:
                var speed = script.MoveSpeed;
                var jump = script.JumpHeight;
                ImGui.DragFloat("Move Speed"u8, ref speed, 0.05f, 0f, 30f);
                ImGui.DragFloat("Jump Height"u8, ref jump, 0.05f, 0f, 10f);
                script.MoveSpeed = speed;
                script.JumpHeight = jump;
                break;
        }
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

        DrawAssetFolder("Scenes"u8, ["SampleScene.unity"]);
        DrawAssetFolder("Scripts"u8, ["PlayerController.cs", "GameManager.cs"]);
        DrawAssetFolder("Prefabs"u8, ["Player.prefab"]);
        DrawAssetFolder("Materials"u8, ["Lit.mat", "Ground.mat"]);
        DrawAssetFolder("Textures"u8, ["Default.png"]);

        ImGui.End();
    }

    static void DrawAssetFolder(ImString name, string[] assets)
    {
        if (!ImGui.TreeNode(name))
            return;

        foreach (var asset in assets)
            ImGui.Selectable(asset);

        ImGui.TreePop();
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
    }
}
