using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public interface IAllocationScenarioQueryService
{
    Task<AllocationScenarioDetailDto?> GetDetailAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

public sealed class AllocationScenarioQueryService : IAllocationScenarioQueryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAllocationGovernanceService _governance;
    private readonly IAllocationScenarioVersionService _versions;
    private readonly ISectionAllocationContextBuilder _builder;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationScenarioQueryService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAllocationGovernanceService governance,
        IAllocationScenarioVersionService versions,
        ISectionAllocationContextBuilder builder)
    {
        _db = db;
        _currentUser = currentUser;
        _governance = governance;
        _versions = versions;
        _builder = builder;
    }

    public async Task<AllocationScenarioDetailDto?> GetDetailAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var row = await _db.AllocationEngineScenarios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == scenarioId, cancellationToken);
        if (row is null) return null;
        var scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts) ?? new AllocationScenario();
        var governance = await _governance.EvaluateAsync(scenarioId, cancellationToken);
        var versions = await _versions.ListAsync(scenarioId, cancellationToken);

        string? currentContextVersion = null;
        var contextCurrent = true;
        if (row.AcademicYearId > 0 && row.CourseId > 0 && row.GroupId > 0 && row.SemesterId > 0)
        {
            try
            {
                var current = await _builder.BuildAsync(new AllocationScopeRequest
                {
                    AcademicYearId = row.AcademicYearId,
                    CourseId = row.CourseId,
                    GroupId = row.GroupId,
                    SemesterId = row.SemesterId,
                }, cancellationToken);
                currentContextVersion = current.ContextId.ToString("N")[..8];
                contextCurrent = string.Equals(current.Checksum, row.ContextChecksum, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                contextCurrent = !governance.ContextStale;
                currentContextVersion = governance.CurrentContextVersion;
            }
        }

        return new AllocationScenarioDetailDto
        {
            ScenarioId = row.ScenarioId,
            SessionId = row.SessionId,
            LifecycleStatus = row.LifecycleStatus,
            Status = row.Status,
            CurrentVersionNumber = row.CurrentVersionNumber,
            TotalScore = row.TotalScore,
            ContextChecksum = row.ContextChecksum,
            ContextVersion = row.ContextVersion,
            CurrentContextVersion = currentContextVersion ?? row.ContextVersion,
            ContextCurrent = contextCurrent,
            ScenarioChecksum = row.ScenarioChecksum,
            GeneratedAt = row.GeneratedAt,
            Scenario = scenario,
            Governance = governance,
            Versions = versions,
        };
    }
}
