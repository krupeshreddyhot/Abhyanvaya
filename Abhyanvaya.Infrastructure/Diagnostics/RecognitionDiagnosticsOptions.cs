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

    /// <summary>
    /// AI16.RUNTIME.5 — diagnostics-only switch. When <see langword="true"/>, at the end of a
    /// successfully completed recognition job, forces <c>GC.Collect()</c> +
    /// <c>GC.WaitForPendingFinalizers()</c> + <c>GC.Collect()</c> and logs the before/after Managed
    /// Heap/Working Set/Private Memory so an investigator can tell whether elevated memory after a
    /// job is genuinely collectible managed garbage or native/unmanaged memory a GC pass cannot
    /// reclaim. <b>Defaults to <see langword="false"/></b> — forcing full blocking collections on
    /// every job would itself hurt latency/CPU and is never appropriate as a steady-state production
    /// setting; this exists purely to be flipped on temporarily on Render while investigating the
    /// Starter-plan OOM restarts, then flipped back off.
    /// </summary>
    public bool ForceGcValidation { get; set; } = false;
}
