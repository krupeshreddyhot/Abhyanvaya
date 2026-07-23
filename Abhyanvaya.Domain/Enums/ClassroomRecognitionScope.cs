namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// AI22.7A Phase 3 — how much of a session's classroom images a recognition job should process.
/// </summary>
public enum ClassroomRecognitionScope : short
{
    /// <summary>Process every session image (reorder / legacy full rebuild).</summary>
    FullSession = 0,

    /// <summary>Process only the targeted image; leave other Processed images untouched.</summary>
    SingleImage = 1,

    /// <summary>Process Uploaded and Failed images only; skip successfully Processed images.</summary>
    PendingOnly = 2,
}
