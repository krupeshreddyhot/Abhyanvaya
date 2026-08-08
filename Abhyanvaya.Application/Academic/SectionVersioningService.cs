using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionVersioningService : ISectionVersioningService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SectionVersioningService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SectionVersionDto> RecordAsync(
        Section section,
        string operation,
        string? reason,
        int currentStrength,
        CancellationToken cancellationToken = default)
    {
        var last = await _db.SectionVersions.AsNoTracking()
            .Where(v => v.TenantId == _currentUser.TenantId && v.SectionId == section.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var max = Math.Max(0, section.MaximumStrength);
        var occupancy = max <= 0 ? 0 : Math.Round(100.0 * currentStrength / max, 2);

        var version = new SectionVersion
        {
            SectionId = section.Id,
            VersionNumber = (last?.VersionNumber ?? 0) + 1,
            VersionDate = DateTime.UtcNow,
            ChangedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Operation = operation.Trim(),
            PreviousVersionId = last?.Id,
            SectionCode = section.SectionCode,
            SectionName = section.SectionName,
            Status = section.Status,
            SectionTypeCode = section.SectionTypeCode,
            MaximumCapacity = max,
            MinimumCapacity = section.MinimumCapacity,
            RecommendedCapacity = section.RecommendedCapacity,
            ReservedSeats = section.ReservedSeats,
            WaitingListCount = section.WaitingListCount,
            CurrentStrength = currentStrength,
            OccupancyPercent = occupancy,
            ParentSectionId = section.ParentSectionId,
            SectionGroupId = section.SectionGroupId,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };

        await _db.AddAsync(version);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(version);
    }

    public async Task<IReadOnlyList<SectionVersionDto>> GetVersionsAsync(
        int sectionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.SectionVersions.AsNoTracking()
            .Where(v => v.TenantId == _currentUser.TenantId && v.SectionId == sectionId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    private static SectionVersionDto Map(SectionVersion v) => new()
    {
        Id = v.Id,
        SectionId = v.SectionId,
        VersionNumber = v.VersionNumber,
        VersionDate = v.VersionDate,
        ChangedBy = v.ChangedBy,
        Reason = v.Reason,
        Operation = v.Operation,
        PreviousVersionId = v.PreviousVersionId,
        SectionCode = v.SectionCode,
        SectionName = v.SectionName,
        Status = v.Status,
        SectionTypeCode = v.SectionTypeCode,
        MaximumCapacity = v.MaximumCapacity,
        MinimumCapacity = v.MinimumCapacity,
        RecommendedCapacity = v.RecommendedCapacity,
        ReservedSeats = v.ReservedSeats,
        WaitingListCount = v.WaitingListCount,
        CurrentStrength = v.CurrentStrength,
        OccupancyPercent = v.OccupancyPercent,
    };
}
