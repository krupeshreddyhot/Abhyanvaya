namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>Configuration for <see cref="RecognitionPipelineDiagnostics"/> (AI15.DIAGNOSTICS.1).</summary>
public sealed class RecognitionDiagnosticsOptions
{
    public const string SectionName = "RecognitionDiagnostics";

    /// <summary>
    /// Master on/off switch. When <c>false</c>, every diagnostics call is a true no-op (no snapshot
    /// capture, no allocation, no logging) — a safety valve if the added log volume ever needs to be
    /// turned off without a redeploy. This milestone is diagnostics-only, so the recognition pipeline
    /// itself behaves identically either way.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Working Set threshold, in MB, above which a stage boundary logs an OOM-approaching warning
    /// (AI15.DIAGNOSTICS.1 Task 8). Default leaves ~62 MB of headroom below Render's Starter plan's
    /// 512 MB hard limit — configurable, never hardcoded in the check itself.
    /// </summary>
    public int WorkingSetWarningThresholdMB { get; set; } = 450;
}
