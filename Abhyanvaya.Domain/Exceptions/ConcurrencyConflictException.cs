namespace Abhyanvaya.Domain.Exceptions;

/// <summary>
/// Raised when an optimistic concurrency token (<c>RowVersion</c>) conflict is detected during save.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public const string DefaultCode = "ConcurrencyConflict";

    public ConcurrencyConflictException(string message)
        : base(message)
    {
        Code = DefaultCode;
        ReloadRequired = true;
    }

    /// <summary>Stable API error code for clients.</summary>
    public string Code { get; }

    /// <summary>When true, the client should reload current server state before retrying.</summary>
    public bool ReloadRequired { get; }

    /// <summary>Conflict on an <see cref="Entities.AttendanceSession"/> row.</summary>
    public static ConcurrencyConflictException ForAttendanceSession() =>
        new("The attendance session was modified by another user.");

    /// <summary>Conflict on an <see cref="Entities.AttendanceRecognition"/> row.</summary>
    public static ConcurrencyConflictException ForAttendanceRecognition() =>
        new("This recognition was modified by another user.");

    /// <summary>Conflict on another AI attendance entity participating in the same unit of work.</summary>
    public static ConcurrencyConflictException ForAttendanceModule() =>
        new("Attendance data was modified by another user. Please reload and try again.");

    /// <summary>Conflict on a <see cref="Entities.StudentEnrollmentBatch"/> or item row.</summary>
    public static ConcurrencyConflictException ForEnrollmentBatch() =>
        new("This enrollment batch was modified by another process. Please reload and try again.");

    /// <summary>
    /// Conflict on a scheduling entity (timetable, entry, teaching group, membership, projection).
    /// AI-SCHED-CAP Prompt 10 — established conflict response for PostgreSQL / EF concurrency.
    /// </summary>
    public static ConcurrencyConflictException ForSchedulingModule() =>
        new("This scheduling data was modified by another user. Please reload and try again.");
}
