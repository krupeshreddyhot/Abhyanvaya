namespace Abhyanvaya.Application.Academic.Observability;

/// <summary>
/// AI29.1A.7 — Advisory academic health only. Never invalidates caches, repairs data, or mutates hierarchy.
/// </summary>
public interface IAcademicHealthService
{
    Task<AcademicHealthReport> GetHealthAsync(CancellationToken cancellationToken = default);
}
