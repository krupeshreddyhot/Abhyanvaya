using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Abhyanvaya.Application;

/// <summary>
/// Validates teacher review completeness and materializes official attendance for an approved session.
/// </summary>
public sealed class AttendanceSessionFinalizer : IAttendanceSessionFinalizer
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttendanceBuilder _attendanceBuilder;
    private readonly IAuditService _auditService;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly IAttendanceCalendar _attendanceCalendar;
    private readonly ILogger<AttendanceSessionFinalizer> _logger;

    public AttendanceSessionFinalizer(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IAttendanceBuilder attendanceBuilder,
        IAuditService auditService,
        IDomainEventDispatcher domainEventDispatcher,
        IAttendanceCalendar attendanceCalendar,
        ILogger<AttendanceSessionFinalizer> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _attendanceBuilder = attendanceBuilder;
        _auditService = auditService;
        _domainEventDispatcher = domainEventDispatcher;
        _attendanceCalendar = attendanceCalendar;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AttendanceBuildSummaryDto> FinalizeAttendanceSessionAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        AttendanceBuildSummaryDto summary = null!;
        AttendanceSession session = null!;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            session = await _context.AttendanceSessions
                .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, ct)
                ?? throw new KeyNotFoundException($"Attendance session '{attendanceSessionId}' was not found.");

            TenantAccessGuard.EnsureTenantAccess(_currentUser, session.TenantId);

            var attendanceAlreadyGenerated = await HasGeneratedAttendanceAsync(attendanceSessionId, ct);

            if (session.Status is AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
            {
                summary = await BuildExistingSummaryAsync(attendanceSessionId, session, ct);
                summary.AlreadyFinalized = true;
                summary.GeneratedUtc = session.ApprovedUtc;
                return;
            }

            var recognitions = await _context.AttendanceRecognitions
                .AsNoTracking()
                .Where(r => r.AttendanceSessionId == attendanceSessionId)
                .ToListAsync(ct);

            FinalizationValidator.ValidateOrThrow(session, recognitions, attendanceAlreadyGenerated);

            _logger.LogInformation(
                "Finalization validating complete. AttendanceSessionId={AttendanceSessionId}",
                attendanceSessionId);

            summary = await _attendanceBuilder.BuildAsync(attendanceSessionId, ct);

            _logger.LogInformation(
                "Finalization attendance built. AttendanceSessionId={AttendanceSessionId} Present={Present} Absent={Absent}",
                attendanceSessionId,
                summary.Present,
                summary.Absent);

            var previousStatus = session.Status;
            var approvedUtc = DateTime.UtcNow;
            session.Approve(
                _currentUser.UserId > 0 ? _currentUser.UserId : null,
                approvedUtc);

            await _auditService.RecordAsync(
                nameof(AttendanceSession),
                session.Id.ToString(),
                AuditAction.Approved,
                oldValues: new { Status = previousStatus, ApprovedUtc = (DateTime?)null },
                newValues: new
                {
                    Status = AttendanceSessionStatus.Approved,
                    ApprovedUtc = approvedUtc,
                    summary.Present,
                    summary.Absent,
                    summary.ManualCorrections,
                    summary.Unknown,
                    DurationMilliseconds = (int)stopwatch.ElapsedMilliseconds
                },
                ct);

            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct);

            var attendanceDay = _attendanceCalendar.GetAttendanceDay(session.GetAttendanceDateUtc());
            session.AddDomainEvent(new AttendanceGeneratedFromAIEvent(
                session.Id,
                session.TenantId,
                session.SubjectId,
                attendanceDay,
                summary.TotalStudents,
                summary.Present,
                summary.Absent,
                session.AttendanceMethod));
            session.AddDomainEvent(new AttendanceFinalizedEvent(
                session.Id,
                session.TenantId,
                session.SubjectId,
                attendanceDay,
                summary.TotalStudents,
                summary.Present,
                summary.Absent,
                session.AttendanceMethod));

            await DomainEventPublisher.DispatchAndClearAsync(session, _domainEventDispatcher, ct);

            summary.GeneratedUtc = approvedUtc;
        }, cancellationToken);

        summary.DurationMilliseconds = (int)stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Attendance finalized. AttendanceSessionId={AttendanceSessionId} TenantId={TenantId} Present={Present} Absent={Absent} DurationMs={DurationMs} AlreadyFinalized={AlreadyFinalized}",
            summary.AttendanceSessionId,
            session.TenantId,
            summary.Present,
            summary.Absent,
            summary.DurationMilliseconds,
            summary.AlreadyFinalized);

        return summary;
    }

    private async Task<bool> HasGeneratedAttendanceAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken) =>
        await _context.Attendances
            .AsNoTracking()
            .AnyAsync(a => a.AttendanceSessionId == attendanceSessionId, cancellationToken);

    private async Task<AttendanceBuildSummaryDto> BuildExistingSummaryAsync(
        Guid attendanceSessionId,
        AttendanceSession session,
        CancellationToken cancellationToken)
    {
        var attendances = await _context.Attendances
            .AsNoTracking()
            .Where(a => a.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var counts = AttendanceRecognitionMetrics.CountByStatus(recognitions);

        return new AttendanceBuildSummaryDto
        {
            AttendanceSessionId = attendanceSessionId,
            Present = attendances.Count(a => a.Status == AttendanceStatus.Present),
            Absent = attendances.Count(a => a.Status == AttendanceStatus.Absent),
            Ignored = counts.IgnoredCount,
            Rejected = counts.RejectedCount,
            Unknown = counts.UnknownCount,
            ManualCorrections = recognitions.Count(r => r.TeacherOverride),
            TotalStudents = attendances.Count,
            GeneratedUtc = session.ApprovedUtc,
            AlreadyFinalized = true
        };
    }
}
