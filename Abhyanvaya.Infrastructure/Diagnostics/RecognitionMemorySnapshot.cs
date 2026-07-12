using System.Diagnostics;

namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// A single point-in-time process memory/GC reading (AI15.DIAGNOSTICS.1). Uses only
/// <see cref="GC.GetTotalMemory"/>, <see cref="Environment.WorkingSet"/>,
/// <see cref="Process.GetCurrentProcess"/>, and <see cref="GC.CollectionCount"/> — no third-party
/// packages, no allocations proportional to any image/tensor size.
/// </summary>
public readonly record struct RecognitionMemorySnapshot(
    DateTime TimestampUtc,
    long ManagedHeapBytes,
    long WorkingSetBytes,
    long PrivateBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    int ThreadId,
    int ProcessThreadCount)
{
    public static RecognitionMemorySnapshot Capture()
    {
        // GC.GetTotalMemory(false): read the last-known heap size without forcing a collection —
        // a forced collection would itself distort the very measurement being taken.
        var managedHeapBytes = GC.GetTotalMemory(false);
        var workingSetBytes = Environment.WorkingSet;

        long privateBytes;
        int processThreadCount;
        using (var process = Process.GetCurrentProcess())
        {
            privateBytes = process.PrivateMemorySize64;
            // AI17.RUNTIME.1: total OS-level thread count for the process (not to be confused with
            // ThreadId below, which is the *current managed* thread executing this capture). Read
            // from the same already-open Process handle used for PrivateBytes above — no extra
            // process-table lookup.
            processThreadCount = process.Threads.Count;
        }

        return new RecognitionMemorySnapshot(
            TimestampUtc: DateTime.UtcNow,
            ManagedHeapBytes: managedHeapBytes,
            WorkingSetBytes: workingSetBytes,
            PrivateBytes: privateBytes,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            ThreadId: Environment.CurrentManagedThreadId,
            ProcessThreadCount: processThreadCount);
    }

    public double ManagedHeapMegabytes => Math.Round(ManagedHeapBytes / (1024d * 1024d), 1);
    public double WorkingSetMegabytes => Math.Round(WorkingSetBytes / (1024d * 1024d), 1);
    public double PrivateMegabytes => Math.Round(PrivateBytes / (1024d * 1024d), 1);

    /// <summary>
    /// AI16.RUNTIME.4 — a rough estimate of *native/unmanaged* memory: Private Bytes minus the
    /// managed heap. Private Bytes already includes the managed heap (the CLR's segments are part of
    /// the process's committed private memory), so subtracting it leaves everything else the managed
    /// heap doesn't account for: the ONNX Runtime native allocator/arena, ImageSharp's native
    /// interop (if any), thread stacks, the JIT, loaded native libraries, etc. This is an estimate,
    /// not an exact figure — Private Bytes and <see cref="GC.GetTotalMemory"/> are sampled from two
    /// different subsystems (OS process accounting vs. the GC) a few instructions apart, so minor
    /// skew is expected. Clamped to 0 so a moment of sampling skew never logs as a negative number.
    /// </summary>
    public long NativeEstimateBytes => Math.Max(0, PrivateBytes - ManagedHeapBytes);

    public double NativeEstimateMegabytes => Math.Round(NativeEstimateBytes / (1024d * 1024d), 1);
}
