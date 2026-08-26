using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J-A —
/// Operational vs historical Semester selection. Does not infer Group ownership.
/// Soft-delete (<see cref="Semester.IsDeleted"/>) remains distinct from historical archive.
/// </summary>
public static class OperationalSemesterRules
{
    public const string HistoricalRejectedMessage =
        "Historical Semesters cannot be used for new operational assignments.";

    public static bool IsOperational(bool isDeleted, int? groupId, bool isHistoricalArchive)
        => !isDeleted && groupId is not null && !isHistoricalArchive;

    public static bool IsOperational(Semester semester)
        => IsOperational(semester.IsDeleted, semester.GroupId, semester.IsHistoricalArchive);

    public static IQueryable<Semester> WhereOperational(IQueryable<Semester> query)
        => query.Where(s => !s.IsDeleted && s.GroupId != null && !s.IsHistoricalArchive);

    public static IQueryable<Semester> WhereHistoricalArchive(IQueryable<Semester> query)
        => query.Where(s => !s.IsDeleted && s.IsHistoricalArchive);
}
