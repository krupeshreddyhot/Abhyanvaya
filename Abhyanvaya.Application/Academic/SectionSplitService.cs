using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionSplitService : ISectionSplitService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionLifecycleService _lifecycle;
    private readonly ISectionAllocationRecommendationService _allocation;
    private readonly ISectionVersioningService _versions;

    public SectionSplitService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionLifecycleService lifecycle,
        ISectionAllocationRecommendationService allocation,
        ISectionVersioningService versions)
    {
        _db = db;
        _currentUser = currentUser;
        _lifecycle = lifecycle;
        _allocation = allocation;
        _versions = versions;
    }

    public Task<SectionSplitPreviewDto> ValidateAsync(SectionSplitValidateRequest request, CancellationToken cancellationToken = default)
        => PreviewAsync(request, cancellationToken);

    public async Task<SectionSplitPreviewDto> PreviewAsync(SectionSplitValidateRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var childCount = request.ChildCount < 2 ? 2 : request.ChildCount;
        if (childCount > 10) errors.Add("Split supports at most 10 child sections.");

        var source = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SourceSectionId && s.TenantId == _currentUser.TenantId, cancellationToken);
        if (source is null)
        {
            errors.Add("Source section not found.");
            return new SectionSplitPreviewDto { IsValid = false, Errors = errors, SourceSectionId = request.SourceSectionId };
        }

        var status = SectionLifecycleStates.Normalize(source.Status);
        if (status is SectionLifecycleStates.Merged or SectionLifecycleStates.Split or SectionLifecycleStates.Archived or SectionLifecycleStates.Closed)
            errors.Add($"Section {source.SectionCode} cannot be split (status {status}).");

        var studentCount = await _db.StudentSections.CountAsync(
            x => x.SectionId == source.Id && x.IsCurrent, cancellationToken);

        var strategy = string.IsNullOrWhiteSpace(request.StrategyCode) ? "Manual" : request.StrategyCode.Trim();
        await _allocation.RecommendForSplitAsync(new SectionAllocationSplitContext
        {
            SourceSectionId = source.Id,
            StrategyCode = strategy,
            ChildCount = childCount,
            StudentCount = studentCount,
        }, cancellationToken);

        var perChild = childCount == 0 ? 0 : studentCount / childCount;
        var remainder = childCount == 0 ? 0 : studentCount % childCount;
        var capacityEach = Math.Max(1, (int)Math.Ceiling(source.MaximumStrength / (double)childCount));
        var children = new List<SectionSplitChildPlanDto>();
        for (var i = 0; i < childCount; i++)
        {
            var planned = perChild + (i < remainder ? 1 : 0);
            children.Add(new SectionSplitChildPlanDto
            {
                ProposedCode = $"{source.SectionCode}-{ (char)('A' + i) }",
                ProposedName = $"{source.SectionName} ({(char)('A' + i)})",
                ProposedCapacity = capacityEach,
                PlannedStudentCount = planned,
            });
        }

        if (strategy != "Manual")
            warnings.Add($"Strategy '{strategy}' will be applied by AI29.1C; commit creates child sections only (no auto student movement).");

        return new SectionSplitPreviewDto
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            SourceSectionId = source.Id,
            SourceStudentCount = studentCount,
            StrategyCode = strategy,
            ProposedChildren = children,
        };
    }

    public async Task<SectionSplitTransactionDto> CommitAsync(
        SectionSplitCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(new SectionSplitValidateRequest
        {
            SourceSectionId = request.SourceSectionId,
            ChildCount = request.Children?.Count > 0 ? request.Children.Count : 2,
            StrategyCode = request.StrategyCode,
            EffectiveDate = request.EffectiveDate,
        }, cancellationToken);
        if (!preview.IsValid)
            throw new InvalidOperationException(string.Join(" ", preview.Errors));

        var source = await _db.Sections.FirstAsync(
            s => s.Id == request.SourceSectionId && s.TenantId == _currentUser.TenantId, cancellationToken);
        var plans = (request.Children?.Count > 0 ? request.Children : preview.ProposedChildren).ToList();
        if (plans.Count < 2) throw new InvalidOperationException("Split requires at least two child sections.");

        var effective = request.EffectiveDate == default
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : request.EffectiveDate;
        var txId = Guid.NewGuid();
        var childIds = new List<int>();

        foreach (var plan in plans)
        {
            var code = plan.ProposedCode.Trim().ToUpperInvariant();
            var exists = await _db.Sections.AnyAsync(s =>
                s.TenantId == _currentUser.TenantId
                && s.AcademicYearId == source.AcademicYearId
                && s.CourseId == source.CourseId
                && s.GroupId == source.GroupId
                && s.SemesterId == source.SemesterId
                && s.SectionCode == code, cancellationToken);
            if (exists) throw new InvalidOperationException($"Section code '{code}' already exists.");

            var child = new Section
            {
                CollegeId = source.CollegeId,
                AcademicYearId = source.AcademicYearId,
                CourseId = source.CourseId,
                GroupId = source.GroupId,
                SemesterId = source.SemesterId,
                SectionCode = code,
                SectionName = plan.ProposedName.Trim(),
                DisplayOrder = source.DisplayOrder,
                MaximumStrength = plan.ProposedCapacity > 0 ? plan.ProposedCapacity : Math.Max(1, source.MaximumStrength / plans.Count),
                MinimumCapacity = 0,
                RecommendedCapacity = plan.ProposedCapacity,
                ReservedSeats = 0,
                WaitingListCount = 0,
                Status = SectionLifecycleStates.Open,
                SectionTypeCode = source.SectionTypeCode,
                ParentSectionId = source.Id,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            };
            await _db.AddAsync(child);
            await _db.SaveChangesAsync(cancellationToken);
            childIds.Add(child.Id);

            await _db.AddAsync(new SectionLineage
            {
                ParentSectionId = source.Id,
                ChildSectionId = child.Id,
                RelationKind = "Split",
                TransactionId = txId,
                EffectiveDate = effective,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });
        }

        await _lifecycle.ApplyStatusAsync(source.Id, SectionLifecycleStates.Split, $"Split into {string.Join(",", childIds)}", cancellationToken);

        var tx = new SectionSplitTransaction
        {
            TransactionId = txId,
            SourceSectionId = source.Id,
            ChildSectionIdsCsv = string.Join(",", childIds),
            StrategyCode = string.IsNullOrWhiteSpace(request.StrategyCode) ? "Manual" : request.StrategyCode.Trim(),
            EffectiveDate = effective,
            Status = "Committed",
            Notes = request.Notes,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(tx);
        await _db.SaveChangesAsync(cancellationToken);

        var sourceStrength = await _db.StudentSections.CountAsync(
            x => x.SectionId == source.Id && x.IsCurrent, cancellationToken);
        await _versions.RecordAsync(source, Domain.Academic.SectionVersionOperations.Split, request.Notes, sourceStrength, cancellationToken);
        foreach (var childId in childIds)
        {
            var child = await _db.Sections.FirstAsync(s => s.Id == childId, cancellationToken);
            await _versions.RecordAsync(child, Domain.Academic.SectionVersionOperations.Split, "Split child created", 0, cancellationToken);
        }

        return MapTx(tx);
    }

    public async Task<SectionSplitTransactionDto> ReverseAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var tx = await _db.SectionSplitTransactions.FirstOrDefaultAsync(
            t => t.TenantId == _currentUser.TenantId && t.TransactionId == transactionId, cancellationToken)
            ?? throw new KeyNotFoundException("Split transaction not found.");
        if (tx.IsReversed) throw new InvalidOperationException("Split transaction already reversed.");

        await _lifecycle.ApplyStatusAsync(tx.SourceSectionId, SectionLifecycleStates.Active, $"Split reversed {transactionId:N}", cancellationToken);

        foreach (var childId in ParseIds(tx.ChildSectionIdsCsv))
            await _lifecycle.ApplyStatusAsync(childId, SectionLifecycleStates.Closed, $"Split reversed {transactionId:N}", cancellationToken);

        tx.IsReversed = true;
        tx.ReversedUtc = DateTime.UtcNow;
        tx.Status = "Reversed";
        tx.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return MapTx(tx);
    }

    public async Task<IReadOnlyList<SectionSplitTransactionDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.SectionSplitTransactions.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId)
            .OrderByDescending(t => t.CreatedDate)
            .ToListAsync(cancellationToken);
        return rows.Select(MapTx).ToList();
    }

    public async Task<IReadOnlyList<SectionLineageDto>> GetLineageAsync(int sectionId, CancellationToken cancellationToken = default)
    {
        return await _db.SectionLineages.AsNoTracking()
            .Where(l => l.TenantId == _currentUser.TenantId
                        && (l.ParentSectionId == sectionId || l.ChildSectionId == sectionId))
            .OrderByDescending(l => l.EffectiveDate)
            .Select(l => new SectionLineageDto
            {
                ParentSectionId = l.ParentSectionId,
                ChildSectionId = l.ChildSectionId,
                RelationKind = l.RelationKind,
                TransactionId = l.TransactionId,
                EffectiveDate = l.EffectiveDate,
            })
            .ToListAsync(cancellationToken);
    }

    private static SectionSplitTransactionDto MapTx(SectionSplitTransaction t) => new()
    {
        Id = t.Id,
        TransactionId = t.TransactionId,
        SourceSectionId = t.SourceSectionId,
        ChildSectionIds = ParseIds(t.ChildSectionIdsCsv),
        StrategyCode = t.StrategyCode,
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
