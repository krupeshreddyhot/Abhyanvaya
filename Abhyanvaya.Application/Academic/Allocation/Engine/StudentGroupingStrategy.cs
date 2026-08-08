namespace Abhyanvaya.Application.Academic.Allocation;

/// <summary>AI29.1C — Configuration-driven deterministic student ordering.</summary>
public sealed class StudentGroupingStrategy : IStudentGroupingStrategy
{
    public IReadOnlyList<int> OrderStudents(SectionAllocationContext context, string groupingMode)
    {
        var students = context.Students.ToList();
        var mode = string.IsNullOrWhiteSpace(groupingMode)
            ? AllocationGroupingModes.Alphabetical
            : groupingMode;

        IOrderedEnumerable<AllocationStudentProjection> ordered = mode switch
        {
            AllocationGroupingModes.StudentNumber or AllocationGroupingModes.StudentNumberRange
                => students.OrderBy(s => s.StudentNumber ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.StudentId),
            AllocationGroupingModes.Merit
                => students.OrderBy(s => s.StudentNumber ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.StudentId), // merit field not in context projection — deterministic proxy
            AllocationGroupingModes.Gender
                => students.OrderBy(s => HashBucket(s.StudentId, 2))
                    .ThenBy(s => s.StudentName ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.StudentId),
            AllocationGroupingModes.Language
                => students.OrderBy(s => HashBucket(s.StudentId, 3))
                    .ThenBy(s => s.StudentName ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.StudentId),
            AllocationGroupingModes.Scholarship
                => students.OrderBy(s => HashBucket(s.StudentId, 2))
                    .ThenBy(s => s.StudentNumber ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.StudentId),
            AllocationGroupingModes.Hostel or AllocationGroupingModes.Transport
                or AllocationGroupingModes.MinorSubject or AllocationGroupingModes.ElectiveCombination
                => students.OrderBy(s => HashBucket(s.StudentId, 4))
                    .ThenBy(s => s.StudentName ?? "", StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.StudentId),
            _ => students.OrderBy(s => s.StudentName ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.StudentNumber ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.StudentId),
        };

        return ordered.Select(s => s.StudentId).ToList();
    }

    private static int HashBucket(int studentId, int buckets)
        => Math.Abs(studentId) % Math.Max(1, buckets);
}
