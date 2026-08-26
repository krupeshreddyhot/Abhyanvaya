using Abhyanvaya.Application;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Application;

/// <summary>
/// Creates attendance sessions and uploads classroom photos within a single transaction.
/// </summary>
public sealed class AttendanceSessionCreator : IAttendanceSessionCreator
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAttendancePhotoService _photoService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AttendanceSessionCreator> _logger;

    public AttendanceSessionCreator(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IAttendancePhotoService photoService,
        ICurrentUserService currentUser,
        ILogger<AttendanceSessionCreator> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _photoService = photoService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error, Guid? SessionId)> CreatePhotoAttendanceSessionAsync(
        CreatePhotoAttendanceSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Guid sessionId = Guid.Empty;

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await EnsureOperationalSemesterAsync(request.SemesterId, ct);

                var facultyId = _currentUser.StaffId > 0 ? _currentUser.StaffId : _currentUser.UserId;
                var session = AttendanceSession.CreateForPhotoAttendance(
                    _currentUser.TenantId,
                    facultyId,
                    request.CourseId,
                    request.GroupId,
                    request.SemesterId,
                    request.SubjectId,
                    request.AttendanceDate,
                    request.PeriodNumber,
                    request.SessionNumber,
                    recognitionPipelineVersion: request.RecognitionPipelineVersion);

                session.TotalStudents = request.TotalStudents;
                session.CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;

                await _context.AddAsync(session);
                await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct);
                sessionId = session.Id;
            }, cancellationToken);

            _logger.LogInformation(
                "Photo attendance session created. SessionId={SessionId} TenantId={TenantId}",
                sessionId,
                _currentUser.TenantId);

            return (true, null, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create photo attendance session.");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Ok, string? Error, ClassroomPhotoUploadResult? Result)> CreateAndUploadClassroomPhotoAsync(
        CreatePhotoAttendanceSessionRequest request,
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ClassroomPhotoUploadResult? result = null;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await EnsureOperationalSemesterAsync(request.SemesterId, ct);

                var facultyId = _currentUser.StaffId > 0 ? _currentUser.StaffId : _currentUser.UserId;
                var session = AttendanceSession.CreateForPhotoAttendance(
                    _currentUser.TenantId,
                    facultyId,
                    request.CourseId,
                    request.GroupId,
                    request.SemesterId,
                    request.SubjectId,
                    request.AttendanceDate,
                    request.PeriodNumber,
                    request.SessionNumber,
                    recognitionPipelineVersion: request.RecognitionPipelineVersion);

                session.TotalStudents = request.TotalStudents;
                session.CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;

                await _context.AddAsync(session);

                var upload = await _photoService.UploadToSessionAsync(
                    session,
                    imageStream,
                    fileName,
                    fileSizeBytes,
                    cancellationToken: ct);

                if (!upload.Ok)
                {
                    throw new InvalidOperationException(upload.Error ?? "Classroom photo upload failed.");
                }

                session.MoveToPending();
                await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, ct);
                result = upload.Result;
            }, cancellationToken);

            if (result != null)
            {
                await _photoService.QueueProcessingAsync(result.AttendanceSessionId, result.ImageStorageKey, cancellationToken);
                result.Queued = true;
            }

            return (true, null, result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create attendance session with classroom photo upload.");
            return (false, ex.Message, null);
        }
    }

    private async Task EnsureOperationalSemesterAsync(int semesterId, CancellationToken ct)
    {
        var semester = await _context.Semesters.AsNoTracking()
            .Where(s => s.Id == semesterId && s.TenantId == _currentUser.TenantId && !s.IsDeleted)
            .Select(s => new { s.GroupId, s.IsHistoricalArchive })
            .FirstOrDefaultAsync(ct);
        if (semester is null)
            throw new InvalidOperationException("Semester not found.");
        if (semester.IsHistoricalArchive || semester.GroupId is null)
            throw new InvalidOperationException(OperationalSemesterRules.HistoricalRejectedMessage);
    }
}
