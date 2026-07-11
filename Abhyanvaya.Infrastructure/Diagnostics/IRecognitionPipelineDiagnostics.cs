namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// Lightweight, per-job (DI-scoped) memory/timing forensics for one classroom recognition run
/// (AI15.DIAGNOSTICS.1). Diagnostics-only: every method here only reads process/GC state and logs —
/// none of them influence any recognition, matching, or persistence decision, and none of them are
/// allowed to throw (implementations must swallow and log a single internal warning instead, never
/// propagate, so a diagnostics bug can never affect recognition behavior).
///
/// Inert until <see cref="Begin"/> is called (see remarks on the implementation) — this lets
/// <c>InsightFaceEngine</c>'s shared private helpers be instrumented once for Task 6 (tensor/ORT
/// value lifecycle) without any effect on the student face embedding pipeline, which never calls
/// <see cref="Begin"/> and therefore never activates a diagnostics session on the same engine.
/// </summary>
public interface IRecognitionPipelineDiagnostics
{
    /// <summary>Starts a new diagnostics session for one classroom recognition job. Logs "Recognition Started".</summary>
    void Begin(Guid attendanceSessionId, int tenantId);

    /// <summary>Logs the "Started" boundary for a stage and returns a handle for the matching <see cref="StageEnd"/> call.</summary>
    RecognitionStageHandle StageStart(string stageName, int? faceNumber = null, int? faceCount = null);

    /// <summary>Logs the "Finished" boundary for a stage previously opened with <see cref="StageStart"/>.</summary>
    void StageEnd(RecognitionStageHandle handle);

    /// <summary>Logs a batched-but-per-face-attributed event (e.g. matching, computed once for all faces but reported per face).</summary>
    void FaceEvent(string label, int faceNumber, int faceCount);

    /// <summary>Records that an <see cref="IDisposable"/> instance was created (Task 6). Lightweight — no memory snapshot.</summary>
    void ObjectCreated(string typeName, string? detail = null);

    /// <summary>Records that an <see cref="IDisposable"/> instance was disposed (Task 6). Lightweight — no memory snapshot.</summary>
    void ObjectDisposed(string typeName, string? detail = null);

    /// <summary>Logs failure diagnostics (current stage/face, peak memory, elapsed, stack trace) and finalizes the session as failed. Never swallows or alters <paramref name="exception"/> — callers still rethrow it themselves.</summary>
    void Fail(Exception exception);

    /// <summary>Finalizes the session as completed, logging the memory and timing summaries (Tasks 5 &amp; 7).</summary>
    void Complete();
}
