using System.Diagnostics;

namespace Abhyanvaya.Infrastructure.Diagnostics.MemoryAudit;

/// <summary>
/// AI18.MEMORY.1 — one point-in-time forensic memory reading, richer than
/// <see cref="RecognitionMemorySnapshot"/> (AI15/AI16/AI17): adds GC heap fragmentation, GC memory
/// load, OS handle count, process processor time, and running "peak so far" values for every core
/// metric. Uses only <see cref="GC.GetGCMemoryInfo"/>, <see cref="GC.GetTotalMemory"/>,
/// <see cref="Environment.WorkingSet"/>, and <see cref="Process"/> — no third-party packages, no
/// allocation proportional to any image/tensor size.
/// </summary>
public readonly record struct MemoryAuditSnapshot(
    DateTime TimestampUtc,
    string ExecutionTraceId,
    string Stage,
    double ElapsedMs,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long ManagedHeapBytes,
    long GcFragmentedBytes,
    long GcMemoryLoadBytes,
    long NativeEstimateBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int ThreadCount,
    int HandleCount,
    double ProcessorTimeMs,
    long PeakWorkingSetBytes,
    long PeakPrivateMemoryBytes,
    long PeakManagedHeapBytes,
    long PeakNativeEstimateBytes)
{
    /// <summary>
    /// Captures the current process/GC state and folds it against the caller-supplied "peak so far"
    /// values (owned and persisted by the caller — <see cref="RecognitionMemoryAudit"/> — across calls,
    /// since this struct itself is stateless/immutable).
    /// </summary>
    public static MemoryAuditSnapshot Capture(
        string executionTraceId,
        string stage,
        double elapsedMs,
        long peakWorkingSetSoFarBytes,
        long peakPrivateMemorySoFarBytes,
        long peakManagedHeapSoFarBytes,
        long peakNativeEstimateSoFarBytes)
    {
        // GC.GetTotalMemory(false): read the last-known heap size without forcing a collection — a
        // forced collection would itself distort the very measurement being taken (AI16.RUNTIME.5
        // handles forced-GC validation separately, behind its own explicit opt-in).
        var managedHeapBytes = GC.GetTotalMemory(false);
        var workingSetBytes = Environment.WorkingSet;
        var gcInfo = GC.GetGCMemoryInfo();

        long privateBytes;
        int threadCount;
        int handleCount;
        double processorTimeMs;
        using (var process = Process.GetCurrentProcess())
        {
            privateBytes = process.PrivateMemorySize64;
            threadCount = process.Threads.Count;
            handleCount = process.HandleCount;
            processorTimeMs = process.TotalProcessorTime.TotalMilliseconds;
        }

        // Same native-memory estimation methodology as AI16.RUNTIME.4/AI17.RUNTIME (Private Bytes
        // minus the managed heap) — kept identical across all three diagnostics generations so figures
        // are directly comparable in logs from different milestones.
        var nativeEstimateBytes = Math.Max(0, privateBytes - managedHeapBytes);

        return new MemoryAuditSnapshot(
            TimestampUtc: DateTime.UtcNow,
            ExecutionTraceId: executionTraceId,
            Stage: stage,
            ElapsedMs: elapsedMs,
            WorkingSetBytes: workingSetBytes,
            PrivateMemoryBytes: privateBytes,
            ManagedHeapBytes: managedHeapBytes,
            GcFragmentedBytes: gcInfo.FragmentedBytes,
            GcMemoryLoadBytes: gcInfo.MemoryLoadBytes,
            NativeEstimateBytes: nativeEstimateBytes,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            ThreadCount: threadCount,
            HandleCount: handleCount,
            ProcessorTimeMs: processorTimeMs,
            PeakWorkingSetBytes: Math.Max(peakWorkingSetSoFarBytes, workingSetBytes),
            PeakPrivateMemoryBytes: Math.Max(peakPrivateMemorySoFarBytes, privateBytes),
            PeakManagedHeapBytes: Math.Max(peakManagedHeapSoFarBytes, managedHeapBytes),
            PeakNativeEstimateBytes: Math.Max(peakNativeEstimateSoFarBytes, nativeEstimateBytes));
    }

    public double WorkingSetMB => Math.Round(WorkingSetBytes / (1024d * 1024d), 2);
    public double PrivateMemoryMB => Math.Round(PrivateMemoryBytes / (1024d * 1024d), 2);
    public double ManagedHeapMB => Math.Round(ManagedHeapBytes / (1024d * 1024d), 2);
    public double GcFragmentedMB => Math.Round(GcFragmentedBytes / (1024d * 1024d), 2);
    public double GcMemoryLoadMB => Math.Round(GcMemoryLoadBytes / (1024d * 1024d), 2);
    public double NativeEstimateMB => Math.Round(NativeEstimateBytes / (1024d * 1024d), 2);
    public double PeakWorkingSetMB => Math.Round(PeakWorkingSetBytes / (1024d * 1024d), 2);
    public double PeakPrivateMemoryMB => Math.Round(PeakPrivateMemoryBytes / (1024d * 1024d), 2);
    public double PeakManagedHeapMB => Math.Round(PeakManagedHeapBytes / (1024d * 1024d), 2);
    public double PeakNativeEstimateMB => Math.Round(PeakNativeEstimateBytes / (1024d * 1024d), 2);
}
