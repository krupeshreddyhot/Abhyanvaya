using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecognition;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application;

/// <summary>
/// Teacher review workflow for provisional <see cref="AttendanceRecognition"/> rows.
/// </summary>
/// <remarks>
/// All review mutations persist through <see cref="ConcurrencyExceptionHelper"/> so concurrent teacher
/// edits return <see cref="Domain.Exceptions.ConcurrencyConflictException"/> instead of silently overwriting.
/// </remarks>
public sealed class AttendanceRecognitionReviewService : IAttendanceRecognitionReviewService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentService _studentService;
    private readonly IAttendanceSessionSummaryService _sessionSummaryService;
    private readonly ILogger<AttendanceRecognitionReviewService> _logger;

    public AttendanceRecognitionReviewService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IStudentService studentService,
        IAttendanceSessionSummaryService sessionSummaryService,
        ILogger<AttendanceRecognitionReviewService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _studentService = studentService;
        _sessionSummaryService = sessionSummaryService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AttendanceRecognitionReviewDto>> GetRecognitionsForSessionAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await ValidateSessionAsync(attendanceSessionId, cancellationToken);

        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Include(r => r.Student)
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .OrderByDescending(r => r.ConfidenceScore ?? -1m)
            .ThenBy(r => r.FaceNumber)
            .ToListAsync(cancellationToken);

        var results = new List<AttendanceRecognitionReviewDto>(recognitions.Count);
        foreach (var recognition in recognitions)
        {
            results.Add(await MapToReviewDtoAsync(recognition, session, cancellationToken));
        }

        return results;
    }

    public async Task<RecognitionSummaryDto> GetRecognitionSummaryAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        await ValidateSessionAsync(attendanceSessionId, cancellationToken);

        var recognitions = await _context.AttendanceRecognitions
            .AsNoTracking()
            .Where(r => r.AttendanceSessionId == attendanceSessionId)
            .ToListAsync(cancellationToken);

        var stats = BuildStatistics(recognitions);
        var blockers = BuildFinalizeBlockers(recognitions);

        return new RecognitionSummaryDto
        {
            AttendanceSessionId = attendanceSessionId,
            Statistics = stats,
            CanFinalize = blockers.Count == 0,
            FinalizeBlockers = blockers
        };
    }

    public async Task<AttendanceRecognitionDto> ReviewRecognitionAsync(
        AttendanceRecognitionReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var recognition = await _context.AttendanceRecognitions
            .Include(r => r.Student)
            .FirstOrDefaultAsync(r => r.Id == request.RecognitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Recognition '{request.RecognitionId}' was not found.");

        await ValidateSessionAsync(recognition.AttendanceSessionId, cancellationToken, requireWritable: true);
        TenantAccessGuard.EnsureTenantAccess(_currentUser, recognition.TenantId);

        ValidateReviewRequest(request);

        if (request.Action == RecognitionReviewAction.AssignStudent)
        {
            await ValidateStudentAsync(request.StudentId, recognition.TenantId, cancellationToken);
        }

        var history = ApplyReviewActionWithAudit(recognition, request);
        await _context.AddAsync(history);
        await _sessionSummaryService.SyncSessionSummaryAsync(recognition.AttendanceSessionId, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        _logger.LogInformation(
            "Recognition reviewed. RecognitionId={RecognitionId} AttendanceSessionId={AttendanceSessionId} TenantId={TenantId} Action={Action} NewStatus={NewStatus}",
            recognition.Id,
            recognition.AttendanceSessionId,
            recognition.TenantId,
            request.Action,
            recognition.RecognitionStatus);

        return await MapToDtoAsync(recognition, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecognitionDto>> ReviewBatchAsync(
        AttendanceRecognitionBatchReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Reviews.Count == 0)
        {
            return Array.Empty<AttendanceRecognitionDto>();
        }

        var session = await ValidateSessionAsync(request.AttendanceSessionId, cancellationToken, requireWritable: true);

        var recognitionIds = request.Reviews.Select(r => r.RecognitionId).Distinct().ToList();
        if (recognitionIds.Count != request.Reviews.Count)
        {
            throw new InvalidOperationException("Duplicate recognition IDs are not allowed in a batch review.");
        }

        var recognitions = await _context.AttendanceRecognitions
            .Include(r => r.Student)
            .Where(r => r.AttendanceSessionId == request.AttendanceSessionId
                        && recognitionIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (recognitions.Count != recognitionIds.Count)
        {
            throw new KeyNotFoundException(
                "One or more recognition rows were not found for the specified attendance session.");
        }

        TenantAccessGuard.EnsureTenantAccess(_currentUser, session.TenantId);

        var byId = recognitions.ToDictionary(r => r.Id);
        var results = new List<AttendanceRecognitionDto>(request.Reviews.Count);
        var historyEntries = new List<AttendanceRecognitionReviewHistory>(request.Reviews.Count);

        foreach (var review in request.Reviews)
        {
            var recognition = byId[review.RecognitionId];

            ValidateReviewRequest(review);

            if (review.Action == RecognitionReviewAction.AssignStudent)
            {
                await ValidateStudentAsync(review.StudentId, recognition.TenantId, cancellationToken);
            }

            historyEntries.Add(ApplyReviewActionWithAudit(recognition, review));
            results.Add(await MapToDtoAsync(recognition, cancellationToken));
        }

        await _context.AddRangeAsync(historyEntries);
        await _sessionSummaryService.SyncSessionSummaryAsync(request.AttendanceSessionId, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        _logger.LogInformation(
            "Recognition batch reviewed. AttendanceSessionId={AttendanceSessionId} TenantId={TenantId} ReviewCount={ReviewCount}",
            request.AttendanceSessionId,
            session.TenantId,
            request.Reviews.Count);

        return results;
    }

    public Task<AttendanceRecognitionDto> ResetRecognitionAsync(
        Guid recognitionId,
        CancellationToken cancellationToken = default) =>
        ReviewRecognitionAsync(
            new AttendanceRecognitionReviewRequest
            {
                RecognitionId = recognitionId,
                Action = RecognitionReviewAction.Reset
            },
            cancellationToken);

    public async Task<IReadOnlyList<AttendanceRecognitionReviewHistoryDto>> GetReviewHistoryForRecognitionAsync(
        Guid recognitionId,
        CancellationToken cancellationToken = default)
    {
        var recognition = await _context.AttendanceRecognitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == recognitionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Recognition '{recognitionId}' was not found.");

        TenantAccessGuard.EnsureTenantAccess(_currentUser, recognition.TenantId);

        return await QueryReviewHistoryAsync(
            _context.AttendanceRecognitionReviewHistories
                .AsNoTracking()
                .Where(h => h.RecognitionId == recognitionId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRecognitionReviewHistoryDto>> GetReviewHistoryForSessionAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken = default)
    {
        await ValidateSessionAsync(attendanceSessionId, cancellationToken);

        return await QueryReviewHistoryAsync(
            _context.AttendanceRecognitionReviewHistories
                .AsNoTracking()
                .Where(h => h.Recognition.AttendanceSessionId == attendanceSessionId),
            cancellationToken);
    }

    private async Task<IReadOnlyList<AttendanceRecognitionReviewHistoryDto>> QueryReviewHistoryAsync(
        IQueryable<AttendanceRecognitionReviewHistory> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Include(h => h.ReviewedByUser)
            .OrderByDescending(h => h.ReviewedUtc)
            .ThenByDescending(h => h.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(MapHistoryToDto).ToList();
    }

    private AttendanceRecognitionReviewHistory ApplyReviewActionWithAudit(
        AttendanceRecognition recognition,
        AttendanceRecognitionReviewRequest request)
    {
        var oldStatus = recognition.RecognitionStatus;
        var oldStudentId = recognition.StudentId;

        ApplyReviewAction(recognition, request);

        return CreateHistoryEntry(
            recognition,
            oldStatus,
            oldStudentId,
            request,
            recognition.RecognitionStatus,
            recognition.StudentId);
    }

    private AttendanceRecognitionReviewHistory CreateHistoryEntry(
        AttendanceRecognition recognition,
        RecognitionStatus oldStatus,
        int? oldStudentId,
        AttendanceRecognitionReviewRequest request,
        RecognitionStatus newStatus,
        int? newStudentId) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecognitionId = recognition.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            OldStudentId = oldStudentId,
            NewStudentId = newStudentId,
            ReviewAction = request.Action,
            ReviewNotes = request.ReviewNotes,
            ReviewedBy = GetReviewedByUserId(),
            ReviewedUtc = DateTime.UtcNow
        };

    private int GetReviewedByUserId()
    {
        if (_currentUser.UserId <= 0)
        {
            throw new InvalidOperationException("A valid authenticated user is required to review recognitions.");
        }

        return _currentUser.UserId;
    }

    private async Task<AttendanceSession> ValidateSessionAsync(
        Guid attendanceSessionId,
        CancellationToken cancellationToken,
        bool requireWritable = false)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == attendanceSessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attendance session '{attendanceSessionId}' was not found.");

        TenantAccessGuard.EnsureTenantAccess(_currentUser, session.TenantId);

        if (requireWritable)
        {
            EnsureSessionWritable(session);
        }

        return session;
    }

    private static void EnsureSessionWritable(AttendanceSession session)
    {
        if (session.Status is AttendanceSessionStatus.Approved
            or AttendanceSessionStatus.Completed
            or AttendanceSessionStatus.Cancelled)
        {
            throw ConcurrencyConflictException.ForAttendanceSession();
        }
    }

    private async Task ValidateStudentAsync(
        int? studentId,
        int tenantId,
        CancellationToken cancellationToken)
    {
        if (!studentId.HasValue)
        {
            throw new InvalidOperationException("StudentId is required for AssignStudent.");
        }

        var exists = await _context.Students
            .AsNoTracking()
            .AnyAsync(s => s.Id == studentId.Value && s.TenantId == tenantId, cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException($"Student '{studentId.Value}' was not found for this tenant.");
        }
    }

    private static void ApplyReviewAction(
        AttendanceRecognition recognition,
        AttendanceRecognitionReviewRequest request)
    {
        switch (request.Action)
        {
            case RecognitionReviewAction.Approve:
                recognition.VerifiedByTeacher = true;
                recognition.TeacherOverride = false;
                recognition.ReviewNotes = request.ReviewNotes;
                if (recognition.StudentId.HasValue)
                {
                    recognition.RecognitionStatus = RecognitionStatus.Recognized;
                }
                break;

            case RecognitionReviewAction.Reject:
                recognition.VerifiedByTeacher = true;
                recognition.TeacherOverride = false;
                recognition.ReviewNotes = request.ReviewNotes;
                recognition.RecognitionStatus = RecognitionStatus.Rejected;
                break;

            case RecognitionReviewAction.Ignore:
                recognition.VerifiedByTeacher = true;
                recognition.TeacherOverride = false;
                recognition.ReviewNotes = request.ReviewNotes;
                recognition.RecognitionStatus = RecognitionStatus.Ignored;
                recognition.StudentId = null;
                break;

            case RecognitionReviewAction.AssignStudent:
                recognition.StudentId = request.StudentId;
                recognition.VerifiedByTeacher = true;
                recognition.TeacherOverride = true;
                recognition.ReviewNotes = request.ReviewNotes;
                recognition.RecognitionStatus = RecognitionStatus.ManuallyAssigned;
                break;

            case RecognitionReviewAction.Reset:
                recognition.VerifiedByTeacher = false;
                recognition.TeacherOverride = false;
                recognition.ReviewNotes = null;
                recognition.RecognitionStatus = RecognitionStatus.Unknown;
                recognition.StudentId = null;
                break;

            default:
                throw new InvalidOperationException($"Unsupported review action '{request.Action}'.");
        }
    }

    private static AttendanceRecognitionReviewHistoryDto MapHistoryToDto(
        AttendanceRecognitionReviewHistory history) =>
        new()
        {
            Id = history.Id,
            RecognitionId = history.RecognitionId,
            OldStatus = history.OldStatus,
            NewStatus = history.NewStatus,
            OldStudentId = history.OldStudentId,
            NewStudentId = history.NewStudentId,
            ReviewAction = history.ReviewAction,
            ReviewNotes = history.ReviewNotes,
            ReviewedBy = history.ReviewedBy,
            ReviewedByUsername = history.ReviewedByUser?.Username,
            ReviewedUtc = history.ReviewedUtc
        };

    private static void ValidateReviewRequest(AttendanceRecognitionReviewRequest request)
    {
        if (request.Action == RecognitionReviewAction.Reject
            && string.IsNullOrWhiteSpace(request.ReviewNotes))
        {
            throw new InvalidOperationException("A rejection reason is required in ReviewNotes.");
        }
    }

    private static RecognitionStatisticsDto BuildStatistics(IReadOnlyList<AttendanceRecognition> recognitions)
    {
        var withConfidence = recognitions.Where(r => r.ConfidenceScore.HasValue).ToList();
        decimal? averageConfidence = withConfidence.Count == 0
            ? null
            : Math.Round(withConfidence.Average(r => r.ConfidenceScore!.Value), 2);

        return new RecognitionStatisticsDto
        {
            DetectedFaces = recognitions.Count,
            Matched = recognitions.Count(IsMatchedRecognition),
            Unmatched = recognitions.Count(r => !IsMatchedRecognition(r)),
            LowConfidence = recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.LowConfidence),
            ManualOverrides = recognitions.Count(r => r.TeacherOverride),
            Rejected = recognitions.Count(r => r.RecognitionStatus == RecognitionStatus.Rejected),
            Approved = recognitions.Count(r =>
                r.VerifiedByTeacher
                && r.RecognitionStatus is RecognitionStatus.Recognized or RecognitionStatus.ManuallyAssigned),
            PendingReview = recognitions.Count(IsPendingRecognition),
            AverageConfidence = averageConfidence
        };
    }

    private static List<string> BuildFinalizeBlockers(IReadOnlyList<AttendanceRecognition> recognitions)
    {
        var blockers = new List<string>();

        if (recognitions.Any(r => r.RecognitionStatus == RecognitionStatus.Unknown))
        {
            blockers.Add("Unknown recognitions must be reviewed before finalization.");
        }

        if (recognitions.Any(r => r.RecognitionStatus == RecognitionStatus.LowConfidence && !r.VerifiedByTeacher))
        {
            blockers.Add("Low-confidence recognitions must be reviewed before finalization.");
        }

        if (recognitions.Any(r => !r.VerifiedByTeacher))
        {
            blockers.Add("All recognitions must be reviewed before finalization.");
        }

        if (recognitions.Any(r => r.RecognitionStatus == RecognitionStatus.Duplicate && !r.VerifiedByTeacher))
        {
            blockers.Add("Duplicate recognitions must be reviewed before finalization.");
        }

        return blockers;
    }

    private static bool IsMatchedRecognition(AttendanceRecognition recognition) =>
        recognition.StudentId.HasValue
        && recognition.RecognitionStatus is RecognitionStatus.Recognized
            or RecognitionStatus.LowConfidence
            or RecognitionStatus.ManuallyAssigned;

    private static bool IsPendingRecognition(AttendanceRecognition recognition) =>
        !recognition.VerifiedByTeacher
        || recognition.RecognitionStatus is RecognitionStatus.Unknown or RecognitionStatus.LowConfidence;

    private async Task<AttendanceRecognitionReviewDto> MapToReviewDtoAsync(
        AttendanceRecognition recognition,
        AttendanceSession session,
        CancellationToken cancellationToken)
    {
        var student = recognition.Student;
        if (student == null && recognition.StudentId.HasValue)
        {
            student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == recognition.StudentId.Value, cancellationToken);
        }

        var studentPhotoUrl = student == null
            ? null
            : _studentService.BuildPhotoVariantPath(student.PhotoKey, student.PhotoUploadedUtc, "thumbnail");

        var faceThumbnailUrl = AttendanceSessionMediaPaths.BuildMediaUrl(
            recognition.FaceImageKey,
            recognition.CreatedUtc);

        int? suggestedStudentId = null;
        string? suggestedStudentName = null;
        string? suggestedStudentNumber = null;
        int? manualOverrideStudentId = null;
        string? manualOverrideStudentName = null;
        string? manualOverrideStudentNumber = null;

        if (recognition.TeacherOverride)
        {
            manualOverrideStudentId = recognition.StudentId;
            manualOverrideStudentName = student?.Name;
            manualOverrideStudentNumber = student?.StudentNumber;
        }
        else if (recognition.StudentId.HasValue)
        {
            suggestedStudentId = recognition.StudentId;
            suggestedStudentName = student?.Name;
            suggestedStudentNumber = student?.StudentNumber;
        }

        return new AttendanceRecognitionReviewDto
        {
            RecognitionId = recognition.Id,
            AttendanceSessionId = recognition.AttendanceSessionId,
            FaceNumber = recognition.FaceNumber,
            StudentId = recognition.StudentId,
            StudentNumber = student?.StudentNumber,
            StudentName = student?.Name,
            Confidence = recognition.ConfidenceScore,
            BoundingBoxX = recognition.BoundingBoxX,
            BoundingBoxY = recognition.BoundingBoxY,
            BoundingBoxWidth = recognition.BoundingBoxWidth,
            BoundingBoxHeight = recognition.BoundingBoxHeight,
            FaceThumbnailUrl = faceThumbnailUrl,
            StudentPhotoUrl = studentPhotoUrl,
            Status = recognition.RecognitionStatus,
            IsMatched = IsMatchedRecognition(recognition),
            SuggestedStudentId = suggestedStudentId,
            SuggestedStudentName = suggestedStudentName,
            SuggestedStudentNumber = suggestedStudentNumber,
            ManualOverrideStudentId = manualOverrideStudentId,
            ManualOverrideStudentName = manualOverrideStudentName,
            ManualOverrideStudentNumber = manualOverrideStudentNumber,
            VerifiedByTeacher = recognition.VerifiedByTeacher,
            TeacherOverride = recognition.TeacherOverride,
            ReviewNotes = recognition.ReviewNotes
        };
    }

    private async Task<AttendanceRecognitionDto> MapToDtoAsync(
        AttendanceRecognition recognition,
        CancellationToken cancellationToken)
    {
        var session = await _context.AttendanceSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == recognition.AttendanceSessionId, cancellationToken);

        var reviewDto = await MapToReviewDtoAsync(recognition, session, cancellationToken);

        return new AttendanceRecognitionDto
        {
            Id = reviewDto.RecognitionId,
            AttendanceSessionId = reviewDto.AttendanceSessionId,
            StudentId = reviewDto.StudentId,
            StudentName = reviewDto.StudentName,
            StudentNumber = reviewDto.StudentNumber,
            ThumbnailUrl = reviewDto.FaceThumbnailUrl ?? reviewDto.StudentPhotoUrl,
            ConfidenceScore = reviewDto.Confidence,
            EmbeddingDistance = recognition.EmbeddingDistance,
            RecognitionStatus = reviewDto.Status,
            BoundingBoxX = reviewDto.BoundingBoxX,
            BoundingBoxY = reviewDto.BoundingBoxY,
            BoundingBoxWidth = reviewDto.BoundingBoxWidth,
            BoundingBoxHeight = reviewDto.BoundingBoxHeight,
            VerifiedByTeacher = reviewDto.VerifiedByTeacher,
            TeacherOverride = reviewDto.TeacherOverride,
            ReviewNotes = reviewDto.ReviewNotes
        };
    }
}
