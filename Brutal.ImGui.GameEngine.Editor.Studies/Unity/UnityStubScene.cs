using Brutal.Numerics;

/// <summary>
/// In-memory stand-ins for Unity types. Nothing here talks to a real engine;
/// it only gives the ImGui editor something to inspect and mutate.
/// </summary>
sealed class UnityStubScene
{
    public required string Name { get; init; }
    public List<StubGameObject> Roots { get; } = [];

    public static UnityStubScene CreateSampleScene()
    {
        var scene = new UnityStubScene { Name = "SampleScene" };

        var camera = new StubGameObject("Main Camera")
        {
            Position = new float3(0f, 1f, -10f),
        };
        camera.Components.Add(new StubCameraComponent());
        camera.Components.Add(new StubAudioListenerComponent());

        var player = new StubGameObject("Player")
        {
            Position = new float3(0f, 1f, 0f),
        };
        player.Components.Add(new StubMeshRendererComponent { Color = new float3(0.35f, 0.65f, 0.95f) });
        player.Components.Add(new StubMonoBehaviourComponent("PlayerController")
        {
            MoveSpeed = 6f,
            JumpHeight = 1.4f,
        });

        var ground = new StubGameObject("Ground")
        {
            Layer = "Default",
            Scale = new float3(10f, 1f, 10f),
        };
        ground.Components.Add(new StubMeshRendererComponent { Color = new float3(0.28f, 0.32f, 0.28f) });

        var cube = new StubGameObject("Cube") { Position = new float3(2.5f, 0.5f, 1f) };
        cube.Components.Add(new StubMeshRendererComponent { Color = new float3(0.85f, 0.45f, 0.25f) });

        var sphere = new StubGameObject("Sphere") { Position = new float3(-2.2f, 0.5f, 1.4f) };
        sphere.Components.Add(new StubMeshRendererComponent { Color = new float3(0.9f, 0.85f, 0.3f) });

        var environment = new StubGameObject("Environment");
        environment.AddChild(ground);
        environment.AddChild(cube);
        environment.AddChild(sphere);

        scene.Roots.Add(camera);
        scene.Roots.Add(player);
        scene.Roots.Add(environment);
        return scene;
    }

    public StubGameObject? Find(int id)
    {
        foreach (var root in Roots)
        {
            var found = Find(root, id);
            if (found is not null)
                return found;
        }

        return null;
    }

    static StubGameObject? Find(StubGameObject node, int id)
    {
        if (node.Id == id)
            return node;

        foreach (var child in node.Children)
        {
            var found = Find(child, id);
            if (found is not null)
                return found;
        }

        return null;
    }
}

sealed class StubGameObject
{
    static int s_nextId = 1;

    public StubGameObject(string name)
    {
        Id = s_nextId++;
        Name = name;
    }

    public int Id { get; }
    public string Name { get; set; }
    public bool Active { get; set; } = true;
    public string Layer { get; set; } = "Default";
    public float3 Position { get; set; }
    public float3 Rotation { get; set; }
    public float3 Scale { get; set; } = new(1f, 1f, 1f);
    public StubGameObject? Parent { get; private set; }
    public List<StubGameObject> Children { get; } = [];
    public List<StubComponent> Components { get; } = [];

    public void AddChild(StubGameObject child)
    {
        if (child.Parent == this)
            return;

        child.Parent?.Children.Remove(child);
        child.Parent = this;
        Children.Add(child);
    }
}

abstract class StubComponent
{
    public abstract string TypeName { get; }
    public bool Enabled { get; set; } = true;
}

sealed class StubCameraComponent : StubComponent
{
    public override string TypeName => "Camera";
    public float FieldOfView { get; set; } = 60f;
    public float NearClip { get; set; } = 0.3f;
    public float FarClip { get; set; } = 1000f;
    public bool Orthographic { get; set; }
}

sealed class StubMeshRendererComponent : StubComponent
{
    public override string TypeName => "Mesh Renderer";
    public float3 Color { get; set; } = new(0.8f, 0.8f, 0.8f);
    public bool CastShadows { get; set; } = true;
}

sealed class StubAudioListenerComponent : StubComponent
{
    public override string TypeName => "Audio Listener";
}

sealed class StubMonoBehaviourComponent : StubComponent
{
    public StubMonoBehaviourComponent(string scriptName) => ScriptName = scriptName;

    public override string TypeName => ScriptName;
    public string ScriptName { get; }
    public float MoveSpeed { get; set; }
    public float JumpHeight { get; set; }
}
