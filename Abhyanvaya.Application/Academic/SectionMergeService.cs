using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionMergeService : ISectionMergeService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionLifecycleService _lifecycle;
    private readonly ISectionCapacityEngine _capacity;
    private readonly ISectionAllocationRecommendationService _allocation;
    private readonly ISectionVersioningService _versions;

    public SectionMergeService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionLifecycleService lifecycle,
        ISectionCapacityEngine capacity,
        ISectionAllocationRecommendationService allocation,
        ISectionVersioningService versions)
    {
        _db = db;
        _currentUser = currentUser;
        _lifecycle = lifecycle;
        _capacity = capacity;
        _allocation = allocation;
        _versions = versions;
    }

    public Task<SectionMergePreviewDto> ValidateAsync(SectionMergeValidateRequest request, CancellationToken cancellationToken = default)
        => PreviewAsync(request, cancellationToken);

    public async Task<SectionMergePreviewDto> PreviewAsync(SectionMergeValidateRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var sourceIds = (request.SourceSectionIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (sourceIds.Count < 1) errors.Add("Select at least one source section to merge.");

        var sources = await _db.Sections
            .Where(s => s.TenantId == _currentUser.TenantId && sourceIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        if (sources.Count != sourceIds.Count) errors.Add("One or more source sections were not found.");

        Section? target = null;
        if (request.TargetSectionId is > 0)
        {
            target = await _db.Sections.FirstOrDefaultAsync(
                s => s.Id == request.TargetSectionId && s.TenantId == _currentUser.TenantId, cancellationToken);
            if (target is null) errors.Add("Target section not found.");
            else if (sourceIds.Contains(target.Id)) errors.Add("Target section cannot also be a source.");
        }
        else if (sources.Count > 0)
        {
            errors.Add("Target section is required.");
        }

        if (sources.Count > 0 && target is not null)
        {
            var scopeMismatch = sources.Any(s =>
                s.AcademicYearId != target.AcademicYearId
                || s.CourseId != target.CourseId
                || s.GroupId != target.GroupId
                || s.SemesterId != target.SemesterId);
            if (scopeMismatch) errors.Add("All sections in a merge must share academic year, course, group, and semester.");

            foreach (var s in sources)
            {
                var status = SectionLifecycleStates.Normalize(s.Status);
                if (status is SectionLifecycleStates.Merged or SectionLifecycleStates.Split or SectionLifecycleStates.Archived)
                    errors.Add($"Section {s.SectionCode} cannot be merged (status {status}).");
            }

            var targetStatus = SectionLifecycleStates.Normalize(target.Status);
            if (targetStatus is SectionLifecycleStates.Merged or SectionLifecycleStates.Split or SectionLifecycleStates.Archived or SectionLifecycleStates.Closed)
                errors.Add($"Target section {target.SectionCode} is not merge-eligible (status {targetStatus}).");
        }

        var allIds = sourceIds.Concat(request.TargetSectionId is > 0 ? [request.TargetSectionId.Value] : Array.Empty<int>()).ToList();
        var studentCount = allIds.Count == 0
            ? 0
            : await _db.StudentSections.CountAsync(
                x => x.TenantId == _currentUser.TenantId && x.IsCurrent && allIds.Contains(x.SectionId), cancellationToken);
        var facultyCount = allIds.Count == 0
            ? 0
            : await _db.FacultySectionAssignments.CountAsync(
                x => x.TenantId == _currentUser.TenantId && x.IsCurrent && allIds.Contains(x.SectionId), cancellationToken);

        var targetMax = target?.MaximumStrength ?? 0;
        if (target is not null && studentCount > targetMax)
            warnings.Add($"Combined strength ({studentCount}) exceeds target capacity ({targetMax}).");

        if (request.TargetSectionId is > 0 && sourceIds.Count > 0)
        {
            await _allocation.RecommendForMergeAsync(new SectionAllocationMergeContext
            {
                SourceSectionIds = sourceIds,
                TargetSectionId = request.TargetSectionId.Value,
            }, cancellationToken);
        }

        return new SectionMergePreviewDto
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            CombinedStudentCount = studentCount,
            CombinedFacultyCount = facultyCount,
            TargetMaximumCapacity = targetMax,
            SourceSectionIds = sourceIds,
            TargetSectionId = request.TargetSectionId,
        };
    }

    public async Task<SectionMergeTransactionDto> CommitAsync(
        SectionMergeCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(new SectionMergeValidateRequest
        {
            SourceSectionIds = request.SourceSectionIds,
            TargetSectionId = request.TargetSectionId,
            EffectiveDate = request.EffectiveDate,
        }, cancellationToken);
        if (!preview.IsValid)
            throw new InvalidOperationException(string.Join(" ", preview.Errors));

        var effective = request.EffectiveDate == default
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : request.EffectiveDate;
        var txId = Guid.NewGuid();
        var today = effective;

        // Move current students from sources → target (history preserved via StudentSection rows).
        var sourceAssignments = await _db.StudentSections
            .Where(x => x.TenantId == _currentUser.TenantId && x.IsCurrent && request.SourceSectionIds.Contains(x.SectionId))
            .ToListAsync(cancellationToken);
        foreach (var a in sourceAssignments)
        {
            a.IsCurrent = false;
            a.EffectiveTo = today.AddDays(-1);
            a.UpdatedDate = DateTime.UtcNow;
            await _db.AddAsync(new StudentSection
            {
                StudentId = a.StudentId,
                SectionId = request.TargetSectionId,
                EffectiveFrom = today,
                IsCurrent = true,
                TransferReason = $"Merged via {txId:N}",
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });
        }

        // Faculty: end source assignments; ensure target has primary if none.
        var sourceFaculty = await _db.FacultySectionAssignments
            .Where(x => x.TenantId == _currentUser.TenantId && x.IsCurrent && request.SourceSectionIds.Contains(x.SectionId))
            .ToListAsync(cancellationToken);
        foreach (var f in sourceFaculty)
        {
            f.IsCurrent = false;
            f.EffectiveTo = today.AddDays(-1);
            f.UpdatedDate = DateTime.UtcNow;
            var exists = await _db.FacultySectionAssignments.AnyAsync(
                x => x.TenantId == _currentUser.TenantId && x.IsCurrent
                     && x.FacultyId == f.FacultyId && x.SectionId == request.TargetSectionId, cancellationToken);
            if (!exists)
            {
                await _db.AddAsync(new FacultySectionAssignment
                {
                    FacultyId = f.FacultyId,
                    SectionId = request.TargetSectionId,
                    AcademicYearId = f.AcademicYearId,
                    Role = f.Role,
                    EffectiveFrom = today,
                    IsCurrent = true,
                    TenantId = _currentUser.TenantId,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
                });
            }
        }

        foreach (var sourceId in request.SourceSectionIds)
        {
            await _db.AddAsync(new SectionLineage
            {
                ParentSectionId = sourceId,
                ChildSectionId = request.TargetSectionId,
                RelationKind = "Merge",
                TransactionId = txId,
                EffectiveDate = effective,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });
            await _lifecycle.ApplyStatusAsync(sourceId, SectionLifecycleStates.Merged, $"Merged into {request.TargetSectionId}", cancellationToken);
        }

        var tx = new SectionMergeTransaction
        {
            TransactionId = txId,
            TargetSectionId = request.TargetSectionId,
            SourceSectionIdsCsv = string.Join(",", request.SourceSectionIds),
            EffectiveDate = effective,
            Status = "Committed",
            Notes = request.Notes,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(tx);
        await _db.SaveChangesAsync(cancellationToken);

        // Capacity advisory after merge (warnings only).
        _ = await _capacity.GetOccupancyAsync(request.TargetSectionId, cancellationToken);

        var target = await _db.Sections.FirstAsync(s => s.Id == request.TargetSectionId, cancellationToken);
        var targetStrength = await _db.StudentSections.CountAsync(
            x => x.SectionId == target.Id && x.IsCurrent, cancellationToken);
        await _versions.RecordAsync(target, Domain.Academic.SectionVersionOperations.Merge, request.Notes, targetStrength, cancellationToken);
        foreach (var sourceId in request.SourceSectionIds)
        {
            var source = await _db.Sections.FirstAsync(s => s.Id == sourceId, cancellationToken);
            await _versions.RecordAsync(source, Domain.Academic.SectionVersionOperations.Merge, request.Notes, 0, cancellationToken);
        }

        return MapTx(tx);
    }

    public async Task<SectionMergeTransactionDto> ReverseAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var tx = await _db.SectionMergeTransactions.FirstOrDefaultAsync(
            t => t.TenantId == _currentUser.TenantId && t.TransactionId == transactionId, cancellationToken)
            ?? throw new KeyNotFoundException("Merge transaction not found.");
        if (tx.IsReversed) throw new InvalidOperationException("Merge transaction already reversed.");

        var sourceIds = ParseIds(tx.SourceSectionIdsCsv);
        foreach (var sourceId in sourceIds)
            await _lifecycle.ApplyStatusAsync(sourceId, SectionLifecycleStates.Active, $"Merge reversed {transactionId:N}", cancellationToken);

        tx.IsReversed = true;
        tx.ReversedUtc = DateTime.UtcNow;
        tx.Status = "Reversed";
        tx.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapTx(tx);
    }

    public async Task<IReadOnlyList<SectionMergeTransactionDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.SectionMergeTransactions.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId)
            .OrderByDescending(t => t.CreatedDate)
            .ToListAsync(cancellationToken);
        return rows.Select(MapTx).ToList();
    }

    private static SectionMergeTransactionDto MapTx(SectionMergeTransaction t) => new()
    {
        Id = t.Id,
        TransactionId = t.TransactionId,
        TargetSectionId = t.TargetSectionId,
        SourceSectionIds = ParseIds(t.SourceSectionIdsCsv),
        EffectiveDate = t.EffectiveDate,
        Status = t.Status,
        Notes = t.Notes,
        IsReversed = t.IsReversed,
    };

    private static List<int> ParseIds(string csv)
        => string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();
}
