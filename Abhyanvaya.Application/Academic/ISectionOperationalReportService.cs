namespace Abhyanvaya.Application.Academic;

/// <summary>AI29.1B — Capacity / lifecycle / merge-split / readiness report exports.</summary>
public interface ISectionOperationalReportService
{
    Task<byte[]> ExportAsync(string reportKind, string format, CancellationToken cancellationToken = default);
}
