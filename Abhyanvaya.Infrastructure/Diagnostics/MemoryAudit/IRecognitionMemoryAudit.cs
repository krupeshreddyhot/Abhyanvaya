namespace Abhyanvaya.Infrastructure.Diagnostics.MemoryAudit;

/// <summary>
/// AI18.MEMORY.1 — diagnostics-only, per-job (DI-scoped) COMPLETE memory forensics for one classroom
/// recognition run. This is a forensic investigation tool, not an optimization: every method here only
/// reads process/GC state or records caller-supplied size estimates and logs — none of them influence
/// any recognition, matching, storage, or persistence decision, and none of them are allowed to throw
/// (the implementation swallows and logs a single internal warning instead, never propagates).
/// </summary>
/// <remarks>
/// Deliberately a separate service from <see cref="IRecognitionPipelineDiagnostics"/> (AI15/AI16) and
/// <see cref="IRecognitionForensicsAudit"/> (AI17.RUNTIME) rather than an extension of either: this keeps
/// every existing AI15/AI16/AI17 log format and code path byte-for-byte untouched (zero regression risk
/// to already-shipped diagnostics) while adding the richer AI18 snapshot fields (GC fragmentation, GC
/// memory load, handle count, processor time, four independent running peaks) and the generic heavy-object
/// registry / Top-20 / final-report machinery AI18.MEMORY.1 calls for. Unlike
/// <see cref="IRecognitionForensicsAudit"/>, this service is gated by an explicit <see cref="Begin"/> call
/// (per the AI18.MEMORY.1 prompt: "completely inert until Begin()") rather than implicitly activating on
/// first use.
/// </remarks>
public interface IRecognitionMemoryAudit
{
    /// <summary>
    /// Activates this audit instance for the current DI scope (one recognition job) and captures the
    /// baseline snapshot every later "increase since pipeline start" figure is measured against. A no-op
    /// (and every other method on this interface remains a no-op) until this is called.
    /// </summary>
    void Begin();

    /// <summary>
    /// Captures and logs one full memory snapshot (STEP 2 fields) for <paramref name="stage"/>, and
    /// automatically computes/logs the STEP 10 memory-delta report (increase since previous stage,
    /// increase since pipeline start, and updates the running "largest stage increase" tracker).
    /// </summary>
    void Snapshot(string stage, int? faceNumber = null);

    /// <summary>
    /// STEP 3 — records that a heavy object (&gt;100 KB, or any object worth tracking regardless of size)
    /// was created at <paramref name="stage"/>. Returns an opaque registration id to pass to
    /// <see cref="DisposeObject"/> later; returns -1 if the audit is inactive (caller does not need to
    /// branch on this — passing -1 to <see cref="DisposeObject"/> is always a safe no-op).
    /// </summary>
    long RegisterObject(string objectType, long approximateBytes, string stage, int? faceNumber = null);

    /// <summary>STEP 3 — records that the object identified by <paramref name="registrationId"/> was disposed, and logs its measured lifetime.</summary>
    void DisposeObject(long registrationId);

    /// <summary>
    /// STEP 4 — Entity Framework materialization audit for one major query. Call immediately after the
    /// query executes (i.e. once entity counts are known), before the results are used.
    /// </summary>
    void RecordEntityFrameworkQuery(
        string queryName,
        bool asNoTracking,
        int entitiesMaterialized,
        string navigationPropertiesLoaded,
        bool studentPhotosLoaded,
        bool navigationCollectionsLoaded,
        long estimatedGraphBytes);

    /// <summary>STEP 5 — student embedding load audit for one job.</summary>
    void RecordStudentEmbeddingLoad(
        int studentsLoaded,
        int embeddingsLoaded,
        int embeddingDimensions,
        long totalFloatCount,
        int duplicateStudentIds,
        int nullEmbeddings,
        bool imageBytesLoaded,
        bool photoLoaded,
        bool navigationLoaded);

    /// <summary>
    /// STEP 7 — ONNX Runtime inference audit. <paramref name="before"/>/<paramref name="after"/> must be
    /// captured (via <see cref="Snapshot"/> or an equivalent raw capture) immediately before/after the
    /// <c>session.Run(...)</c> call being audited.
    /// </summary>
    void RecordOnnxInference(
        string model,
        string inputTensorShape,
        string outputTensorShape,
        long inputBytesApprox,
        long outputBytesApprox,
        MemoryAuditSnapshot before,
        MemoryAuditSnapshot after,
        long inferenceDurationMs,
        int disposableOutputCount,
        bool outputsDisposed);

    /// <summary>
    /// Matching memory audit (supports STEP 10/12's "Largest Matching Allocation").
    /// <paramref name="before"/>/<paramref name="after"/> must be captured immediately before/after the
    /// batched candidate-matching call.
    /// </summary>
    void RecordMatchingMemory(
        int detectedFaceCount,
        int candidateStudentCount,
        MemoryAuditSnapshot before,
        MemoryAuditSnapshot after);

    /// <summary>
    /// STEP 9 — database save audit. Call once with <paramref name="phase"/> = "Before" immediately
    /// before <c>SaveChangesAsync</c>, and once with "After" immediately after it returns.
    /// </summary>
    void RecordDatabaseSave(
        string phase,
        int pendingEntityCount,
        int attendanceRecognitionCount,
        int attendanceSessionCount,
        long estimatedGraphBytes);

    /// <summary>
    /// STEP 11/12 — must be called exactly once, at job completion (success or failure): captures the
    /// final snapshot, logs the TOP 20 MEMORY CONSUMERS table, logs the MEMORY FORENSICS REPORT, and
    /// flags any registered object that was never disposed.
    /// </summary>
    void Complete();
}
