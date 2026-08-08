using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionGroupService : ISectionGroupService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SectionGroupService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<SectionGroupDto>> ListAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.SectionGroups.AsNoTracking().Where(g => g.TenantId == _currentUser.TenantId);
        if (academicYearId is > 0) q = q.Where(g => g.AcademicYearId == academicYearId);
        if (semesterId is > 0) q = q.Where(g => g.SemesterId == semesterId);
        var groups = await q.OrderBy(g => g.GroupCode).ToListAsync(cancellationToken);
        var result = new List<SectionGroupDto>();
        foreach (var g in groups)
            result.Add(await MapAsync(g, cancellationToken));
        return result;
    }

    public async Task<SectionGroupDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var g = await _db.SectionGroups.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUser.TenantId, cancellationToken);
        return g is null ? null : await MapAsync(g, cancellationToken);
    }

    public async Task<SectionGroupDto> CreateAsync(CreateSectionGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.GroupCode) || string.IsNullOrWhiteSpace(request.GroupName))
            throw new ArgumentException("Group code and name are required.");

        var sectionIds = (request.SectionIds ?? []).Where(id => id > 0).Distinct().ToList();
        var sections = await _db.Sections
            .Where(s => s.TenantId == _currentUser.TenantId && sectionIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        if (sections.Count != sectionIds.Count)
            throw new InvalidOperationException("One or more sections were not found.");
        if (sections.Any(s =>
                s.AcademicYearId != request.AcademicYearId
                || s.CourseId != request.CourseId
                || s.GroupId != request.GroupId
                || s.SemesterId != request.SemesterId))
            throw new InvalidOperationException("All member sections must match the section group scope.");

        var collegeId = sections.FirstOrDefault()?.CollegeId
            ?? await _db.Colleges.AsNoTracking()
                .Where(c => c.TenantId == _currentUser.TenantId)
                .OrderBy(c => c.Id)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

        var entity = new SectionGroup
        {
            CollegeId = collegeId > 0 ? collegeId : _currentUser.TenantId,
            AcademicYearId = request.AcademicYearId,
            CourseId = request.CourseId,
            GroupId = request.GroupId,
            SemesterId = request.SemesterId,
            GroupCode = request.GroupCode.Trim().ToUpperInvariant(),
            GroupName = request.GroupName.Trim(),
            Status = "Active",
            Notes = request.Notes,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var s in sections)
        {
            await _db.AddAsync(new SectionGroupMember
            {
                SectionGroupId = entity.Id,
                SectionId = s.Id,
                EffectiveFrom = from,
                IsCurrent = true,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });
            s.SectionGroupId = entity.Id;
            s.UpdatedDate = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task<SectionGroupDto> UpdateMembersAsync(
        int id,
        UpdateSectionGroupMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await _db.SectionGroups.FirstOrDefaultAsync(
            g => g.Id == id && g.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section group not found.");

        var desired = (request.SectionIds ?? []).Where(x => x > 0).Distinct().ToList();
        var sections = await _db.Sections
            .Where(s => s.TenantId == _currentUser.TenantId && desired.Contains(s.Id))
            .ToListAsync(cancellationToken);
        if (sections.Count != desired.Count)
            throw new InvalidOperationException("One or more sections were not found.");
        if (sections.Any(s =>
                s.AcademicYearId != group.AcademicYearId
                || s.CourseId != group.CourseId
                || s.GroupId != group.GroupId
                || s.SemesterId != group.SemesterId))
            throw new InvalidOperationException("Member sections must match section group scope.");

        var from = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var current = await _db.SectionGroupMembers
            .Where(m => m.SectionGroupId == id && m.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var m in current.Where(m => !desired.Contains(m.SectionId)))
        {
            m.IsCurrent = false;
            m.EffectiveTo = from.AddDays(-1);
            m.UpdatedDate = DateTime.UtcNow;
            var sec = await _db.Sections.FirstOrDefaultAsync(s => s.Id == m.SectionId, cancellationToken);
            if (sec is not null && sec.SectionGroupId == id) sec.SectionGroupId = null;
        }

        var currentIds = current.Where(m => m.IsCurrent).Select(m => m.SectionId).ToHashSet();
        foreach (var s in sections.Where(s => !currentIds.Contains(s.Id)))
        {
            await _db.AddAsync(new SectionGroupMember
            {
                SectionGroupId = id,
                SectionId = s.Id,
                EffectiveFrom = from,
                IsCurrent = true,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });
            s.SectionGroupId = id;
            s.UpdatedDate = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(id, cancellationToken))!;
    }

    private async Task<SectionGroupDto> MapAsync(SectionGroup g, CancellationToken ct)
    {
        var memberIds = await _db.SectionGroupMembers.AsNoTracking()
            .Where(m => m.SectionGroupId == g.Id && m.IsCurrent)
            .Select(m => m.SectionId)
            .ToListAsync(ct);
        return new SectionGroupDto
        {
            Id = g.Id,
            CollegeId = g.CollegeId,
            AcademicYearId = g.AcademicYearId,
            CourseId = g.CourseId,
            GroupId = g.GroupId,
            SemesterId = g.SemesterId,
            GroupCode = g.GroupCode,
            GroupName = g.GroupName,
            Status = g.Status,
            Notes = g.Notes,
            CurrentSectionIds = memberIds,
        };
    }
}
