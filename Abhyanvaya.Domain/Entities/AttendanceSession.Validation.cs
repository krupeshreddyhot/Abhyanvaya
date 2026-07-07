using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Domain validation for <see cref="AttendanceSession"/> as the attendance aggregate root.
/// Application services trust the session's denormalized academic context; they do not re-query
/// Course, Group, Semester, or Subject when materializing attendance.
/// </summary>
public partial class AttendanceSession
{
    /// <summary>
    /// Ensures the session carries complete denormalized academic context required to build attendance.
    /// </summary>
    /// <exception cref="InvalidOperationException">When any required context field is missing.</exception>
    public void ValidateAcademicContext()
    {
        if (TenantId <= 0)
        {
            throw new InvalidOperationException("Attendance session tenant is not set.");
        }

        if (CourseId <= 0 || GroupId <= 0 || SemesterId <= 0 || SubjectId <= 0)
        {
            throw new InvalidOperationException("Attendance session academic context is incomplete.");
        }

        if (AttendanceDate == default)
        {
            throw new InvalidOperationException("Attendance session date is not set.");
        }
    }

    /// <summary>
    /// Ensures the session is in a state that allows official attendance rows to be materialized.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the session cannot be built.</exception>
    public void ValidateCanBuildAttendance()
    {
        ValidateAcademicContext();

        if (Status == AttendanceSessionStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled attendance sessions cannot materialize attendance.");
        }
    }

    /// <summary>
    /// Ensures attendance for this session's subject and date is not locked.
    /// </summary>
    /// <param name="isLocked">Result of an attendance lock query for this session's scope.</param>
    /// <exception cref="InvalidOperationException">When attendance is locked.</exception>
    public void ValidateAttendanceNotLocked(bool isLocked)
    {
        if (isLocked)
        {
            throw new InvalidOperationException(
                $"Attendance is locked for subject {SubjectId} on {GetAttendanceDateUtc():yyyy-MM-dd}.");
        }
    }

    /// <summary>
    /// UTC instant used when persisting <see cref="Attendance.Date"/> for this session.
    /// </summary>
    public DateTime GetAttendanceDateUtc() =>
        AttendanceDate.Kind switch
        {
            DateTimeKind.Utc => AttendanceDate,
            DateTimeKind.Local => AttendanceDate.ToUniversalTime(),
            _ => DateTime.SpecifyKind(AttendanceDate, DateTimeKind.Utc)
        };
}
