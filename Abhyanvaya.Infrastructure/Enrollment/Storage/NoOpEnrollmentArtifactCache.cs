namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

internal sealed class NoOpEnrollmentArtifactCache : Abhyanvaya.Application.Common.Interfaces.IEnrollmentArtifactCache
{
    public Task<Abhyanvaya.Application.Enrollment.Storage.EnrollmentArtifact?> LookupAsync(
        Guid manifestId,
        string artifactType,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Abhyanvaya.Application.Enrollment.Storage.EnrollmentArtifact?>(null);

    public Task StoreAsync(
        Guid manifestId,
        string artifactType,
        Abhyanvaya.Application.Enrollment.Storage.EnrollmentArtifact artifact,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task InvalidateAsync(
        Guid manifestId,
        string? artifactType = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
