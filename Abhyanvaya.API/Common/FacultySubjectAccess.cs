using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Common;

/// <summary>Faculty access is limited to <see cref="Domain.Entities.StaffSubjectAssignment"/> rows when the user is linked to <see cref="Domain.Entities.Staff"/>.</summary>
public static class FacultySubjectAccess
{
    public static Task<bool> StaffTeachesSubjectAsync(
        IApplicationDbContext db,
        int staffId,
        int subjectId,
        CancellationToken ct = default) =>
        db.StaffSubjectAssignments.AsNoTracking()
            .AnyAsync(x => x.StaffId == staffId && x.SubjectId == subjectId, ct);

    /// <summary>
    /// Faculty with a staff link may only use subjects assigned on the staff record.
    /// Faculty without a staff link (legacy) may use subjects in their JWT course/group only (caller enforces cohort).
    /// </summary>
    public static async Task<bool> FacultyMayAccessSubjectAsync(
        IApplicationDbContext db,
        ICurrentUserService current,
        int subjectId,
        CancellationToken ct = default)
    {
        if (!current.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            return true;

        if (current.StaffId <= 0)
            return true;

        return await StaffTeachesSubjectAsync(db, current.StaffId, subjectId, ct).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<int>> GetAssignedSubjectIdsAsync(
        IApplicationDbContext db,
        int staffId,
        CancellationToken ct = default) =>
        await db.StaffSubjectAssignments.AsNoTracking()
            .Where(x => x.StaffId == staffId)
            .Select(x => x.SubjectId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
