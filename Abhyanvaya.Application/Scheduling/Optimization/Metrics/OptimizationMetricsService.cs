using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization.Scoring;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Metrics;

public interface IOptimizationMetricsService
{
    Task<IReadOnlyList<OptimizationMetricDto>> CaptureAsync(
        int academicYearId,
        int? timetableId,
        int? departmentId,
        OptimizationContext context,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OptimizationMetricDto>> ListLatestAsync(
        int academicYearId,
        int? timetableId,
        CancellationToken cancellationToken = default);
}

/// <summary>Stores optimization metrics independently of any optimizer.</summary>
public sealed class OptimizationMetricsService : IOptimizationMetricsService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptimizationScoreCalculator _scoreCalculator;

    public OptimizationMetricsService(
        IApplicationDbContext db,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IOptimizationScoreCalculator scoreCalculator)
    {
        _db = db;
        _uow = uow;
        _currentUser = currentUser;
        _scoreCalculator = scoreCalculator;
    }

    public async Task<IReadOnlyList<OptimizationMetricDto>> CaptureAsync(
        int academicYearId,
        int? timetableId,
        int? departmentId,
        OptimizationContext context,
        CancellationToken cancellationToken = default)
    {
        var summary = _scoreCalculator.Calculate(context);
        var now = DateTime.UtcNow;
        var entities = summary.SupportingMetrics.Select(m => new OptimizationMetricSnapshot
        {
            TenantId = _currentUser.TenantId,
            SnapshotId = Guid.NewGuid(),
            TimetableId = timetableId,
            AcademicYearId = academicYearId,
            DepartmentId = departmentId,
            MetricKind = m.Kind,
            MetricName = m.Name,
            Value = m.Value,
            Unit = m.Unit,
            CapturedUtc = now,
            CreatedDate = now,
            CreatedBy = _currentUser.UserId
        }).ToList();

        await _db.AddRangeAsync(entities);
        await _uow.SaveChangesAsync(cancellationToken);

        return entities.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<OptimizationMetricDto>> ListLatestAsync(
        int academicYearId,
        int? timetableId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var query = _db.SchedulingOptimizationMetricSnapshots
            .Where(x => x.TenantId == tenantId && x.AcademicYearId == academicYearId && !x.IsDeleted);
        if (timetableId.HasValue)
            query = query.Where(x => x.TimetableId == timetableId.Value);

        var latestBatch = await query.OrderByDescending(x => x.CapturedUtc).Take(50).AsNoTracking().ToListAsync(cancellationToken);
        if (latestBatch.Count == 0) return [];

        var maxUtc = latestBatch.Max(x => x.CapturedUtc);
        return latestBatch.Where(x => x.CapturedUtc == maxUtc).Select(Map).ToList();
    }

    private static OptimizationMetricDto Map(OptimizationMetricSnapshot x) => new()
    {
        MetricKind = x.MetricKind,
        MetricName = x.MetricName,
        Value = x.Value,
        Unit = x.Unit,
        CapturedUtc = x.CapturedUtc,
        TimetableId = x.TimetableId,
        AcademicYearId = x.AcademicYearId
    };
}
