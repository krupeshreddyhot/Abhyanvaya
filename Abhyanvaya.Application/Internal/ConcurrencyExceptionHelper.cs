using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Internal;

/// <summary>
/// Centralizes translation of EF Core persistence exceptions for the AI attendance module.
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
            if (entry.Entity is AttendanceSession)
            {
                return ConcurrencyConflictException.ForAttendanceSession();
            }

            if (entry.Entity is AttendanceRecognition)
            {
                return ConcurrencyConflictException.ForAttendanceRecognition();
            }
        }

        return ConcurrencyConflictException.ForAttendanceModule();
    }
}
