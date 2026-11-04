/// <summary>
/// One ImGui study of a real engine's editor. Functionality is stubbed so the
/// focus stays on learning Dear ImGui layout, widgets, and docking.
/// </summary>
interface IEngineEditorStudy
{
    string WindowTitle { get; }

    void Setup();

    void Draw(Action requestClose, Action returnToLauncher);
}

readonly record struct StudyCatalogEntry(
    StudySubjects Subject,
    string Title,
    string Summary);

static class EngineEditorStudyFactory
{
    public static IReadOnlyList<StudyCatalogEntry> Catalog { get; } =
    [
        new(
            StudySubjects.UnityEngine,
            "Unity Editor",
            "Hierarchy, Scene/Game, Inspector, Project, Console, and Profiler with ImGuizmo scene tools."),
        new(
            StudySubjects.UnrealEngine,
            "Unreal Editor",
            "Place Actors, Viewport, World Outliner, Details, Content Browser, and Output Log with PIE stubs."),
    ];

    public static IEngineEditorStudy Create(StudySubjects subject) => subject switch
    {
        StudySubjects.UnityEngine => new UnityEngineEditorStudy(),
        StudySubjects.UnrealEngine => new UnrealEngineEditorStudy(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(subject),
            subject,
            "Add a study class for this engine and wire it here."),
    };
}
