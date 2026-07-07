using Abhyanvaya.Application.DTOs.Timetable;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Manages published class schedules and session creation from timetable slots.
/// </summary>
public interface IClassScheduleService
{
    Task<IReadOnlyList<ClassScheduleDto>> ListAsync(
        ClassScheduleQuery query,
        CancellationToken cancellationToken = default);

    Task<ClassScheduleDto> CreateAsync(
        CreateClassScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAttendanceSessionFromScheduleAsync(
        Guid classScheduleId,
        CancellationToken cancellationToken = default);
}
