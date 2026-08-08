using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionLifecycleService : ISectionLifecycleService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionVersioningService _versions;

    public SectionLifecycleService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionVersioningService versions)
    {
        _db = db;
        _currentUser = currentUser;
        _versions = versions;
    }

    public IReadOnlyList<string> GetAllStates() => SectionLifecycleStates.All;

    public IReadOnlyList<string> GetAllowedTransitions(string currentStatus)
        => SectionLifecycleStateMachine.GetAllowedTargets(currentStatus);

    public IReadOnlyList<SectionTypeOptionDto> GetSectionTypes()
        => SectionTypeCodes.Defaults
            .Select(c => new SectionTypeOptionDto
            {
                Code = c,
                DisplayName = c switch
                {
                    SectionTypeCodes.SpecialBatch => "Special Batch",
                    _ => c
                }
            })
            .ToList();

    public async Task<SectionDto> TransitionAsync(
        int sectionId,
        SectionLifecycleTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApplyStatusAsync(sectionId, request.TargetStatus, request.Reason, cancellationToken);
        var entity = await _db.Sections.AsNoTracking()
            .FirstAsync(s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, cancellationToken);
        var strength = await _db.StudentSections.CountAsync(
            x => x.SectionId == sectionId && x.IsCurrent, cancellationToken);
        return new SectionDto
        {
            Id = entity.Id,
            CollegeId = entity.CollegeId,
            AcademicYearId = entity.AcademicYearId,
            CourseId = entity.CourseId,
            GroupId = entity.GroupId,
            SemesterId = entity.SemesterId,
            SectionCode = entity.SectionCode,
            SectionName = entity.SectionName,
            DisplayOrder = entity.DisplayOrder,
            MaximumStrength = entity.MaximumStrength,
            Status = entity.Status,
            CurrentStrength = strength,
            RemainingCapacity = Math.Max(0, entity.MaximumStrength - strength),
            SectionTypeCode = entity.SectionTypeCode,
            MinimumCapacity = entity.MinimumCapacity,
            RecommendedCapacity = entity.RecommendedCapacity,
            ReservedSeats = entity.ReservedSeats,
            WaitingListCount = entity.WaitingListCount,
            ParentSectionId = entity.ParentSectionId,
            SectionGroupId = entity.SectionGroupId,
        };
    }

    public async Task ApplyStatusAsync(
        int sectionId,
        string targetStatus,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Sections.FirstOrDefaultAsync(
            s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        var from = SectionLifecycleStates.Normalize(entity.Status);
        var to = SectionLifecycleStates.Normalize(targetStatus);
        SectionLifecycleStateMachine.EnsureCanTransition(from, to);

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return;

        entity.Status = to;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;

        await _db.AddAsync(new SectionLifecycleTransition
        {
            SectionId = sectionId,
            FromStatus = from,
            ToStatus = to,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            TransitionedUtc = DateTime.UtcNow,
            TransitionedByUserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        });

        await _db.SaveChangesAsync(cancellationToken);

        var strength = await _db.StudentSections.CountAsync(
            x => x.SectionId == sectionId && x.IsCurrent, cancellationToken);
        await _versions.RecordAsync(entity, SectionVersionOperations.LifecycleChange, reason, strength, cancellationToken);
    }

    public async Task<IReadOnlyList<SectionLifecycleHistoryDto>> GetHistoryAsync(
        int sectionId,
        CancellationToken cancellationToken = default)
    {
        var ok = await _db.Sections.AsNoTracking()
            .AnyAsync(s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, cancellationToken);
        if (!ok) throw new KeyNotFoundException("Section not found.");

        return await _db.SectionLifecycleTransitions.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId && t.SectionId == sectionId)
            .OrderByDescending(t => t.TransitionedUtc)
            .Select(t => new SectionLifecycleHistoryDto
            {
                Id = t.Id,
                SectionId = t.SectionId,
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
                Reason = t.Reason,
                TransitionedUtc = t.TransitionedUtc,
                TransitionedByUserId = t.TransitionedByUserId,
            })
            .ToListAsync(cancellationToken);
    }
}
