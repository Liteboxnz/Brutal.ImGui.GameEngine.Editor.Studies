/// <summary>
/// Which game-engine editor this app is currently mimicking.
/// Add a new value here when you start a study of another engine, then
/// handle it in <see cref="EngineEditorStudyFactory"/> and point
/// <c>Program.CurrentStudy</c> at it.
/// </summary>
enum StudySubjects
{
    UnityEngine,
}
