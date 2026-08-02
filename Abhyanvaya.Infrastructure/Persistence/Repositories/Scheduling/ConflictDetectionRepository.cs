using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class ConflictDetectionRepository : IConflictDetectionRepository
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ConflictDetectionRepository(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConflictDetectionRun?> GetLatestRunAsync(int tenantId, int? timetableId, int? academicYearId, CancellationToken cancellationToken = default)
    {
        var query = _context.SchedulingConflictDetectionRuns
            .Where(r => r.TenantId == tenantId);

        if (timetableId.HasValue)
            query = query.Where(r => r.TimetableId == timetableId.Value);
        if (academicYearId.HasValue)
            query = query.Where(r => r.AcademicYearId == academicYearId.Value);

        var run = await query.OrderByDescending(r => r.StartedUtc).FirstOrDefaultAsync(cancellationToken);
        if (run is null) return null;

        run.Findings = await _context.SchedulingConflictFindings
            .Where(f => f.ConflictDetectionRunId == run.Id)
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.RuleCode)
            .ToListAsync(cancellationToken);
        return run;
    }

    public async Task<IReadOnlyList<ConflictDetectionRun>> ListRecentRunsAsync(int tenantId, int take, CancellationToken cancellationToken = default)
    {
        return await _context.SchedulingConflictDetectionRuns
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.StartedUtc)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ConflictDetectionRun> SaveRunAsync(ConflictDetectionRun run, IReadOnlyList<ConflictFinding> findings, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(run);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var finding in findings)
        {
            finding.ConflictDetectionRunId = run.Id;
            await _context.AddAsync(finding);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        run.Findings = findings.ToList();
        return run;
    }
}
