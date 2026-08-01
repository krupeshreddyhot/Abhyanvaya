using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimetableGovernanceDashboardService : ITimetableGovernanceDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly ITimetableApprovalRepository _approvalRepository;
    private readonly ITimetableChangeHistoryRepository _historyRepository;
    private readonly ITimetableSoftValidationService _softValidationService;
    private readonly ICurrentUserService _currentUser;

    public TimetableGovernanceDashboardService(
        IApplicationDbContext context,
        ITimetableApprovalRepository approvalRepository,
        ITimetableChangeHistoryRepository historyRepository,
        ITimetableSoftValidationService softValidationService,
        ICurrentUserService currentUser)
    {
        _context = context;
        _approvalRepository = approvalRepository;
        _historyRepository = historyRepository;
        _softValidationService = softValidationService;
        _currentUser = currentUser;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<TimetableGovernanceDashboardDto> GetDashboardAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var versions = _context.SchedulingScheduleVersions.Where(x => x.TenantId == TenantId);
        if (academicYearId.HasValue)
            versions = versions.Where(x => x.AcademicYearId == academicYearId.Value);

        var draftVersionCount = await versions.CountAsync(x => x.Status == ScheduleVersionStatus.Draft, cancellationToken);
        var publishedVersionCount = await versions.CountAsync(x => x.Status == ScheduleVersionStatus.Published, cancellationToken);
        var archivedVersionCount = await versions.CountAsync(x => x.Status == ScheduleVersionStatus.Archived, cancellationToken);

        var approvalQueueCount = await _approvalRepository.CountPendingAsync(TenantId, cancellationToken);
        var pendingReviewsCount = await _context.SchedulingTimetableApprovalRequests
            .CountAsync(x => x.TenantId == TenantId && x.Status == TimetableApprovalRequestStatus.InReview, cancellationToken);

        var since = DateTime.UtcNow.AddDays(-30);
        var recentlyPublishedCount = await _context.SchedulingTimetables
            .CountAsync(x => x.TenantId == TenantId && x.Status == TimetableStatus.Published && x.UpdatedDate >= since, cancellationToken);
        var recentChangesCount = await _historyRepository.CountRecentAsync(TenantId, since, cancellationToken);

        var sampleDraft = await _context.SchedulingTimetables
            .Where(x => x.TenantId == TenantId && x.Status == TimetableStatus.Draft)
            .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var softWarningCount = sampleDraft > 0 ? (await _softValidationService.ValidateAsync(sampleDraft, cancellationToken)).Count : 0;

        var approvalTrend = await _context.SchedulingTimetableApprovalRequests
            .Where(x => x.TenantId == TenantId && x.SubmittedUtc >= since)
            .GroupBy(x => x.Status)
            .Select(g => new NamedCountDto { Name = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var versionGrowth = await versions
            .GroupBy(x => x.Status)
            .Select(g => new NamedCountDto { Name = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var publishingHistory = await _context.SchedulingScheduleVersions
            .Where(x => x.TenantId == TenantId && x.PublishedDate.HasValue)
            .OrderByDescending(x => x.PublishedDate)
            .Take(10)
            .Select(x => new NamedCountDto { Name = x.VersionName, Count = x.VersionNumber })
            .ToListAsync(cancellationToken);

        var frozenTimetableCount = await _context.SchedulingTimetables
            .CountAsync(x => x.TenantId == TenantId && x.IsFrozen, cancellationToken);
        var archivedTimetableCount = await _context.SchedulingTimetables
            .CountAsync(x => x.TenantId == TenantId && x.Status == TimetableStatus.Archived, cancellationToken);

        var archiveReasonDistribution = await (
            from t in _context.SchedulingTimetables
            join r in _context.SchedulingArchiveReasons on t.ArchiveReasonId equals r.Id into rj
            from r in rj.DefaultIfEmpty()
            where t.TenantId == TenantId && t.Status == TimetableStatus.Archived
            group t by (r != null ? r.Name : "Unspecified") into g
            select new NamedCountDto { Name = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);

        var latestArchives = await (
            from t in _context.SchedulingTimetables
            join r in _context.SchedulingArchiveReasons on t.ArchiveReasonId equals r.Id into rj
            from r in rj.DefaultIfEmpty()
            join v in _context.SchedulingScheduleVersions on t.ReferenceVersionId equals v.Id into vj
            from v in vj.DefaultIfEmpty()
            where t.TenantId == TenantId && t.Status == TimetableStatus.Archived
            orderby t.ArchivedDate descending
            select new ArchiveLifecycleItemDto
            {
                TimetableId = t.Id,
                TimetableName = t.Name,
                ArchiveReasonName = r != null ? r.Name : null,
                ArchiveReasonCode = r != null ? r.Code : null,
                Comments = t.ArchiveComments,
                ArchivedBy = t.ArchivedBy,
                ArchivedDate = t.ArchivedDate,
                ReferenceVersionId = t.ReferenceVersionId,
                ReferenceVersionName = v != null ? v.VersionName : null
            }).Take(10).ToListAsync(cancellationToken);

        return new TimetableGovernanceDashboardDto
        {
            DraftVersionCount = draftVersionCount,
            PublishedVersionCount = publishedVersionCount,
            ApprovalQueueCount = approvalQueueCount,
            PendingReviewsCount = pendingReviewsCount,
            SoftWarningCount = softWarningCount,
            RecentlyPublishedCount = recentlyPublishedCount,
            ArchivedVersionCount = archivedVersionCount,
            RecentChangesCount = recentChangesCount,
            FrozenTimetableCount = frozenTimetableCount,
            ArchivedTimetableCount = archivedTimetableCount,
            ApprovalTrend = approvalTrend,
            VersionGrowth = versionGrowth,
            PublishingHistory = publishingHistory,
            ArchiveReasonDistribution = archiveReasonDistribution,
            LatestArchives = latestArchives
        };
    }
}
