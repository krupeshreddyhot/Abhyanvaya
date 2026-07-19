using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

public sealed class AttendanceSessionCandidateStrategy : IRecognitionCandidateStrategy
{
    private readonly IApplicationDbContext _context;

    public AttendanceSessionCandidateStrategy(IApplicationDbContext context)
    {
        _context = context;
    }

    public RecognitionCandidateScope Scope => RecognitionCandidateScope.AttendanceSession;

    public bool CanHandle(RecognitionCandidateFilter filter) =>
        filter.AttendanceSessionId.HasValue;

    public async Task<IReadOnlyList<RecognitionCandidate>> ResolveCandidatesAsync(
        RecognitionCandidateFilter filter,
        IRecognitionRepository repository,
        CancellationToken cancellationToken = default)
    {
        if (!filter.AttendanceSessionId.HasValue)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .Where(s => s.Id == filter.AttendanceSessionId.Value && s.TenantId == filter.TenantId)
            .Select(s => new { s.CourseId, s.GroupId, s.SemesterId })
            .FirstOrDefaultAsync(cancellationToken);

        if (session == null)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        var enriched = filter with
        {
            CourseId = filter.CourseId ?? session.CourseId,
            GroupId = filter.GroupId ?? session.GroupId,
            SemesterId = filter.SemesterId ?? session.SemesterId,
        };

        return await repository.GetActiveEmbeddingsAsync(enriched, cancellationToken);
    }
}

public sealed class CourseCandidateStrategy : IRecognitionCandidateStrategy
{
    public RecognitionCandidateScope Scope => RecognitionCandidateScope.Course;

    public bool CanHandle(RecognitionCandidateFilter filter) =>
        filter.CourseId.HasValue;

    public Task<IReadOnlyList<RecognitionCandidate>> ResolveCandidatesAsync(
        RecognitionCandidateFilter filter,
        IRecognitionRepository repository,
        CancellationToken cancellationToken = default) =>
        repository.GetActiveEmbeddingsAsync(filter, cancellationToken);
}

public sealed class TenantCandidateStrategy : IRecognitionCandidateStrategy
{
    public RecognitionCandidateScope Scope => RecognitionCandidateScope.Tenant;

    public bool CanHandle(RecognitionCandidateFilter filter) => true;

    public Task<IReadOnlyList<RecognitionCandidate>> ResolveCandidatesAsync(
        RecognitionCandidateFilter filter,
        IRecognitionRepository repository,
        CancellationToken cancellationToken = default) =>
        repository.GetActiveEmbeddingsAsync(filter, cancellationToken);
}

public sealed class RecognitionCandidateProvider : IRecognitionCandidateProvider
{
    private readonly IEnumerable<IRecognitionCandidateStrategy> _strategies;
    private readonly IRecognitionRepository _repository;

    public RecognitionCandidateProvider(
        IEnumerable<IRecognitionCandidateStrategy> strategies,
        IRecognitionRepository repository)
    {
        _strategies = strategies;
        _repository = repository;
    }

    public async Task<IReadOnlyList<RecognitionCandidate>> GetCandidatesAsync(
        RecognitionCandidateFilter filter,
        CancellationToken cancellationToken = default)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(filter))
            ?? _strategies.First(s => s.Scope == RecognitionCandidateScope.Tenant);

        return await strategy.ResolveCandidatesAsync(filter, _repository, cancellationToken);
    }
}
