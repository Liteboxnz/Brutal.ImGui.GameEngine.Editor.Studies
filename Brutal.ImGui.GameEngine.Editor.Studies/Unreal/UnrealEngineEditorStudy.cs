using System.Runtime.InteropServices;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Internal;
using Brutal.ImGuizmoAPI;
using Brutal.ImPlotApi;
using Brutal.Numerics;

/// <summary>
/// ImGui shell that copies Unreal Editor's usual layout: menu, PIE toolbar,
/// Viewport, World Outliner, Details, Content Browser, Output Log.
/// Engine behaviour is stubbed so you can study the ImGui calls in isolation.
/// </summary>
sealed class UnrealEngineEditorStudy : IEngineEditorStudy
{
    static readonly string[] MobilityNames = ["Static", "Stationary", "Movable"];
    static readonly string[] ViewModes = ["Lit", "Unlit", "Wireframe", "Detail Lighting"];
    static readonly string[][] PlaceItems =
    [
        ["Cube", "Sphere", "Cylinder", "Plane", "Empty Actor"],
        ["Directional Light", "Point Light", "Spot Light", "Sky Light", "Rect Light"],
        ["Particle System", "Niagara System", "Exponential Height Fog"],
        ["Blocking Volume", "Trigger Box", "Nav Mesh Bounds Volume"],
        ["Camera Actor", "Player Start", "Static Mesh Actor"],
    ];

    readonly UnrealStubWorld _world = UnrealStubWorld.CreateThirdPersonMap();
    readonly List<OutputLine> _log = [];
    readonly float[] _fpsHistory = new float[180];
    readonly float[] _fpsAxis = new float[180];
    readonly List<float4x4> _cubeMatrices = [];

    int _selectedId;
    int _gizmoMode = 1; // 0 select, 1 translate, 2 rotate, 3 scale
    int _gizmoSpace; // 0 world, 1 local (Unreal defaults to world)
    int _viewMode;
    int _placeCategory;
    int _logFilter;
    bool _playing;
    bool _simulating;
    bool _paused;
    bool _dockLayoutBuilt;
    bool _useSnap = true;
    float _playTime;
    int _fpsWrite;
    readonly ImInputString _outlinerFilter = new(128);
    readonly ImInputString _contentFilter = new(128);
    string _selectedFolder = "Content/ThirdPerson/Maps";
    float _viewportFocusDistance = 18f;
    float3 _viewportTarget = new(0f, 1f, 0f);
    float4x4 _viewportView;

    static readonly (string Path, string[] Assets)[] ContentTree =
    [
        ("Content/ThirdPerson/Maps", ["ThirdPersonMap.umap"]),
        ("Content/ThirdPerson/Blueprints", ["BP_ThirdPersonCharacter", "BP_ThirdPersonGameMode", "BP_ThirdPersonPlayerController"]),
        ("Content/ThirdPerson/Meshes", ["SM_Cube", "SM_Sphere", "SKM_Manny"]),
        ("Content/ThirdPerson/Materials", ["M_Default", "M_Ground"]),
        ("Engine/Content/EngineMeshes", ["Cube", "Sphere", "Cylinder"]),
    ];

    public string WindowTitle => "Unreal Editor — ImGui Study";

    public void Setup()
    {
        ApplyUnrealLikeTheme();
        _viewportView = float4x4.CreateLookAt(new float3(8f, 6f, -10f), _viewportTarget, new float3(0f, 1f, 0f));
        var character = _world.FindByLabel("BP_ThirdPersonCharacter");
        if (character is not null)
            _selectedId = character.Id;

        Log(OutputSeverity.Log, "Unreal editor study ready. Viewport gizmos are ImGuizmo; PIE and assets are stubbed.");
        Log(OutputSeverity.Log, "Viewport: W/E/R tools, World/Local, grid snap. MMB or Select+LMB orbit, RMB pan, wheel zoom.");
        Log(OutputSeverity.Warning, "Play-in-Editor does not launch a game instance — it only advances stub animation.");
    }

    public void Draw(Action requestClose, Action returnToLauncher)
    {
        var io = ImGui.GetIO();
        var dt = io.DeltaTime;
        if (!ImGuizmo.IsUsing())
            TickPlayMode(dt);
        RecordFps(io.Framerate);

        ImGuizmo.BeginFrame();

        DrawHostWindow(requestClose, returnToLauncher);
        DrawPlaceActors();
        DrawViewport();
        DrawWorldOutliner();
        DrawDetails();
        DrawWorldSettings();
        DrawContentBrowser();
        DrawOutputLog();
        DrawStatUnit(io.Framerate);
    }

    void TickPlayMode(float dt)
    {
        if ((!_playing && !_simulating) || _paused)
            return;

        _playTime += dt;
        var character = _world.FindByLabel("BP_ThirdPersonCharacter");
        if (character is null)
            return;

        var t = _playTime;
        character.Location = new float3(MathF.Sin(t) * 180f, 96f, MathF.Cos(t) * 180f);
        character.Rotation = new float3(0f, t * 40f, 0f);
    }

    void RecordFps(float fps)
    {
        _fpsHistory[_fpsWrite] = fps;
        _fpsAxis[_fpsWrite] = _fpsWrite;
        _fpsWrite = (_fpsWrite + 1) % _fpsHistory.Length;
    }

    void DrawHostWindow(Action requestClose, Action returnToLauncher)
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

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(0f, 0f));
        ImGui.Begin("Unreal Editor Host"u8, hostFlags);
        ImGui.PopStyleVar(3);

        DrawMainMenu(requestClose, returnToLauncher);
        DrawToolbar();

        var dockspaceId = ImGui.GetID("UnrealDockSpace"u8);
        if (!_dockLayoutBuilt)
        {
            BuildUnrealDockLayout(dockspaceId, ImGui.GetContentRegionAvail());
            _dockLayoutBuilt = true;
        }

        ImGui.DockSpace(dockspaceId);
        ImGui.End();
    }

    void DrawMainMenu(Action requestClose, Action returnToLauncher)
    {
        if (!ImGui.BeginMenuBar())
            return;

        if (ImGui.BeginMenu("File"u8))
        {
            if (ImGui.MenuItem("New Level"u8, "Ctrl+N"u8))
                Log(OutputSeverity.Log, "File > New Level (stub)");
            if (ImGui.MenuItem("Open Level"u8, "Ctrl+O"u8))
                Log(OutputSeverity.Log, "File > Open Level (stub)");
            if (ImGui.MenuItem("Save Current"u8, "Ctrl+S"u8))
                Log(OutputSeverity.Log, $"Saved {_world.MapName}.umap (stub)");
            ImGui.Separator();
            if (ImGui.MenuItem("Package Project"u8))
                Log(OutputSeverity.Warning, "Packaging is not implemented.");
            ImGui.Separator();
            if (ImGui.MenuItem("Back to Study List"u8))
                returnToLauncher();
            if (ImGui.MenuItem("Exit"u8, "Alt+F4"u8))
                requestClose();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"u8))
        {
            ImGui.MenuItem("Undo"u8, "Ctrl+Z"u8, false, false);
            ImGui.MenuItem("Redo"u8, "Ctrl+Y"u8, false, false);
            ImGui.Separator();
            if (ImGui.MenuItem("Editor Preferences"u8))
                Log(OutputSeverity.Log, "Editor Preferences (stub)");
            if (ImGui.MenuItem("Project Settings"u8))
                Log(OutputSeverity.Log, "Project Settings (stub)");
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Window"u8))
        {
            ImGui.MenuItem("Place Actors"u8);
            ImGui.MenuItem("World Outliner"u8);
            ImGui.MenuItem("Details"u8);
            ImGui.MenuItem("Content Browser"u8);
            ImGui.MenuItem("Output Log"u8);
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Tools"u8))
        {
            if (ImGui.MenuItem("Rebuild Navigation"u8))
                Log(OutputSeverity.Log, "Recast navmesh rebuild (stub)");
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Build"u8))
        {
            if (ImGui.MenuItem("Build Lighting"u8))
                Log(OutputSeverity.Warning, "Lighting build is stubbed.");
            if (ImGui.MenuItem("Build All Levels"u8))
                Log(OutputSeverity.Log, "Build All Levels (stub)");
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Select"u8))
        {
            if (ImGui.MenuItem("Select All"u8, "Ctrl+A"u8))
                Log(OutputSeverity.Log, "Select All (stub)");
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Actor"u8))
        {
            if (ImGui.MenuItem("Add Empty Actor"u8))
                SpawnEmptyActor("EmptyActor", "Actor");
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"u8))
        {
            ImGui.TextDisabled("This is an ImGui study, not Unreal Engine."u8);
            ImGui.EndMenu();
        }

        ImGui.EndMenuBar();
    }

    void DrawToolbar()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(8f, 6f));
        ImGui.BeginChild("UnrealToolbar"u8, new float2(0f, ImGui.GetFrameHeightWithSpacing() + 8f), ImGuiChildFlags.Borders);
        ImGui.PopStyleVar();

        ImGui.RadioButton("Select"u8, ref _gizmoMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton("Translate"u8, ref _gizmoMode, 1);
        ImGui.SameLine();
        ImGui.RadioButton("Rotate"u8, ref _gizmoMode, 2);
        ImGui.SameLine();
        ImGui.RadioButton("Scale"u8, ref _gizmoMode, 3);
        ImGui.SameLine();
        ImGui.TextDisabled("|"u8);
        ImGui.SameLine();
        ImGui.RadioButton("World"u8, ref _gizmoSpace, 0);
        ImGui.SameLine();
        ImGui.RadioButton("Local"u8, ref _gizmoSpace, 1);
        ImGui.SameLine();
        ImGui.Checkbox("Grid Snap"u8, ref _useSnap);

        var playWidth = 280f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX() + 16f, (ImGui.GetWindowWidth() - playWidth) * 0.5f));

        PushAccentButton(_playing);
        if (ImGui.Button(_playing ? "Stop"u8 : "Play"u8))
            TogglePlay();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        PushAccentButton(_simulating && !_playing);
        if (ImGui.Button(_simulating && !_playing ? "End Sim"u8 : "Simulate"u8))
            ToggleSimulate();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.BeginDisabled(!_playing && !_simulating);
        if (ImGui.Button(_paused ? "Resume"u8 : "Pause"u8))
        {
            _paused = !_paused;
            Log(OutputSeverity.Log, _paused ? "PIE paused (stub)" : "PIE resumed (stub)");
        }

        ImGui.SameLine();
        if (ImGui.Button("Eject"u8))
            Log(OutputSeverity.Log, "Eject from pawn (stub)");
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextDisabled($"  {_world.MapName}.umap{SessionMarker()}");

        ImGui.EndChild();
    }

    string SessionMarker()
    {
        if (_playing)
            return "  [PIE]";
        if (_simulating)
            return "  [Simulate]";
        return "";
    }

    void PushAccentButton(bool active)
    {
        var color = active
            ? new float4(0.12f, 0.42f, 0.78f, 1f)
            : new float4(0.22f, 0.24f, 0.27f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Button, color);
    }

    void TogglePlay()
    {
        if (_playing)
        {
            _playing = false;
            _simulating = false;
            _paused = false;
            _playTime = 0f;
            Log(OutputSeverity.Log, "Stopped Play-in-Editor (stub)");
            return;
        }

        _playing = true;
        _simulating = false;
        _paused = false;
        Log(OutputSeverity.Log, "Play-in-Editor (stub)");
    }

    void ToggleSimulate()
    {
        if (_simulating && !_playing)
        {
            _simulating = false;
            _paused = false;
            _playTime = 0f;
            Log(OutputSeverity.Log, "Ended Simulate (stub)");
            return;
        }

        _playing = false;
        _simulating = true;
        _paused = false;
        Log(OutputSeverity.Log, "Simulate (stub)");
    }

    void SpawnEmptyActor(string label, string className)
    {
        var actor = new UnrealStubActor(label) { ClassName = className };
        _world.RootActors.Add(actor);
        _selectedId = actor.Id;
        Log(OutputSeverity.Log, $"Spawned {className} (stub)");
    }

    static void BuildUnrealDockLayout(ImGuiID dockspaceId, float2 size)
    {
        ImGui.Internal.DockBuilderRemoveNode(dockspaceId);
        ImGui.Internal.DockBuilderAddNode(dockspaceId, (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.DockSpace);
        ImGui.Internal.DockBuilderSetNodeSize(dockspaceId, size);

        var dockMain = dockspaceId;
        ImGui.Internal.DockBuilderSplitNode(dockMain, ImGuiDir.Left, 0.16f, out var dockLeft, out dockMain);
        ImGui.Internal.DockBuilderSplitNode(dockMain, ImGuiDir.Right, 0.28f, out var dockRight, out dockMain);
        ImGui.Internal.DockBuilderSplitNode(dockMain, ImGuiDir.Down, 0.30f, out var dockBottom, out dockMain);
        ImGui.Internal.DockBuilderSplitNode(dockRight, ImGuiDir.Down, 0.58f, out var dockDetails, out var dockOutliner);
        ImGui.Internal.DockBuilderSplitNode(dockBottom, ImGuiDir.Right, 0.38f, out var dockLog, out var dockContent);

        ImGui.Internal.DockBuilderDockWindow("Place Actors"u8, dockLeft);
        ImGui.Internal.DockBuilderDockWindow("Viewport"u8, dockMain);
        ImGui.Internal.DockBuilderDockWindow("Stat Unit"u8, dockMain);
        ImGui.Internal.DockBuilderDockWindow("World Outliner"u8, dockOutliner);
        ImGui.Internal.DockBuilderDockWindow("Details"u8, dockDetails);
        ImGui.Internal.DockBuilderDockWindow("World Settings"u8, dockDetails);
        ImGui.Internal.DockBuilderDockWindow("Content Browser"u8, dockContent);
        ImGui.Internal.DockBuilderDockWindow("Output Log"u8, dockLog);
        ImGui.Internal.DockBuilderFinish(dockspaceId);
    }

    void DrawPlaceActors()
    {
        if (!ImGui.Begin("Place Actors"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled("Modes  |  Place"u8);
        ImGui.SetNextItemWidth(-1f);
        ImGui.Combo("##PlaceCategory"u8, ref _placeCategory, "Basic\0Lights\0Visual Effects\0Volumes\0All Classes\0"u8);
        ImGui.Separator();

        var items = PlaceItems[Math.Clamp(_placeCategory, 0, PlaceItems.Length - 1)];
        foreach (var item in items)
        {
            if (ImGui.Selectable(item))
                SpawnEmptyActor(item.Replace(" ", ""), item);
        }

        ImGui.End();
    }

    void DrawViewport()
    {
        if (!ImGui.Begin("Viewport"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(140f);
        ImGui.Combo("##ViewMode"u8, ref _viewMode, "Lit\0Unlit\0Wireframe\0Detail Lighting\0"u8);
        ImGui.SameLine();
        ImGui.TextDisabled($"Perspective  |  {GizmoName(_gizmoMode)}  |  {(_gizmoSpace == 0 ? "World" : "Local")}  |  ImGuizmo");

        Draw3DViewport(new float4(0.08f, 0.09f, 0.1f, 1f));
        ImGui.End();
    }

    void Draw3DViewport(float4 background)
    {
        var avail = ImGui.GetContentRegionAvail();
        if (avail.X < 32f || avail.Y < 32f)
            return;

        var origin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        var max = new float2(origin.X + avail.X, origin.Y + avail.Y);
        draw.AddRectFilled(origin, max, ImGui.GetColorU32(background));

        var camera = _world.FindByLabel("CameraActor")?.Components.OfType<UnrealStubCameraComponent>().FirstOrDefault();
        var projection = BuildProjection(avail.X / MathF.Max(avail.Y, 1f), camera);

        ImGuizmo.SetDrawlist(draw);
        ImGuizmo.SetRect(origin.X, origin.Y, avail.X, avail.Y);
        ImGuizmo.SetOrthographic(camera?.Orthographic ?? false);
        ImGuizmo.DrawGrid(_viewportView, projection, float4x4.Identity, 20f);

        CollectCubeMatrices();
        if (_cubeMatrices.Count > 0)
            ImGuizmo.DrawCubes(_viewportView, projection, CollectionsMarshal.AsSpan(_cubeMatrices));

        ManipulateSelected(projection);
        HandleViewportNavigation();
        var cubeSize = new float2(96f, 96f);
        var cubePos = new float2(max.X - cubeSize.X - 8f, origin.Y + 8f);
        ImGuizmo.ViewManipulate(ref _viewportView, _viewportFocusDistance, cubePos, cubeSize, 0x10101010u);
        SyncTargetFromView();

        var overlay = _playing ? "PIE Viewport (stub)" : (_simulating ? "Simulate (stub)" : $"Perspective — {ViewModes[_viewMode]}");
        draw.AddText(
            new float2(origin.X + 10f, origin.Y + 8f),
            ImGui.GetColorU32(new float4(1f, 1f, 1f, 0.65f)),
            overlay);
    }

    void ManipulateSelected(float4x4 projection)
    {
        if (_gizmoMode == 0)
            return;

        var selected = _world.Find(_selectedId);
        if (selected is null)
            return;

        var operation = _gizmoMode switch
        {
            2 => ImGuizmoOperation.Rotate,
            3 => ImGuizmoOperation.Scale,
            _ => ImGuizmoOperation.Translate,
        };
        var mode = _gizmoSpace == 0 ? ImGuizmoMode.World : ImGuizmoMode.Local;
        float3? snap = !_useSnap
            ? null
            : _gizmoMode switch
            {
                2 => new float3(10f, 10f, 10f),
                3 => new float3(0.1f, 0.1f, 0.1f),
                _ => new float3(0.5f, 0.5f, 0.5f),
            };

        // Unreal stores location in centimetres; the gizmo world is Unity-scale metres.
        var displayLocation = selected.Location / 100f;
        var matrix = ImGuizmo.RecomposeMatrixFromComponents(displayLocation, selected.Rotation, selected.Scale);
        ImGuizmo.PushId(selected.Id);
        if (ImGuizmo.Manipulate(_viewportView, projection, operation, mode, ref matrix, snap))
        {
            ImGuizmo.DecomposeMatrixToComponents(matrix, out var translation, out var rotation, out var scale);
            selected.Location = translation * 100f;
            selected.Rotation = rotation;
            selected.Scale = scale;
        }

        ImGuizmo.PopId();
    }

    void HandleViewportNavigation()
    {
        if (!ImGui.IsWindowHovered() || ImGuizmo.IsUsing() || ImGuizmo.IsUsingViewManipulate() || ImGuizmo.IsViewManipulateHovered())
            return;

        var io = ImGui.GetIO();
        var orbit = ImGui.IsMouseDragging(ImGuiMouseButton.Middle)
                    || (_gizmoMode == 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left));
        if (orbit)
            OrbitViewport(io.MouseDelta);
        else if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            PanViewport(io.MouseDelta);

        if (io.MouseWheel != 0f)
        {
            _viewportFocusDistance = Math.Clamp(_viewportFocusDistance * (1f - io.MouseWheel * 0.1f), 1.5f, 80f);
            RebuildViewport();
        }
    }

    void OrbitViewport(float2 mouseDelta)
    {
        var eye = ViewportCameraPosition();
        var offset = eye - _viewportTarget;
        var distance = MathF.Max(0.5f, offset.Length());
        var yaw = MathF.Atan2(offset.X, offset.Z);
        var pitch = MathF.Asin(Math.Clamp(offset.Y / distance, -1f, 1f));
        yaw += mouseDelta.X * 0.005f;
        pitch = Math.Clamp(pitch + mouseDelta.Y * 0.005f, -1.4f, 1.4f);
        var direction = new float3(
            MathF.Sin(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Cos(yaw) * MathF.Cos(pitch));
        _viewportFocusDistance = distance;
        _viewportView = float4x4.CreateLookAt(_viewportTarget + direction * distance, _viewportTarget, new float3(0f, 1f, 0f));
    }

    void PanViewport(float2 mouseDelta)
    {
        if (!float4x4.Invert(_viewportView, out var inverse))
            return;

        var right = new float3(inverse.X.X, inverse.X.Y, inverse.X.Z);
        var up = new float3(inverse.Y.X, inverse.Y.Y, inverse.Y.Z);
        var speed = _viewportFocusDistance * 0.0015f;
        _viewportTarget -= right * (mouseDelta.X * speed) + up * (mouseDelta.Y * speed);
        RebuildViewport();
    }

    void RebuildViewport()
    {
        var eye = ViewportCameraPosition();
        var offset = eye - _viewportTarget;
        if (offset.LengthSquared() < 0.0001f)
            offset = new float3(0f, 0.3f, -1f);
        var direction = float3.Normalize(offset);
        _viewportView = float4x4.CreateLookAt(_viewportTarget + direction * _viewportFocusDistance, _viewportTarget, new float3(0f, 1f, 0f));
    }

    void SyncTargetFromView()
    {
        if (!float4x4.Invert(_viewportView, out var inverse))
            return;

        var eye = inverse.Translation;
        var forward = float3.Normalize(new float3(-inverse.Z.X, -inverse.Z.Y, -inverse.Z.Z));
        _viewportTarget = eye + forward * _viewportFocusDistance;
    }

    float3 ViewportCameraPosition()
    {
        float4x4.Invert(_viewportView, out var inverse);
        return inverse.Translation;
    }

    static float4x4 BuildProjection(float aspect, UnrealStubCameraComponent? camera)
    {
        var near = 0.3f;
        var far = camera?.FarClip / 100f ?? 1000f;
        if (camera?.Orthographic == true)
            return float4x4.CreateOrthographic(10f * aspect, 10f, near, far);

        var fov = (camera?.FieldOfView ?? 90f) * (MathF.PI / 180f);
        return float4x4.CreatePerspectiveFieldOfView(fov, aspect, near, far);
    }

    void CollectCubeMatrices()
    {
        _cubeMatrices.Clear();
        foreach (var actor in _world.AllActors())
        {
            if (!actor.Visible)
                continue;
            if (!actor.Components.OfType<UnrealStubStaticMeshComponent>().Any())
                continue;

            var location = actor.Location / 100f;
            _cubeMatrices.Add(ImGuizmo.RecomposeMatrixFromComponents(location, actor.Rotation, actor.Scale));
        }
    }

    static string GizmoName(int mode) => mode switch
    {
        1 => "Translate",
        2 => "Rotate",
        3 => "Scale",
        _ => "Select",
    };

    void DrawWorldOutliner()
    {
        if (!ImGui.Begin("World Outliner"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##OutlinerSearch"u8, "Search Outliner"u8, _outlinerFilter);
        ImGui.TextDisabled($"{_world.MapName}  |  {_world.RootActors.Count} actors");
        ImGui.Separator();

        foreach (var folder in _world.Folders)
        {
            if (!ImGui.TreeNodeEx($"{folder.Name}##folder{folder.Name}", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
                continue;

            foreach (var actor in _world.RootActors)
            {
                if (actor.Folder == folder)
                    DrawOutlinerActor(actor);
            }

            ImGui.TreePop();
        }

        var unfoldered = false;
        foreach (var actor in _world.RootActors)
        {
            if (actor.Folder is not null)
                continue;
            if (!unfoldered)
            {
                ImGui.Separator();
                unfoldered = true;
            }

            DrawOutlinerActor(actor);
        }

        ImGui.End();
    }

    void DrawOutlinerActor(UnrealStubActor actor)
    {
        var filter = _outlinerFilter.Value;
        if (filter.Length > 0 &&
            actor.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
            actor.ClassName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            return;

        var flags = ImGuiTreeNodeFlags.OpenOnArrow
                    | ImGuiTreeNodeFlags.SpanAvailWidth
                    | ImGuiTreeNodeFlags.DrawLinesToNodes;
        if (actor.Id == _selectedId)
            flags |= ImGuiTreeNodeFlags.Selected;
        if (actor.Children.Count == 0)
            flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

        ImGui.PushStyleColor(ImGuiCol.Text, actor.Visible
            ? new float4(0.88f, 0.9f, 0.92f, 1f)
            : new float4(0.5f, 0.52f, 0.55f, 1f));

        var opened = ImGui.TreeNodeEx($"{actor.Label}##{actor.Id}", flags);
        ImGui.PopStyleColor();

        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            _selectedId = actor.Id;

        if (ImGui.BeginPopupContextItem())
        {
            if (ImGui.MenuItem("Rename"u8))
                Log(OutputSeverity.Log, $"Rename {actor.Label} (stub)");
            if (ImGui.MenuItem(actor.Visible ? "Hide"u8 : "Show"u8))
                actor.Visible = !actor.Visible;
            if (ImGui.MenuItem("Delete"u8))
                Log(OutputSeverity.Warning, "Delete is stubbed.");
            ImGui.EndPopup();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(actor.ClassName);

        if (!opened || actor.Children.Count == 0)
            return;

        foreach (var child in actor.Children)
            DrawOutlinerActor(child);

        ImGui.TreePop();
    }

    void DrawDetails()
    {
        if (!ImGui.Begin("Details"u8))
        {
            ImGui.End();
            return;
        }

        var selected = _world.Find(_selectedId);
        if (selected is null)
        {
            ImGui.TextDisabled("Select an Actor in the World Outliner."u8);
            ImGui.End();
            return;
        }

        ImGui.Text(selected.Label);
        ImGui.SameLine();
        ImGui.TextDisabled($"  {selected.ClassName}");

        var visible = selected.Visible;
        if (ImGui.Checkbox("Actor Hidden in Game"u8, ref visible))
            selected.Visible = visible;

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Transform"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            var location = selected.Location;
            var rotation = selected.Rotation;
            var scale = selected.Scale;
            var mobility = (int)selected.Mobility;
            ImGui.PushItemWidth(-1f);
            if (ImGui.DragFloat3("Location"u8, ref location, 1f))
                selected.Location = location;
            if (ImGui.DragFloat3("Rotation"u8, ref rotation, 0.5f))
                selected.Rotation = rotation;
            if (ImGui.DragFloat3("Scale"u8, ref scale, 0.05f))
                selected.Scale = scale;
            ImGui.PopItemWidth();
            if (DrawCombo("Mobility"u8, ref mobility, MobilityNames))
                selected.Mobility = (UnrealMobility)mobility;
        }

        foreach (var component in selected.Components)
            DrawComponent(component);

        ImGui.Spacing();
        if (ImGui.Button("Add Component"u8, new float2(-1f, 0f)))
            Log(OutputSeverity.Warning, "Add Component menu is stubbed.");

        ImGui.End();
    }

    static void DrawComponent(UnrealStubComponent component)
    {
        var flags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowOverlap;
        if (!ImGui.CollapsingHeader($"{component.TypeName}", flags))
            return;

        var enabled = component.Enabled;
        ImGui.Checkbox("Component Tick Enabled"u8, ref enabled);
        component.Enabled = enabled;

        switch (component)
        {
            case UnrealStubCameraComponent camera:
                var fov = camera.FieldOfView;
                var near = camera.NearClip;
                var far = camera.FarClip;
                var ortho = camera.Orthographic;
                ImGui.SliderFloat("Field Of View"u8, ref fov, 5f, 170f);
                ImGui.DragFloat("Near Clip Plane"u8, ref near, 1f, 1f, 100f);
                ImGui.DragFloat("Far Clip Plane"u8, ref far, 100f, 100f, 100000f);
                ImGui.Checkbox("Constrain Aspect Ratio"u8, ref ortho);
                camera.FieldOfView = fov;
                camera.NearClip = near;
                camera.FarClip = far;
                camera.Orthographic = ortho;
                break;

            case UnrealStubLightComponent light:
                var intensity = light.Intensity;
                var color = light.Color;
                var mobility = (int)light.Mobility;
                ImGui.DragFloat("Intensity"u8, ref intensity, 0.05f, 0f, 20f);
                ImGui.ColorEdit3("Light Color"u8, ref color);
                DrawCombo("Mobility"u8, ref mobility, MobilityNames);
                light.Intensity = intensity;
                light.Color = color;
                light.Mobility = (UnrealMobility)mobility;
                break;

            case UnrealStubStaticMeshComponent mesh:
                var meshColor = mesh.Color;
                ImGui.TextDisabled($"Static Mesh  {mesh.MeshName}");
                ImGui.ColorEdit3("Tint"u8, ref meshColor);
                mesh.Color = meshColor;
                break;

            case UnrealStubMovementComponent movement:
                var walk = movement.MaxWalkSpeed;
                var jump = movement.JumpZVelocity;
                ImGui.DragFloat("Max Walk Speed"u8, ref walk, 1f, 0f, 2000f);
                ImGui.DragFloat("Jump Z Velocity"u8, ref jump, 1f, 0f, 2000f);
                movement.MaxWalkSpeed = walk;
                movement.JumpZVelocity = jump;
                break;
        }
    }

    void DrawWorldSettings()
    {
        if (!ImGui.Begin("World Settings"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled(_world.MapName);
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Game Mode"u8, ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Text("GameMode Override"u8);
            ImGui.SameLine();
            ImGui.TextDisabled("BP_ThirdPersonGameMode"u8);
            ImGui.Text("Default Pawn Class"u8);
            ImGui.SameLine();
            ImGui.TextDisabled("BP_ThirdPersonCharacter"u8);
        }

        if (ImGui.CollapsingHeader("Lightmass"u8, ImGuiTreeNodeFlags.DefaultOpen))
            ImGui.TextDisabled("Static lighting quality settings are stubbed."u8);

        ImGui.End();
    }

    void DrawContentBrowser()
    {
        if (!ImGui.Begin("Content Browser"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(240f);
        ImGui.InputTextWithHint("##ContentSearch"u8, "Search Assets"u8, _contentFilter);
        ImGui.SameLine();
        ImGui.TextDisabled("  /Game"u8);
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        if (ImGui.BeginChild("ContentFolders"u8, new float2(MathF.Min(240f, avail.X * 0.35f), 0f), ImGuiChildFlags.Borders))
        {
            foreach (var (path, _) in ContentTree)
            {
                if (ImGui.Selectable(path, _selectedFolder == path))
                    _selectedFolder = path;
            }
        }

        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("ContentAssets"u8, new float2(0f, 0f), ImGuiChildFlags.Borders))
        {
            var folder = ContentTree.FirstOrDefault(entry => entry.Path == _selectedFolder);
            if (folder.Assets is not null)
            {
                foreach (var asset in folder.Assets)
                {
                    var filter = _contentFilter.Value;
                    if (filter.Length > 0 &&
                        asset.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    ImGui.Selectable(asset);
                }
            }
        }

        ImGui.EndChild();
        ImGui.End();
    }

    void DrawOutputLog()
    {
        if (!ImGui.Begin("Output Log"u8))
        {
            ImGui.End();
            return;
        }

        if (ImGui.Button("Clear"u8))
            _log.Clear();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140f);
        ImGui.Combo("Filter"u8, ref _logFilter, "All\0Log\0Warning\0Error\0"u8);

        ImGui.Separator();
        if (ImGui.BeginChild("OutputLogLines"u8))
        {
            foreach (var line in _log)
            {
                if (_logFilter != 0 && (int)line.Severity != _logFilter)
                    continue;

                ImGui.TextColored(line.Color, $"LogUnrealEditor: {line.Severity}: {line.Message}");
            }
        }

        ImGui.EndChild();
        ImGui.End();
    }

    void DrawStatUnit(float fps)
    {
        if (!ImGui.Begin("Stat Unit"u8))
        {
            ImGui.End();
            return;
        }

        ImGui.Text($"Frame {1000f / MathF.Max(fps, 1f):0.0} ms   |   Game {_playTime:0.00}s   |   GPU stubbed");
        if (ImPlot.BeginPlot("Frame time"u8, new float2(-1f, -1f)))
        {
            ImPlot.PlotLine("FPS"u8, _fpsAxis, _fpsHistory);
            ImPlot.EndPlot();
        }

        ImGui.End();
    }

    void Log(OutputSeverity severity, string message)
        => _log.Add(new OutputLine(severity, message));

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

    static void ApplyUnrealLikeTheme()
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.WindowRounding = 0f;
        style.ChildRounding = 2f;
        style.PopupRounding = 2f;
        style.FrameRounding = 2f;
        style.GrabRounding = 2f;
        style.TabRounding = 2f;
        style.ScrollbarRounding = 3f;
        style.WindowBorderSize = 1f;
        style.FramePadding = new float2(8f, 4f);
        style.ItemSpacing = new float2(8f, 6f);

        Set(style, ImGuiCol.WindowBg, 0.13f, 0.14f, 0.16f);
        Set(style, ImGuiCol.ChildBg, 0.13f, 0.14f, 0.16f);
        Set(style, ImGuiCol.PopupBg, 0.1f, 0.11f, 0.13f);
        Set(style, ImGuiCol.TitleBg, 0.08f, 0.09f, 0.1f);
        Set(style, ImGuiCol.TitleBgActive, 0.1f, 0.18f, 0.28f);
        Set(style, ImGuiCol.MenuBarBg, 0.1f, 0.11f, 0.13f);
        Set(style, ImGuiCol.Header, 0.16f, 0.22f, 0.3f);
        Set(style, ImGuiCol.HeaderHovered, 0.18f, 0.36f, 0.55f);
        Set(style, ImGuiCol.HeaderActive, 0.12f, 0.42f, 0.78f);
        Set(style, ImGuiCol.Tab, 0.1f, 0.11f, 0.13f);
        Set(style, ImGuiCol.TabSelected, 0.16f, 0.22f, 0.3f);
        Set(style, ImGuiCol.TabHovered, 0.18f, 0.36f, 0.55f);
        Set(style, ImGuiCol.Button, 0.22f, 0.24f, 0.27f);
        Set(style, ImGuiCol.ButtonHovered, 0.18f, 0.36f, 0.55f);
        Set(style, ImGuiCol.FrameBg, 0.08f, 0.09f, 0.1f);
        Set(style, ImGuiCol.Separator, 0.06f, 0.07f, 0.08f);
        Set(style, ImGuiCol.DockingEmptyBg, 0.08f, 0.09f, 0.1f);
        Set(style, ImGuiCol.CheckMark, 0.35f, 0.7f, 1f);
        Set(style, ImGuiCol.SliderGrab, 0.2f, 0.55f, 0.95f);
        Set(style, ImGuiCol.SliderGrabActive, 0.35f, 0.7f, 1f);
    }

    static void Set(ImGuiStylePtr style, ImGuiCol col, float r, float g, float b)
        => style.Colors[(int)col] = new float4(r, g, b, 1f);

    readonly record struct OutputLine(OutputSeverity Severity, string Message)
    {
        public float4 Color => Severity switch
        {
            OutputSeverity.Error => new float4(1f, 0.35f, 0.32f, 1f),
            OutputSeverity.Warning => new float4(1f, 0.78f, 0.28f, 1f),
            _ => new float4(0.55f, 0.82f, 1f, 1f),
        };
    }

    enum OutputSeverity
    {
        Log = 1,
        Warning = 2,
        Error = 3,
    }
}
