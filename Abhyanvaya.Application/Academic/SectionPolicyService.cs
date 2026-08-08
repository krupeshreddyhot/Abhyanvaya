using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionPolicyService : ISectionPolicyService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicTelemetryService _telemetry;

    public SectionPolicyService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _telemetry = telemetry;
    }

    public async Task<IReadOnlyList<SectionPolicyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.SectionPolicies.AsNoTracking()
            .Where(p => p.TenantId == _currentUser.TenantId)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<SectionPolicyDto> UpsertAsync(
        UpsertSectionPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var level = (request.ScopeLevel ?? SectionPolicyScopeLevels.Tenant).Trim();
        if (SectionPolicyScopeLevels.Specificity(level) == 0)
            throw new ArgumentException("ScopeLevel must be Tenant, Program, Course, or SectionType.");

        var collegeId = await _db.Colleges.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (collegeId <= 0) collegeId = _currentUser.TenantId;

        var q = _db.SectionPolicies.Where(p => p.TenantId == _currentUser.TenantId && p.ScopeLevel == level);
        q = level switch
        {
            SectionPolicyScopeLevels.Program => q.Where(p => p.ProgramId == request.ProgramId),
            SectionPolicyScopeLevels.Course => q.Where(p => p.CourseId == request.CourseId),
            SectionPolicyScopeLevels.SectionType => q.Where(p => p.SectionTypeCode == request.SectionTypeCode),
            _ => q.Where(p => p.ProgramId == null && p.CourseId == null && p.SectionTypeCode == null),
        };

        var entity = await q.FirstOrDefaultAsync(cancellationToken);
        if (entity is null)
        {
            entity = new SectionPolicy
            {
                CollegeId = collegeId,
                ScopeLevel = level,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            };
            await _db.AddAsync(entity);
        }

        entity.ProgramId = request.ProgramId;
        entity.CourseId = request.CourseId;
        entity.SectionTypeCode = string.IsNullOrWhiteSpace(request.SectionTypeCode) ? null : request.SectionTypeCode.Trim();
        entity.MaximumCapacity = request.MaximumCapacity;
        entity.MinimumCapacity = request.MinimumCapacity;
        entity.RecommendedCapacity = request.RecommendedCapacity;
        entity.MaximumCombinedSections = request.MaximumCombinedSections;
        entity.MaximumFaculty = request.MaximumFaculty;
        entity.MaximumRoomOccupancy = request.MaximumRoomOccupancy;
        entity.AllowMerge = request.AllowMerge;
        entity.AllowSplit = request.AllowSplit;
        entity.IsActive = request.IsActive;
        entity.Notes = request.Notes;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public Task<ResolvedSectionPolicyDto> ResolveForSectionAsync(
        int sectionId,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.SectionPolicyResolve,
            "SectionPolicy.Resolve",
            ct => ResolveCoreAsync(sectionId, ct),
            cancellationToken);

    private async Task<ResolvedSectionPolicyDto> ResolveCoreAsync(int sectionId, CancellationToken ct)
    {
        var section = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, ct)
            ?? throw new KeyNotFoundException("Section not found.");

        var programId = await _db.Courses.AsNoTracking()
            .Where(c => c.Id == section.CourseId && c.TenantId == _currentUser.TenantId)
            .Select(c => c.ProgramId)
            .FirstOrDefaultAsync(ct);

        var policies = await _db.SectionPolicies.AsNoTracking()
            .Where(p => p.TenantId == _currentUser.TenantId && p.IsActive)
            .ToListAsync(ct);

        var chain = new List<SectionPolicy>();
        var tenant = policies.FirstOrDefault(p => p.ScopeLevel == SectionPolicyScopeLevels.Tenant);
        if (tenant is not null) chain.Add(tenant);

        if (programId is > 0)
        {
            var prog = policies.FirstOrDefault(p =>
                p.ScopeLevel == SectionPolicyScopeLevels.Program && p.ProgramId == programId);
            if (prog is not null) chain.Add(prog);
        }

        var course = policies.FirstOrDefault(p =>
            p.ScopeLevel == SectionPolicyScopeLevels.Course && p.CourseId == section.CourseId);
        if (course is not null) chain.Add(course);

        var typeCode = section.SectionTypeCode;
        var type = policies.FirstOrDefault(p =>
            p.ScopeLevel == SectionPolicyScopeLevels.SectionType
            && string.Equals(p.SectionTypeCode, typeCode, StringComparison.OrdinalIgnoreCase));
        if (type is not null) chain.Add(type);

        // Merge: most specific wins (later entries override)
        int? max = null, min = null, rec = null, maxCombined = null, maxFaculty = null, maxRoom = null;
        bool? allowMerge = null, allowSplit = null;
        foreach (var p in chain.OrderBy(p => SectionPolicyScopeLevels.Specificity(p.ScopeLevel)))
        {
            if (p.MaximumCapacity is not null) max = p.MaximumCapacity;
            if (p.MinimumCapacity is not null) min = p.MinimumCapacity;
            if (p.RecommendedCapacity is not null) rec = p.RecommendedCapacity;
            if (p.MaximumCombinedSections is not null) maxCombined = p.MaximumCombinedSections;
            if (p.MaximumFaculty is not null) maxFaculty = p.MaximumFaculty;
            if (p.MaximumRoomOccupancy is not null) maxRoom = p.MaximumRoomOccupancy;
            if (p.AllowMerge is not null) allowMerge = p.AllowMerge;
            if (p.AllowSplit is not null) allowSplit = p.AllowSplit;
        }

        var warnings = new List<string>();
        if (max is > 0 && section.MaximumStrength > max)
            warnings.Add($"Section max capacity {section.MaximumStrength} exceeds policy maximum {max}.");
        if (min is > 0 && section.MaximumStrength < min)
            warnings.Add($"Section max capacity {section.MaximumStrength} is below policy minimum {min}.");

        return new ResolvedSectionPolicyDto
        {
            SectionId = sectionId,
            MaximumCapacity = max,
            MinimumCapacity = min,
            RecommendedCapacity = rec,
            MaximumCombinedSections = maxCombined,
            MaximumFaculty = maxFaculty,
            MaximumRoomOccupancy = maxRoom,
            AllowMerge = allowMerge ?? true,
            AllowSplit = allowSplit ?? true,
            AppliedScopeChain = chain.Select(p => p.ScopeLevel).ToList(),
            Warnings = warnings,
        };
    }

    private static SectionPolicyDto Map(SectionPolicy p) => new()
    {
        Id = p.Id,
        ScopeLevel = p.ScopeLevel,
        ProgramId = p.ProgramId,
        CourseId = p.CourseId,
        SectionTypeCode = p.SectionTypeCode,
        MaximumCapacity = p.MaximumCapacity,
        MinimumCapacity = p.MinimumCapacity,
        RecommendedCapacity = p.RecommendedCapacity,
        MaximumCombinedSections = p.MaximumCombinedSections,
        MaximumFaculty = p.MaximumFaculty,
        MaximumRoomOccupancy = p.MaximumRoomOccupancy,
        AllowMerge = p.AllowMerge,
        AllowSplit = p.AllowSplit,
        IsActive = p.IsActive,
        Notes = p.Notes,
    };
}
