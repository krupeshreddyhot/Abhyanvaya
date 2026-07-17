using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Infrastructure.Enrollment.Validation;

internal sealed class NoOpValidationCache : IValidationCache
{
    public Task<EnrollmentValidationArtifact?> LookupAsync(string cacheKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<EnrollmentValidationArtifact?>(null);

    public Task StoreAsync(string cacheKey, EnrollmentValidationArtifact artifact, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
