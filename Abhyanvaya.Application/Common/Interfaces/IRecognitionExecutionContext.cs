namespace Abhyanvaya.Application.Common.Interfaces
{
    /// <summary>
    /// Scoped, per-job correlation context for one classroom recognition execution
    /// (AI15.DIAGNOSTICS.2A). Follows the exact same architectural pattern as
    /// <see cref="ITenantContextAccessor"/>: a Scoped instance owned by the DI scope the background
    /// worker creates per dequeued message, initialized once at the start of that job and cleared in a
    /// <c>finally</c> block. No static state, no <c>AsyncLocal</c>, no <c>ThreadStatic</c> — this is
    /// deliberately chosen over ambient/thread-flowing state so the same mechanism keeps working
    /// unchanged if recognition is ever split across multiple workers, parallel per-face processing,
    /// or a distributed queue (Hangfire/Azure Queue/etc.), where a single logical "current thread" or
    /// "current async flow" no longer corresponds to one job.
    /// </summary>
    public interface IRecognitionExecutionContext
    {
        /// <summary>Stable per-job correlation identifier, minted once by <see cref="Initialize"/>. Formatted for logs as <c>TRACE-yyyyMMdd-HHmmss-XXXXXXXX</c> via <c>RecognitionExecutionContextFormatting.FormatTraceId</c>.</summary>
        Guid ExecutionTraceId { get; }

        /// <summary>The attendance session this execution is processing.</summary>
        Guid AttendanceSessionId { get; }

        /// <summary>The tenant that owns <see cref="AttendanceSessionId"/>.</summary>
        int TenantId { get; }

        /// <summary>UTC timestamp the job was dequeued (start of the "Elapsed Since Queue" measurement).</summary>
        DateTime QueueStartUtc { get; }

        /// <summary>UTC timestamp <see cref="MarkPipelineStarted"/> was called, or <see cref="DateTime.MinValue"/> before that.</summary>
        DateTime PipelineStartUtc { get; }

        /// <summary>
        /// Which attempt this execution represents. Always <c>1</c> today — no retry mechanism exists
        /// yet (AI15.DIAGNOSTICS.2C is diagnostics-only groundwork for future retry support).
        /// </summary>
        int RecognitionAttempt { get; }

        /// <summary>Binds this scope to one recognition job. Must be called exactly once, before any other member is used.</summary>
        void Initialize(Guid sessionId, int tenantId, int attempt, DateTime queueUtc);

        /// <summary>Records that pipeline execution has begun, setting <see cref="PipelineStartUtc"/>.</summary>
        void MarkPipelineStarted();

        /// <summary>Removes the binding for this scope. Must always be called in a <c>finally</c> block to prevent state leaking across jobs if scopes are ever pooled/reused.</summary>
        void Clear();
    }
}
