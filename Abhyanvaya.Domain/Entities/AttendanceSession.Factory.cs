using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Factory helpers for creating new <see cref="AttendanceSession"/> aggregates.
/// </summary>
public partial class AttendanceSession
{
    /// <summary>
    /// Creates a new session with <see cref="SessionNumber"/> initialized to 1 (first attempt).
    /// </summary>
    public static AttendanceSession CreateNew(
        int tenantId,
        int courseId,
        int groupId,
        int semesterId,
        int subjectId,
        DateTime attendanceDate,
        AttendanceMethod attendanceMethod = AttendanceMethod.Manual,
        AttendanceSource attendanceSource = AttendanceSource.Web,
        int? periodNumber = null,
        short? sessionNumber = null,
        Guid? classScheduleId = null)
    {
        return new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            SubjectId = subjectId,
            AttendanceDate = attendanceDate,
            PeriodNumber = periodNumber,
            SessionNumber = sessionNumber ?? 1,
            AttendanceMethod = attendanceMethod,
            AttendanceSource = attendanceSource,
            ClassScheduleId = classScheduleId,
            Status = AttendanceSessionStatus.Draft,
            CreatedUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new AI photo attendance session with defaults for recognition workflow.
    /// </summary>
    public static AttendanceSession CreateForPhotoAttendance(
        int tenantId,
        int facultyId,
        int courseId,
        int groupId,
        int semesterId,
        int subjectId,
        DateTime attendanceDate,
        int periodNumber,
        short sessionNumber = 1,
        AttendanceSource attendanceSource = AttendanceSource.Web,
        Guid? classScheduleId = null,
        string? recognitionPipelineVersion = null)
    {
        var session = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StaffId = facultyId,
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            SubjectId = subjectId,
            AttendanceDate = attendanceDate,
            PeriodNumber = periodNumber,
            SessionNumber = sessionNumber,
            AttendanceMethod = AttendanceMethod.AIPhoto,
            AttendanceSource = attendanceSource,
            ClassScheduleId = classScheduleId,
            RecognitionPipelineVersion = recognitionPipelineVersion,
            Status = AttendanceSessionStatus.Draft,
            CreatedUtc = DateTime.UtcNow,
            DetectedFaces = 0,
            RecognizedFaces = 0,
            UnknownFaces = 0,
            RecognizedCount = 0,
            UnknownCount = 0,
            RejectedCount = 0,
            IgnoredCount = 0,
            DuplicateCount = 0,
            ManualAssignmentCount = 0,
            LowConfidenceCount = 0,
            TotalStudents = 0,
            RetryCount = 0
        };

        return session;
    }
}
