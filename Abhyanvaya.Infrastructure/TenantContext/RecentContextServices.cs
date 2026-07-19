using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.TenantContext;

public sealed class RecentContextRepository : IRecentContextRepository
{
    private readonly IContextPersistenceProvider _persistence;

    public RecentContextRepository(IContextPersistenceProvider persistence)
    {
        _persistence = persistence;
    }

    public Task<IReadOnlyList<RecentCollegeEntry>> GetRecentCollegesAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        LoadListAsync(userId, cancellationToken);

    public Task SaveRecentCollegesAsync(
        int userId,
        IReadOnlyList<RecentCollegeEntry> entries,
        CancellationToken cancellationToken = default) =>
        _persistence.SaveAsync(
            BuildKey(userId),
            entries.ToList(),
            expiry: null,
            cancellationToken);

    private async Task<IReadOnlyList<RecentCollegeEntry>> LoadListAsync(int userId, CancellationToken cancellationToken)
    {
        var stored = await _persistence.LoadAsync<List<RecentCollegeEntry>>(BuildKey(userId), cancellationToken);
        return stored ?? [];
    }

    internal static string BuildKey(int userId) => $"recent-context:v1:{userId}";
}

public sealed class RecentContextService : IRecentContextService
{
    private readonly IRecentContextRepository _repository;
    private readonly IOptions<ContextPlatformOptions> _options;

    public RecentContextService(
        IRecentContextRepository repository,
        IOptions<ContextPlatformOptions> options)
    {
        _repository = repository;
        _options = options;
    }

    public async Task RecordCollegeSelectionAsync(
        int userId,
        AvailableCollegeDto college,
        CancellationToken cancellationToken = default)
    {
        var existing = (await _repository.GetRecentCollegesAsync(userId, cancellationToken)).ToList();
        existing.RemoveAll(e => e.CollegeId == college.Id);

        existing.Insert(0, new RecentCollegeEntry
        {
            CollegeId = college.Id,
            TenantId = college.TenantId,
            Name = college.Name,
            Code = college.Code,
            SelectedUtc = DateTime.UtcNow,
        });

        var max = _options.Value.RecentCollegesMax;
        if (existing.Count > max)
        {
            existing = existing.Take(max).ToList();
        }

        await _repository.SaveRecentCollegesAsync(userId, existing, cancellationToken);
    }

    public Task<IReadOnlyList<RecentCollegeEntry>> GetRecentCollegesAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        _repository.GetRecentCollegesAsync(userId, cancellationToken);
}
