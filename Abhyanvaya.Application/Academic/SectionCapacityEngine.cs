using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionCapacityEngine : ISectionCapacityEngine
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionVersioningService _versions;
    private readonly ISectionCapacityHistoryService _capacityHistory;

    public SectionCapacityEngine(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionVersioningService versions,
        ISectionCapacityHistoryService capacityHistory)
    {
        _db = db;
        _currentUser = currentUser;
        _versions = versions;
        _capacityHistory = capacityHistory;
    }

    public SectionCapacitySnapshotDto Calculate(Section section, int currentStrength, TenantSectionCapacityPolicy? policy)
    {
        var max = Math.Max(0, section.MaximumStrength);
        var min = Math.Max(0, section.MinimumCapacity);
        var recommended = section.RecommendedCapacity > 0 ? section.RecommendedCapacity : max;
        var reserved = Math.Max(0, section.ReservedSeats);
        var waiting = Math.Max(0, section.WaitingListCount);
        var available = Math.Max(0, max - currentStrength - reserved);
        var occupancy = max <= 0 ? 0 : Math.Round(100.0 * currentStrength / max, 2);

        var warningPct = policy?.WarningPercent ?? 90;
        var underPct = policy?.UnderCapacityPercent ?? 40;
        var autoWarn = policy?.AutoWarningEnabled ?? true;
        var soft = policy?.SoftLimitEnabled ?? true;
        var hard = policy?.EnforceHardLimit ?? true;

        var warnings = new List<string>();
        var over = currentStrength > max;
        var under = max > 0 && occupancy <= underPct && currentStrength > 0;
        var hardBreached = hard && currentStrength > max;
        var nearLimit = autoWarn && max > 0 && occupancy >= warningPct && !over;

        if (over) warnings.Add($"Over capacity: {currentStrength}/{max}.");
        if (under) warnings.Add($"Under capacity: occupancy {occupancy}% (threshold {underPct}%).");
        if (nearLimit) warnings.Add($"Approaching capacity warning threshold ({warningPct}%).");
        if (soft && over) warnings.Add("Soft limit: over-capacity allowed with warning only (no auto student movement).");
        if (hardBreached) warnings.Add("Hard limit breached.");

        var status = hardBreached ? "OverCapacity"
            : over ? "OverCapacity"
            : nearLimit ? "Warning"
            : under ? "UnderCapacity"
            : "Ok";

        return new SectionCapacitySnapshotDto
        {
            SectionId = section.Id,
            SectionCode = section.SectionCode,
            SectionName = section.SectionName,
            LifecycleStatus = SectionLifecycleStates.Normalize(section.Status),
            SectionTypeCode = string.IsNullOrWhiteSpace(section.SectionTypeCode) ? SectionTypeCodes.Regular : section.SectionTypeCode,
            MaximumCapacity = max,
            MinimumCapacity = min,
            RecommendedCapacity = recommended,
            CurrentStrength = currentStrength,
            ReservedSeats = reserved,
            WaitingList = waiting,
            AvailableSeats = available,
            OccupancyPercent = occupancy,
            CapacityStatus = status,
            IsOverCapacity = over,
            IsUnderCapacity = under,
            IsHardLimitBreached = hardBreached,
            HasWarning = warnings.Count > 0,
            Warnings = warnings,
        };
    }

    public async Task<SectionCapacitySnapshotDto> GetOccupancyAsync(int sectionId, CancellationToken cancellationToken = default)
    {
        var list = await GetOccupancyAsync([sectionId], cancellationToken: cancellationToken);
        return list.FirstOrDefault() ?? throw new KeyNotFoundException("Section not found.");
    }

    public async Task<IReadOnlyList<SectionCapacitySnapshotDto>> GetOccupancyAsync(
        IEnumerable<int>? sectionIds = null,
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Sections.AsNoTracking().Where(s => s.TenantId == _currentUser.TenantId);
        if (sectionIds is not null)
        {
            var ids = sectionIds.ToList();
            if (ids.Count > 0) q = q.Where(s => ids.Contains(s.Id));
        }
        if (academicYearId is > 0) q = q.Where(s => s.AcademicYearId == academicYearId);
        if (semesterId is > 0) q = q.Where(s => s.SemesterId == semesterId);

        var sections = await q.OrderBy(s => s.DisplayOrder).ThenBy(s => s.SectionCode).ToListAsync(cancellationToken);
        if (sections.Count == 0) return [];

        var policy = await GetPolicyEntityAsync(cancellationToken);
        var strength = await LoadStrengthMapAsync(sections.Select(s => s.Id).ToList(), cancellationToken);
        return sections.Select(s => Calculate(s, strength.GetValueOrDefault(s.Id), policy)).ToList();
    }

    public async Task<SectionCapacitySummaryDto> GetCapacitySummaryAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await GetOccupancyAsync(null, academicYearId, semesterId, cancellationToken);
        if (rows.Count == 0)
        {
            return new SectionCapacitySummaryDto();
        }

        return new SectionCapacitySummaryDto
        {
            SectionCount = rows.Count,
            TotalMaximumCapacity = rows.Sum(r => r.MaximumCapacity),
            TotalCurrentStrength = rows.Sum(r => r.CurrentStrength),
            TotalAvailableSeats = rows.Sum(r => r.AvailableSeats),
            OverCapacityCount = rows.Count(r => r.IsOverCapacity),
            UnderCapacityCount = rows.Count(r => r.IsUnderCapacity),
            WarningCount = rows.Count(r => r.HasWarning),
            AverageOccupancyPercent = Math.Round(rows.Average(r => r.OccupancyPercent), 2),
        };
    }

    public async Task<IReadOnlyList<SectionCapacitySnapshotDto>> GetOverCapacityAsync(CancellationToken cancellationToken = default)
        => (await GetOccupancyAsync(cancellationToken: cancellationToken)).Where(x => x.IsOverCapacity).ToList();

    public async Task<IReadOnlyList<SectionCapacitySnapshotDto>> GetUnderCapacityAsync(CancellationToken cancellationToken = default)
        => (await GetOccupancyAsync(cancellationToken: cancellationToken)).Where(x => x.IsUnderCapacity).ToList();

    public async Task UpdateCapacityAsync(int sectionId, UpdateSectionCapacityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MaximumCapacity < 0 || request.MinimumCapacity < 0 || request.ReservedSeats < 0 || request.WaitingListCount < 0)
            throw new ArgumentException("Capacity values cannot be negative.");
        if (request.MinimumCapacity > request.MaximumCapacity && request.MaximumCapacity > 0)
            throw new ArgumentException("Minimum capacity cannot exceed maximum capacity.");

        var entity = await _db.Sections.FirstOrDefaultAsync(
            s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        entity.MaximumStrength = request.MaximumCapacity;
        entity.MinimumCapacity = request.MinimumCapacity;
        entity.RecommendedCapacity = request.RecommendedCapacity > 0 ? request.RecommendedCapacity : request.MaximumCapacity;
        entity.ReservedSeats = request.ReservedSeats;
        entity.WaitingListCount = request.WaitingListCount;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
        await _db.SaveChangesAsync(cancellationToken);

        var strength = await _db.StudentSections.CountAsync(x => x.SectionId == sectionId && x.IsCurrent, cancellationToken);
        await _capacityHistory.RecordAsync(entity, strength, "CapacityChange", cancellationToken);
        await _versions.RecordAsync(entity, Domain.Academic.SectionVersionOperations.CapacityChange, "CapacityChange", strength, cancellationToken);
    }

    public async Task<TenantSectionCapacityPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var p = await GetPolicyEntityAsync(cancellationToken) ?? new TenantSectionCapacityPolicy();
        return MapPolicy(p);
    }

    public async Task<TenantSectionCapacityPolicyDto> UpsertPolicyAsync(
        UpsertTenantSectionCapacityPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WarningPercent is < 1 or > 100)
            throw new ArgumentException("Warning percent must be between 1 and 100.");
        if (request.UnderCapacityPercent is < 0 or > 100)
            throw new ArgumentException("Under-capacity percent must be between 0 and 100.");

        var collegeId = await _db.Colleges.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (collegeId <= 0) collegeId = _currentUser.TenantId;

        var entity = await _db.TenantSectionCapacityPolicies
            .FirstOrDefaultAsync(p => p.TenantId == _currentUser.TenantId, cancellationToken);

        if (entity is null)
        {
            entity = new TenantSectionCapacityPolicy
            {
                CollegeId = collegeId,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            };
            await _db.AddAsync(entity);
        }

        entity.EnforceHardLimit = request.EnforceHardLimit;
        entity.SoftLimitEnabled = request.SoftLimitEnabled;
        entity.WarningPercent = request.WarningPercent;
        entity.AutoWarningEnabled = request.AutoWarningEnabled;
        entity.UnderCapacityPercent = request.UnderCapacityPercent;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
        await _db.SaveChangesAsync(cancellationToken);
        return MapPolicy(entity);
    }

    public async Task<SectionCapacityAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await GetOccupancyAsync(cancellationToken: cancellationToken);
        var totalMax = rows.Sum(r => r.MaximumCapacity);
        var totalCur = rows.Sum(r => r.CurrentStrength);
        var mergeCandidates = rows
            .Where(r => r.IsUnderCapacity && r.CurrentStrength > 0)
            .OrderBy(r => r.OccupancyPercent)
            .Take(20)
            .ToList();
        var splitCandidates = rows
            .Where(r => r.IsOverCapacity || r.OccupancyPercent >= 95)
            .OrderByDescending(r => r.OccupancyPercent)
            .Take(20)
            .ToList();

        var growth = await _db.SectionLifecycleTransitions.AsNoTracking()
            .CountAsync(t => t.TenantId == _currentUser.TenantId
                             && t.ToStatus == SectionLifecycleStates.Active
                             && t.TransitionedUtc >= DateTime.UtcNow.AddDays(-90), cancellationToken);

        var trend = new List<SectionCapacityTrendPointDto>
        {
            new()
            {
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                AverageOccupancyPercent = rows.Count == 0 ? 0 : Math.Round(rows.Average(r => r.OccupancyPercent), 2),
                TotalCurrentStrength = totalCur,
            }
        };

        return new SectionCapacityAnalyticsDto
        {
            AverageOccupancyPercent = rows.Count == 0 ? 0 : Math.Round(rows.Average(r => r.OccupancyPercent), 2),
            UtilizationPercent = totalMax <= 0 ? 0 : Math.Round(100.0 * totalCur / totalMax, 2),
            SectionGrowthCount = growth,
            MergeCandidates = mergeCandidates,
            SplitCandidates = splitCandidates,
            CapacityTrend = trend,
        };
    }

    public async Task EnsureCanAcceptStudentAsync(Section section, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyEntityAsync(cancellationToken);
        var count = await _db.StudentSections.CountAsync(x => x.SectionId == section.Id && x.IsCurrent, cancellationToken);
        var snap = Calculate(section, count, policy);
        if (policy?.EnforceHardLimit != false && count >= section.MaximumStrength)
            throw new InvalidOperationException(
                $"Section {section.SectionCode} is at maximum capacity ({section.MaximumStrength}).");
        _ = snap; // warnings are advisory; assignment blocked only on hard limit
    }

    private async Task<TenantSectionCapacityPolicy?> GetPolicyEntityAsync(CancellationToken ct)
        => await _db.TenantSectionCapacityPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == _currentUser.TenantId, ct);

    private async Task<Dictionary<int, int>> LoadStrengthMapAsync(List<int> sectionIds, CancellationToken ct)
        => await _db.StudentSections.AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId && x.IsCurrent && sectionIds.Contains(x.SectionId))
            .GroupBy(x => x.SectionId)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

    private static TenantSectionCapacityPolicyDto MapPolicy(TenantSectionCapacityPolicy p) => new()
    {
        Id = p.Id,
        EnforceHardLimit = p.EnforceHardLimit,
        SoftLimitEnabled = p.SoftLimitEnabled,
        WarningPercent = p.WarningPercent,
        AutoWarningEnabled = p.AutoWarningEnabled,
        UnderCapacityPercent = p.UnderCapacityPercent,
    };
}
