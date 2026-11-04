using Brutal.Numerics;

/// <summary>
/// In-memory stand-ins for Unreal types. Nothing here talks to a real engine;
/// it only gives the ImGui editor something to inspect and mutate.
/// </summary>
sealed class UnrealStubWorld
{
    public required string MapName { get; init; }
    public List<UnrealStubFolder> Folders { get; } = [];
    public List<UnrealStubActor> RootActors { get; } = [];

    public static UnrealStubWorld CreateThirdPersonMap()
    {
        var world = new UnrealStubWorld { MapName = "ThirdPersonMap" };

        var lighting = new UnrealStubFolder("Lighting");
        var geometry = new UnrealStubFolder("Geometry");
        var characters = new UnrealStubFolder("Characters");
        world.Folders.Add(lighting);
        world.Folders.Add(geometry);
        world.Folders.Add(characters);

        var camera = new UnrealStubActor("CameraActor")
        {
            ClassName = "CameraActor",
            Location = new float3(0f, 180f, -900f),
            Rotation = new float3(0f, 0f, 0f),
        };
        camera.Components.Add(new UnrealStubCameraComponent());

        var light = new UnrealStubActor("DirectionalLight")
        {
            ClassName = "DirectionalLight",
            Folder = lighting,
            Rotation = new float3(-40f, -30f, 0f),
        };
        light.Components.Add(new UnrealStubLightComponent
        {
            Intensity = 10f,
            Color = new float3(1f, 0.96f, 0.88f),
            Mobility = UnrealMobility.Stationary,
        });

        var skyLight = new UnrealStubActor("SkyLight")
        {
            ClassName = "SkyLight",
            Folder = lighting,
            Location = new float3(0f, 200f, 0f),
        };
        skyLight.Components.Add(new UnrealStubLightComponent
        {
            Intensity = 1f,
            Color = new float3(0.55f, 0.7f, 1f),
            Mobility = UnrealMobility.Stationary,
        });

        var floor = new UnrealStubActor("Floor")
        {
            ClassName = "StaticMeshActor",
            Folder = geometry,
            Scale = new float3(10f, 0.2f, 10f),
        };
        floor.Components.Add(new UnrealStubStaticMeshComponent
        {
            MeshName = "SM_Plane",
            Color = new float3(0.22f, 0.24f, 0.26f),
            Mobility = UnrealMobility.Static,
        });

        var cube = new UnrealStubActor("SM_Cube")
        {
            ClassName = "StaticMeshActor",
            Folder = geometry,
            Location = new float3(250f, 50f, 140f),
            Scale = new float3(1f, 1f, 1f),
        };
        cube.Components.Add(new UnrealStubStaticMeshComponent
        {
            MeshName = "SM_Cube",
            Color = new float3(0.85f, 0.42f, 0.18f),
        });

        var sphere = new UnrealStubActor("SM_Sphere")
        {
            ClassName = "StaticMeshActor",
            Folder = geometry,
            Location = new float3(-220f, 50f, 160f),
        };
        sphere.Components.Add(new UnrealStubStaticMeshComponent
        {
            MeshName = "SM_Sphere",
            Color = new float3(0.2f, 0.55f, 0.95f),
        });

        var playerStart = new UnrealStubActor("PlayerStart")
        {
            ClassName = "PlayerStart",
            Folder = characters,
            Location = new float3(0f, 92f, 0f),
        };

        var character = new UnrealStubActor("BP_ThirdPersonCharacter")
        {
            ClassName = "BP_ThirdPersonCharacter",
            Folder = characters,
            Location = new float3(0f, 96f, 80f),
        };
        character.Components.Add(new UnrealStubStaticMeshComponent
        {
            MeshName = "SKM_Manny",
            Color = new float3(0.35f, 0.72f, 0.45f),
            Mobility = UnrealMobility.Movable,
        });
        character.Components.Add(new UnrealStubMovementComponent());

        world.RootActors.Add(camera);
        world.RootActors.Add(light);
        world.RootActors.Add(skyLight);
        world.RootActors.Add(floor);
        world.RootActors.Add(cube);
        world.RootActors.Add(sphere);
        world.RootActors.Add(playerStart);
        world.RootActors.Add(character);
        return world;
    }

    public IEnumerable<UnrealStubActor> AllActors()
    {
        foreach (var actor in RootActors)
        {
            yield return actor;
            foreach (var child in Walk(actor))
                yield return child;
        }
    }

    static IEnumerable<UnrealStubActor> Walk(UnrealStubActor node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var nested in Walk(child))
                yield return nested;
        }
    }

    public UnrealStubActor? Find(int id)
    {
        foreach (var actor in AllActors())
        {
            if (actor.Id == id)
                return actor;
        }

        return null;
    }

    public UnrealStubActor? FindByLabel(string label)
    {
        foreach (var actor in AllActors())
        {
            if (actor.Label == label)
                return actor;
        }

        return null;
    }
}

sealed class UnrealStubFolder(string name)
{
    public string Name { get; set; } = name;
}

enum UnrealMobility
{
    Static,
    Stationary,
    Movable,
}

sealed class UnrealStubActor
{
    static int s_nextId = 1;

    public UnrealStubActor(string label)
    {
        Id = s_nextId++;
        Label = label;
        ClassName = label;
    }

    public int Id { get; }
    public string Label { get; set; }
    public string ClassName { get; set; }
    public bool Visible { get; set; } = true;
    public UnrealStubFolder? Folder { get; set; }
    public float3 Location { get; set; }
    public float3 Rotation { get; set; }
    public float3 Scale { get; set; } = new(1f, 1f, 1f);
    public UnrealMobility Mobility { get; set; } = UnrealMobility.Movable;
    public List<UnrealStubActor> Children { get; } = [];
    public List<UnrealStubComponent> Components { get; } = [];
}

abstract class UnrealStubComponent
{
    public abstract string TypeName { get; }
    public bool Enabled { get; set; } = true;
}

sealed class UnrealStubCameraComponent : UnrealStubComponent
{
    public override string TypeName => "CameraComponent";
    public float FieldOfView { get; set; } = 90f;
    public float NearClip { get; set; } = 10f;
    public float FarClip { get; set; } = 100000f;
    public bool Orthographic { get; set; }
}

sealed class UnrealStubLightComponent : UnrealStubComponent
{
    public override string TypeName => "LightComponent";
    public float Intensity { get; set; } = 8f;
    public float3 Color { get; set; } = new(1f, 1f, 1f);
    public UnrealMobility Mobility { get; set; } = UnrealMobility.Stationary;
}

sealed class UnrealStubStaticMeshComponent : UnrealStubComponent
{
    public override string TypeName => "StaticMeshComponent";
    public string MeshName { get; set; } = "SM_Cube";
    public float3 Color { get; set; } = new(0.8f, 0.8f, 0.8f);
    public UnrealMobility Mobility { get; set; } = UnrealMobility.Static;
}

sealed class UnrealStubMovementComponent : UnrealStubComponent
{
    public override string TypeName => "CharacterMovementComponent";
    public float MaxWalkSpeed { get; set; } = 500f;
    public float JumpZVelocity { get; set; } = 700f;
}
