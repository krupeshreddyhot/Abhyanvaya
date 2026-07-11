namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>Thread-safe singleton implementation of <see cref="IRecognitionDiagnosticsStore"/>.</summary>
public sealed class RecognitionDiagnosticsStore : IRecognitionDiagnosticsStore
{
    private readonly object _gate = new();
    private RecognitionDiagnosticsSummary? _last;

    public void RecordCompleted(RecognitionDiagnosticsSummary summary)
    {
        lock (_gate)
        {
            _last = summary;
        }
    }

    public RecognitionDiagnosticsSummary? GetLast()
    {
        lock (_gate)
        {
            return _last;
        }
    }
}
