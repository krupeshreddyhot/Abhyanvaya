using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionCapacityHistoryService : ISectionCapacityHistoryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SectionCapacityHistoryService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task RecordAsync(
        Section section,
        int currentStrength,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var max = Math.Max(0, section.MaximumStrength);
        var occupancy = max <= 0 ? 0 : Math.Round(100.0 * currentStrength / max, 2);
        await _db.AddAsync(new SectionCapacityHistory
        {
            SectionId = section.Id,
            MaximumCapacity = max,
            MinimumCapacity = section.MinimumCapacity,
            CurrentStrength = currentStrength,
            ReservedSeats = section.ReservedSeats,
            OccupancyPercent = occupancy,
            RecordedDate = DateTime.UtcNow,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            RecordedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SectionCapacityHistoryDto>> GetCapacityHistoryAsync(
        int sectionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.SectionCapacityHistories.AsNoTracking()
            .Where(h => h.TenantId == _currentUser.TenantId && h.SectionId == sectionId)
            .OrderByDescending(h => h.RecordedDate)
            .Select(h => new SectionCapacityHistoryDto
            {
                Id = h.Id,
                SectionId = h.SectionId,
                MaximumCapacity = h.MaximumCapacity,
                MinimumCapacity = h.MinimumCapacity,
                CurrentStrength = h.CurrentStrength,
                ReservedSeats = h.ReservedSeats,
                OccupancyPercent = h.OccupancyPercent,
                RecordedDate = h.RecordedDate,
                Reason = h.Reason,
            })
            .ToListAsync(cancellationToken);
    }
}
