using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// Shared formatting for the "Execution Trace" sub-block appended to the small set of per-job summary
/// logs named in AI15.DIAGNOSTICS.2B/2C (Queue Trace, Pipeline Entry, Recognition Started/Completed,
/// Recognition Memory Summary, Failure Diagnostics, and the health endpoint projection). Deliberately
/// NOT applied to the high-frequency per-stage/per-face boxes from AI15.DIAGNOSTICS.1 — those log
/// formats are explicitly preserved unchanged, and appending eight more lines to every one of those
/// (which fire many times per job) would be pure log-volume bloat with no forensic value beyond what
/// the summary-level logs already carry.
/// </summary>
public static class ExecutionTraceLog
{
    /// <summary>
    /// <c>TRACE-yyyyMMdd-HHmmss-XXXXXXXX</c> — a human-readable display form of the underlying
    /// <see cref="IRecognitionExecutionContext.ExecutionTraceId"/> <see cref="Guid"/>. The interface
    /// stores a <see cref="Guid"/> (globally unique, zero-allocation to generate, no custom ID
    /// generator to maintain); this formatter derives the requested display format from it plus
    /// <see cref="IRecognitionExecutionContext.QueueStartUtc"/> rather than storing two redundant
    /// identifiers on the context.
    /// </summary>
    public static string FormatTraceId(IRecognitionExecutionContext context)
    {
        var timestamp = context.QueueStartUtc == DateTime.MinValue ? DateTime.UtcNow : context.QueueStartUtc;
        var suffix = context.ExecutionTraceId.ToString("N")[..8].ToUpperInvariant();
        return $"TRACE-{timestamp:yyyyMMdd}-{timestamp:HHmmss}-{suffix}";
    }

    /// <summary>Milliseconds since the job was dequeued, or 0 before <see cref="IRecognitionExecutionContext.Initialize"/>.</summary>
    public static long ElapsedSinceQueueMs(IRecognitionExecutionContext context) =>
        context.QueueStartUtc == DateTime.MinValue ? 0 : Math.Max(0, (long)(DateTime.UtcNow - context.QueueStartUtc).TotalMilliseconds);

    /// <summary>Milliseconds since <see cref="IRecognitionExecutionContext.MarkPipelineStarted"/>, or 0 before that call.</summary>
    public static long ElapsedSincePipelineStartMs(IRecognitionExecutionContext context) =>
        context.PipelineStartUtc == DateTime.MinValue ? 0 : Math.Max(0, (long)(DateTime.UtcNow - context.PipelineStartUtc).TotalMilliseconds);

    /// <summary>
    /// Logs the "Execution Trace" sub-block. <paramref name="pipelineVersion"/> and
    /// <paramref name="recognitionEngine"/> are supplied by the caller (sourced from the already-bound
    /// <c>InsightFaceOptions.PipelineVersion</c> / <c>EmbeddingProviders.InsightFace</c> respectively)
    /// rather than read here, so this helper never binds configuration itself — no duplicate reads.
    /// </summary>
    public static void LogBlock(
        ILogger logger,
        IRecognitionExecutionContext context,
        string pipelineVersion,
        string recognitionEngine)
    {
        logger.LogInformation("  Execution Trace");
        logger.LogInformation("    Trace Id                          : {TraceId}", FormatTraceId(context));
        logger.LogInformation("    Pipeline Version                  : {PipelineVersion}", pipelineVersion);
        logger.LogInformation("    Recognition Engine                : {RecognitionEngine}", recognitionEngine);
        logger.LogInformation("    Tenant                             : {TenantId}", context.TenantId);
        logger.LogInformation("    Session                            : {AttendanceSessionId}", context.AttendanceSessionId);
        logger.LogInformation("    Attempt                            : {RecognitionAttempt}", context.RecognitionAttempt);
        logger.LogInformation("    Elapsed Since Queue                : {ElapsedSinceQueueMs} ms", ElapsedSinceQueueMs(context));
        logger.LogInformation("    Elapsed Since Pipeline Start       : {ElapsedSincePipelineStartMs} ms", ElapsedSincePipelineStartMs(context));
    }
}
