using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Timetable;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application;

/// <summary>Manages class schedules and attendance session creation from timetable slots.</summary>
public sealed class ClassScheduleService : IClassScheduleService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ClassScheduleService> _logger;

    public ClassScheduleService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILogger<ClassScheduleService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClassScheduleDto>> ListAsync(
        ClassScheduleQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var schedules = _context.ClassSchedules.AsNoTracking().Where(s => s.TenantId == tenantId);

        if (query.ScheduleDate.HasValue)
        {
            schedules = schedules.Where(s => s.ScheduleDate == query.ScheduleDate.Value);
        }

        if (query.StaffId.HasValue)
        {
            schedules = schedules.Where(s => s.StaffId == query.StaffId.Value);
        }

        if (query.CourseId.HasValue)
        {
            schedules = schedules.Where(s => s.CourseId == query.CourseId.Value);
        }

        if (query.GroupId.HasValue)
        {
            schedules = schedules.Where(s => s.GroupId == query.GroupId.Value);
        }

        if (query.SemesterId.HasValue)
        {
            schedules = schedules.Where(s => s.SemesterId == query.SemesterId.Value);
        }

        if (query.SubjectId.HasValue)
        {
            schedules = schedules.Where(s => s.SubjectId == query.SubjectId.Value);
        }

        if (query.ActiveOnly)
        {
            schedules = schedules.Where(s => s.IsActive);
        }

        var rows = await schedules
            .OrderBy(s => s.ScheduleDate)
            .ThenBy(s => s.PeriodNumber)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDto).ToList();
    }

    public async Task<ClassScheduleDto> CreateAsync(
        CreateClassScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        await EnsureReferencesExistAsync(tenantId, request, cancellationToken);

        var schedule = new ClassSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StaffId = request.StaffId,
            CourseId = request.CourseId,
            GroupId = request.GroupId,
            SemesterId = request.SemesterId,
            SubjectId = request.SubjectId,
            PeriodNumber = request.PeriodNumber,
            ScheduleDate = request.ScheduleDate,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };

        await _context.AddAsync(schedule);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        _logger.LogInformation(
            "Class schedule created. ScheduleId={ScheduleId} TenantId={TenantId} Date={Date} Period={Period}",
            schedule.Id,
            tenantId,
            schedule.ScheduleDate,
            schedule.PeriodNumber);

        return MapToDto(schedule);
    }

    public async Task<Guid> CreateAttendanceSessionFromScheduleAsync(
        Guid classScheduleId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var schedule = await _context.ClassSchedules
            .FirstOrDefaultAsync(s => s.Id == classScheduleId && s.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Class schedule '{classScheduleId}' was not found.");

        if (!schedule.IsActive)
        {
            throw new InvalidOperationException("Cannot create an attendance session from an inactive schedule.");
        }

        var session = AttendanceSession.CreateForPhotoAttendance(
            schedule.TenantId,
            schedule.StaffId,
            schedule.CourseId,
            schedule.GroupId,
            schedule.SemesterId,
            schedule.SubjectId,
            schedule.ScheduleDate.ToDateTime(TimeOnly.MinValue),
            schedule.PeriodNumber,
            classScheduleId: schedule.Id);

        session.TotalStudents = await _context.Students
            .CountAsync(
                s => s.TenantId == schedule.TenantId
                     && s.CourseId == schedule.CourseId
                     && s.GroupId == schedule.GroupId
                     && s.SemesterId == schedule.SemesterId,
                cancellationToken);

        await _context.AddAsync(session);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        _logger.LogInformation(
            "Attendance session created from schedule. SessionId={SessionId} ScheduleId={ScheduleId} TenantId={TenantId}",
            session.Id,
            schedule.Id,
            tenantId);

        return session.Id;
    }

    private async Task EnsureReferencesExistAsync(
        int tenantId,
        CreateClassScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var staffExists = await _context.StaffMembers.AnyAsync(s => s.Id == request.StaffId && s.TenantId == tenantId, cancellationToken);
        if (!staffExists)
        {
            throw new KeyNotFoundException($"Staff '{request.StaffId}' was not found.");
        }

        var courseExists = await _context.Courses.AnyAsync(c => c.Id == request.CourseId && c.TenantId == tenantId, cancellationToken);
        if (!courseExists)
        {
            throw new KeyNotFoundException($"Course '{request.CourseId}' was not found.");
        }
    }

    private static ClassScheduleDto MapToDto(ClassSchedule schedule) =>
        new()
        {
            Id = schedule.Id,
            TenantId = schedule.TenantId,
            StaffId = schedule.StaffId,
            CourseId = schedule.CourseId,
            GroupId = schedule.GroupId,
            SemesterId = schedule.SemesterId,
            SubjectId = schedule.SubjectId,
            PeriodNumber = schedule.PeriodNumber,
            ScheduleDate = schedule.ScheduleDate,
            IsActive = schedule.IsActive
        };
}
