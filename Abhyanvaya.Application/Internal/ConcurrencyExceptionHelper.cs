using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// Centralizes translation of EF Core <see cref="DbUpdateConcurrencyException"/> to
/// <see cref="ConcurrencyConflictException"/> for attendance, enrollment, and scheduling saves.
/// </summary>
public static class ConcurrencyExceptionHelper
{
    /// <summary>
    /// Persists pending changes and maps <see cref="DbUpdateConcurrencyException"/> to
    /// <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public static async Task<int> SaveChangesAsync(
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw MapConcurrencyException(ex);
        }
    }

    internal static ConcurrencyConflictException MapConcurrencyException(DbUpdateConcurrencyException exception)
    {
        foreach (var entry in exception.Entries)
        {
            var mapped = ClassifyConcurrencyConflict(entry.Entity);
            if (mapped is not null)
                return mapped;
        }

        // Unknown / empty entry list — preserve prior attendance-module default for non-scheduling callers.
        return ConcurrencyConflictException.ForAttendanceModule();
    }

    /// <summary>
    /// Test seam / shared classifier: maps a conflicted entity instance to the established conflict response,
    /// or <c>null</c> when the type is not recognized (caller applies module default).
    /// </summary>
    internal static ConcurrencyConflictException? ClassifyConcurrencyConflict(object entity) =>
        entity switch
        {
            AttendanceSession => ConcurrencyConflictException.ForAttendanceSession(),
            AttendanceRecognition => ConcurrencyConflictException.ForAttendanceRecognition(),
            StudentEnrollmentBatch or StudentEnrollmentItem => ConcurrencyConflictException.ForEnrollmentBatch(),
            Timetable
                or TimetableEntry
                or TimetableSection
                or TeachingGroup
                or TeachingGroupSection
                or TeachingGroupMembership
                or ScheduleVersion
                or SubjectAllocation
                or Room => ConcurrencyConflictException.ForSchedulingModule(),
            _ => null
        };
}
