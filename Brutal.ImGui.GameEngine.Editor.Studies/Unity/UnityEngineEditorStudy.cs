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
    readonly float[] _fpsAxis = new float[180];
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
    float _sceneFocusDistance = 12f;
    float3 _sceneTarget = new(0f, 0.5f, 0f);
    float4x4 _sceneView;

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
        _fpsAxis[_fpsWrite] = _fpsWrite;
        _fpsWrite = (_fpsWrite + 1) % _fpsHistory.Length;
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

        // Left: transform gizmos (Unity's Q/W/E/R strip).
        ImGui.RadioButton("Hand"u8, ref _gizmoMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton("Move"u8, ref _gizmoMode, 1);
        ImGui.SameLine();
        ImGui.RadioButton("Rotate"u8, ref _gizmoMode, 2);
        ImGui.SameLine();
        ImGui.RadioButton("Scale"u8, ref _gizmoMode, 3);
        ImGui.SameLine();
        ImGui.TextDisabled("|"u8);
        ImGui.SameLine();
        ImGui.RadioButton("Local"u8, ref _gizmoSpace, 0);
        ImGui.SameLine();
        ImGui.RadioButton("World"u8, ref _gizmoSpace, 1);
        ImGui.SameLine();
        ImGui.Checkbox("Snap"u8, ref _useSnap);

        // Center: play / pause / step.
        var playWidth = 220f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX() + 16f, (ImGui.GetWindowWidth() - playWidth) * 0.5f));

        PushPlayColor(_playing && !_paused);
        if (ImGui.Button(_playing ? "Stop"u8 : "Play"u8))
            TogglePlay();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.BeginDisabled(!_playing);
        if (ImGui.Button(_paused ? "Resume"u8 : "Pause"u8))
        {
            _paused = !_paused;
            Log(ConsoleSeverity.Info, _paused ? "Paused (stub)" : "Resumed (stub)");
        }

        ImGui.SameLine();
        if (ImGui.Button("Step"u8))
        {
            _paused = true;
            TickPlayMode(1f / 60f);
            Log(ConsoleSeverity.Info, "Frame step (stub)");
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextDisabled($"  {_scene.Name}{(UnsavedMarker())}");

        ImGui.EndChild();
    }

    string UnsavedMarker() => _playing ? "  [Playing]" : "";

    void PushPlayColor(bool active)
    {
        var color = active
            ? new float4(0.25f, 0.45f, 0.22f, 1f)
            : new float4(0.28f, 0.28f, 0.28f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Button, color);
    }

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
        var draw = ImGui.GetWindowDrawList();
        var max = new float2(origin.X + avail.X, origin.Y + avail.Y);
        draw.AddRectFilled(origin, max, ImGui.GetColorU32(background));

        var camera = FindByName("Main Camera")?.Components.OfType<StubCameraComponent>().FirstOrDefault();
        var projection = BuildProjection(avail.X / MathF.Max(avail.Y, 1f), camera);

        ImGuizmo.SetDrawlist(draw);
        ImGuizmo.SetRect(origin.X, origin.Y, avail.X, avail.Y);
        ImGuizmo.SetOrthographic(camera?.Orthographic ?? false);
        ImGuizmo.DrawGrid(view, projection, float4x4.Identity, 20f);

        CollectCubeMatrices();
        if (_cubeMatrices.Count > 0)
            ImGuizmo.DrawCubes(view, projection, CollectionsMarshal.AsSpan(_cubeMatrices));

        if (sceneTools)
        {
            ManipulateSelected(view, projection);
            HandleSceneNavigation();
            var cubeSize = new float2(96f, 96f);
            var cubePos = new float2(max.X - cubeSize.X - 8f, origin.Y + 8f);
            ImGuizmo.ViewManipulate(ref _sceneView, _sceneFocusDistance, cubePos, cubeSize, 0x10101010u);
            SyncSceneTargetFromView();
        }

        draw.AddText(
            new float2(origin.X + 10f, origin.Y + 8f),
            ImGui.GetColorU32(new float4(1f, 1f, 1f, 0.65f)),
            sceneTools ? "Scene (ImGuizmo)" : (_playing ? "Game (playing)" : "Game (edit mode)"));
    }

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
        var orbit = ImGui.IsMouseDragging(ImGuiMouseButton.Middle)
                    || (_gizmoMode == 0 && ImGui.IsMouseDragging(ImGuiMouseButton.Left));
        if (orbit)
            OrbitSceneCamera(io.MouseDelta);
        else if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
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

        ImGui.Text($"CPU  {fps:0.0} FPS   |   Play time {_playTime:0.00}s   |   modules are stubbed");
        if (ImPlot.BeginPlot("Frame rate"u8, new float2(-1f, -1f)))
        {
            ImPlot.PlotLine("FPS"u8, _fpsAxis, _fpsHistory);
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
}
