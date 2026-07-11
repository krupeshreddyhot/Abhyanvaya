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
    int ThreadId)
{
    public static RecognitionMemorySnapshot Capture()
    {
        // GC.GetTotalMemory(false): read the last-known heap size without forcing a collection —
        // a forced collection would itself distort the very measurement being taken.
        var managedHeapBytes = GC.GetTotalMemory(false);
        var workingSetBytes = Environment.WorkingSet;

        long privateBytes;
        using (var process = Process.GetCurrentProcess())
        {
            privateBytes = process.PrivateMemorySize64;
        }

        return new RecognitionMemorySnapshot(
            TimestampUtc: DateTime.UtcNow,
            ManagedHeapBytes: managedHeapBytes,
            WorkingSetBytes: workingSetBytes,
            PrivateBytes: privateBytes,
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            ThreadId: Environment.CurrentManagedThreadId);
    }

    public double ManagedHeapMegabytes => Math.Round(ManagedHeapBytes / (1024d * 1024d), 1);
    public double WorkingSetMegabytes => Math.Round(WorkingSetBytes / (1024d * 1024d), 1);
    public double PrivateMegabytes => Math.Round(PrivateBytes / (1024d * 1024d), 1);
}
