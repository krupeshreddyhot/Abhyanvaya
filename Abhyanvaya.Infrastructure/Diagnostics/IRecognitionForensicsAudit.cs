namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// AI17.RUNTIME — diagnostics-only, per-job (DI-scoped) native-memory root-cause forensics for one
/// classroom recognition run. Deliberately a separate service from
/// <see cref="IRecognitionPipelineDiagnostics"/> (AI15/AI16) rather than an extension of it: this keeps
/// every existing AI15/AI16 log format and code path byte-for-byte untouched (zero regression risk to
/// already-shipped diagnostics) while adding the new AI17 checkpoint/audit fields the investigation
/// calls for. Every method here only reads process/GC state and logs — none of them influence any
/// recognition, matching, or persistence decision, and none of them are allowed to throw (the
/// implementation swallows and logs a single internal warning instead, never propagates).
///
/// Unlike <see cref="IRecognitionPipelineDiagnostics"/>, <see cref="Checkpoint"/> is NOT gated behind a
/// <c>Begin()</c> call — the very first required checkpoint ("Queue Received") fires in
/// <c>ClassroomRecognitionBackgroundService</c>, before the pipeline (and therefore before
/// <c>IRecognitionPipelineDiagnostics.Begin</c>) even starts. It is gated only by
/// <see cref="Abhyanvaya.Infrastructure.Diagnostics.RecognitionDiagnosticsOptions.Enabled"/>, the same
/// global on/off switch AI15/AI16 already use.
/// </summary>
public interface IRecognitionForensicsAudit
{
    /// <summary>
    /// AI17.RUNTIME.1 — logs one stage memory checkpoint (Stage Name, Execution Trace Id, Working Set,
    /// Private Memory, Managed Heap, Native Estimate, Gen0/1/2, Process Thread Count, Elapsed Time,
    /// Peak Memory So Far) and flags "MEMORY SPIKE DETECTED" if Working Set grew by more than 25 MB
    /// since the previous checkpoint call on this instance.
    /// </summary>
    void Checkpoint(string stageName, int? faceNumber = null);

    /// <summary>
    /// AI17.RUNTIME.2/.4 — records that a heavy <see cref="IDisposable"/> was created. <paramref name="detail"/>
    /// must be unique among currently-open instances of <paramref name="objectType"/> (e.g. "aligned face 3")
    /// so the matching <see cref="ObjectDisposed"/> call can be correlated to compute lifetime.
    /// <paramref name="width"/>/<paramref name="height"/>/<paramref name="pixelFormat"/>/<paramref name="estimatedBytes"/>
    /// are AI17.RUNTIME.4-specific and optional — only supplied for ImageSharp <c>Image</c> instances.
    /// </summary>
    void ObjectCreated(
        string objectType,
        string detail,
        int? width = null,
        int? height = null,
        string? pixelFormat = null,
        long? estimatedBytes = null);

    /// <summary>AI17.RUNTIME.2/.4 — records that the object created via the matching <see cref="ObjectCreated"/> call was disposed, and logs its lifetime.</summary>
    void ObjectDisposed(string objectType, string detail);

    /// <summary>
    /// AI17.RUNTIME.4 — checks whether an ImageSharp face-crop object identified by <paramref name="detail"/>
    /// is still open (i.e. <see cref="ObjectDisposed"/> has not yet been called for it) and, if so, logs
    /// "WARNING: Face crop retained." This is called immediately after embedding generation completes
    /// for one face — it does not itself dispose or otherwise touch the object.
    /// </summary>
    void CheckFaceCropRetainedAfterEmbedding(string detail);

    /// <summary>AI17.RUNTIME.3 — logs the student-embedding load audit for one job. See remarks on the implementation for field derivation.</summary>
    void RecordStudentEmbeddingLoad(
        int studentCount,
        int embeddingCount,
        int totalEmbeddingFloats,
        bool asNoTracking,
        string navigationPropertiesLoaded,
        bool lazyLoadingEnabled);

    /// <summary>
    /// AI17.RUNTIME.5 — logs one ONNX Runtime inference call's audit (model, tensor shapes, duration,
    /// native memory before/after, Working Set delta, disposable output count, session/tensor reuse).
    /// <paramref name="before"/>/<paramref name="after"/> must be captured immediately before/after the
    /// <c>session.Run(...)</c> call being audited.
    /// </summary>
    void RecordOnnxInference(
        string model,
        string inputTensorShape,
        string outputTensorShape,
        long inferenceDurationMs,
        RecognitionMemorySnapshot before,
        RecognitionMemorySnapshot after,
        bool inferenceSessionReused,
        bool tensorReused,
        int disposableOutputCount);

    /// <summary>
    /// AI17.RUNTIME.6 — logs the candidate-matching memory audit for one job's single batched
    /// <c>IFaceMatcher.Match(...)</c> call. <paramref name="before"/>/<paramref name="after"/> must be
    /// captured immediately before/after that call.
    /// </summary>
    void RecordMatching(
        int detectedFaceCount,
        int candidateStudentCount,
        RecognitionMemorySnapshot before,
        RecognitionMemorySnapshot after);

    /// <summary>
    /// Must be called exactly once, at job completion (success or failure) — flags any object created
    /// via <see cref="ObjectCreated"/> that was never disposed ("LONG LIVED DISPOSABLE" /
    /// "UNDISPOSED ONNX OUTPUT") and logs the final per-job audit summary counts.
    /// </summary>
    void FinalizeAudit();
}
