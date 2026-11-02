/// <summary>
/// One ImGui study of a real engine's editor. Functionality is stubbed so the
/// focus stays on learning Dear ImGui layout, widgets, and docking.
/// </summary>
interface IEngineEditorStudy
{
    string WindowTitle { get; }

    void Setup();

    void Draw(Action requestClose);
}

static class EngineEditorStudyFactory
{
    public static IEngineEditorStudy Create(StudySubjects subject) => subject switch
    {
        StudySubjects.UnityEngine => new UnityEngineEditorStudy(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(subject),
            subject,
            "Add a study class for this engine and wire it here."),
    };
}
