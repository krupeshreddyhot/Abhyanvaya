namespace Abhyanvaya.Infrastructure.Diagnostics;

/// <summary>
/// Singleton holder of the most recently completed (or failed) classroom recognition job's
/// diagnostics summary, so <c>/health</c> and <c>/health/ready</c> (AI15.DIAGNOSTICS.1 Task 9) can
/// read it without depending on the per-job scoped <see cref="IRecognitionPipelineDiagnostics"/>
/// instance, which no longer exists once its DI scope has been disposed.
/// </summary>
public interface IRecognitionDiagnosticsStore
{
    /// <summary>Replaces the stored "last recognition" summary. Called exactly once per job, at completion or failure.</summary>
    void RecordCompleted(RecognitionDiagnosticsSummary summary);

    /// <summary><c>null</c> until the first classroom recognition job finishes since process start.</summary>
    RecognitionDiagnosticsSummary? GetLast();
}
