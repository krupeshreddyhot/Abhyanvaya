using System.Text.Json.Nodes;
using Abhyanvaya.Application.Academic.Allocation;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24B.4A.3 Prompt 3 — Determinism and true-drift tests for
/// <see cref="AllocationAcademicContextIntegrity"/> v2.0.0.
/// </summary>
public sealed class AllocationAcademicContextIntegrityTests
{
    [Fact]
    public void Algorithm_version_is_2_0_0()
        => Assert.Equal("2.0.0", AllocationAcademicContextIntegrity.AlgorithmVersion);

    [Fact]
    public void Test1_ContextId_independence()
    {
        var a = BaseContext(contextId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var b = BaseContext(contextId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.NotEqual(a.ContextId, b.ContextId);
        Assert.Equal(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test2_Section_collection_ordering_independent()
    {
        var sectionsAsc = new[]
        {
            Section(5, "B"),
            Section(13, "A"),
            Section(14, "C"),
        };
        var sectionsDesc = sectionsAsc.Reverse().ToArray();
        var capsAsc = new[]
        {
            Capacity(5, max: 60, min: 0, reserved: 0),
            Capacity(13, max: 50, min: 10, reserved: 2),
            Capacity(14, max: 40, min: 0, reserved: 1),
        };
        var capsDesc = capsAsc.Reverse().ToArray();

        var a = BaseContext(sections: sectionsAsc, capacities: capsAsc);
        var b = BaseContext(sections: sectionsDesc, capacities: capsDesc);
        Assert.Equal(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test3_Student_collection_ordering_independent()
    {
        var studentsA = new[] { Student(3), Student(1), Student(2) };
        var studentsB = new[] { Student(1), Student(2), Student(3) };
        var a = BaseContext(students: studentsA);
        var b = BaseContext(students: studentsB);

        Assert.Equal(
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(studentsA.Select(s => s.StudentId)),
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(studentsB.Select(s => s.StudentId)));
        Assert.Equal(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test4_Faculty_collection_ordering_independent()
    {
        var facultyA = new[]
        {
            Faculty(10, 5, "Primary"),
            Faculty(20, 13, "Assistant"),
            Faculty(15, 5, "Advisor"),
        };
        var facultyB = facultyA.Reverse().ToArray();
        var a = BaseContext(faculty: facultyA);
        var b = BaseContext(faculty: facultyB);

        Assert.Equal(
            AllocationAcademicContextIntegrity.ComputeFacultyAssignmentChecksum(facultyA),
            AllocationAcademicContextIntegrity.ComputeFacultyAssignmentChecksum(facultyB));
        Assert.Equal(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test5_Occupancy_independence()
    {
        var baseCaps = new[] { Capacity(5, max: 60, min: 0, reserved: 0, current: 10, available: 50, occupancy: 16.67, waiting: 0, status: "Ok") };
        var driftedCaps = new[] { Capacity(5, max: 60, min: 0, reserved: 0, current: 40, available: 5, occupancy: 90.0, waiting: 3, status: "NearFull") };
        var a = BaseContext(capacities: baseCaps);
        var b = BaseContext(capacities: driftedCaps);
        Assert.Equal(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test6_Section_addition_changes_checksum()
    {
        var a = BaseContext(
            sections: [Section(5)],
            capacities: [Capacity(5, 60)]);
        var b = BaseContext(
            sections: [Section(5), Section(13)],
            capacities: [Capacity(5, 60), Capacity(13, 60)]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test7_Section_removal_changes_checksum()
    {
        var a = BaseContext(
            sections: [Section(5), Section(13)],
            capacities: [Capacity(5, 60), Capacity(13, 60)]);
        var b = BaseContext(
            sections: [Section(5)],
            capacities: [Capacity(5, 60)]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test8_MaximumCapacity_change_changes_checksum()
    {
        var a = BaseContext(capacities: [Capacity(5, max: 60)]);
        var b = BaseContext(capacities: [Capacity(5, max: 50)]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test9_MinimumCapacity_change_changes_checksum()
    {
        var a = BaseContext(capacities: [Capacity(5, max: 60, min: 0)]);
        var b = BaseContext(capacities: [Capacity(5, max: 60, min: 10)]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test10_ReservedSeats_change_changes_checksum()
    {
        var a = BaseContext(capacities: [Capacity(5, max: 60, reserved: 0)]);
        var b = BaseContext(capacities: [Capacity(5, max: 60, reserved: 5)]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test11_Student_addition_changes_population_and_context_checksum()
    {
        var a = BaseContext(students: [Student(1), Student(2), Student(3)]);
        var b = BaseContext(students: [Student(1), Student(2), Student(3), Student(4)]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(a.Students.Select(s => s.StudentId)),
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(b.Students.Select(s => s.StudentId)));
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test12_Student_removal_changes_population_and_context_checksum()
    {
        var a = BaseContext(students: [Student(1), Student(2), Student(3)]);
        var b = BaseContext(students: [Student(1), Student(2)]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(a.Students.Select(s => s.StudentId)),
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(b.Students.Select(s => s.StudentId)));
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test13_Same_count_different_student_detected()
    {
        var a = BaseContext(students: [Student(1), Student(2), Student(3)]);
        var b = BaseContext(students: [Student(1), Student(2), Student(4)]);
        Assert.Equal(a.Students.Count, b.Students.Count);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(a.Students.Select(s => s.StudentId)),
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(b.Students.Select(s => s.StudentId)));
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Theory]
    [InlineData(nameof(AllocationHierarchyProjection.AcademicYearId))]
    [InlineData(nameof(AllocationHierarchyProjection.ProgramId))]
    [InlineData(nameof(AllocationHierarchyProjection.CourseId))]
    [InlineData(nameof(AllocationHierarchyProjection.GroupId))]
    [InlineData(nameof(AllocationHierarchyProjection.SemesterId))]
    public void Test14_Academic_hierarchy_change_changes_checksum(string field)
    {
        var baseline = BaseContext();
        var changedHierarchy = field switch
        {
            nameof(AllocationHierarchyProjection.AcademicYearId) => Hierarchy(academicYearId: 99),
            nameof(AllocationHierarchyProjection.ProgramId) => Hierarchy(programId: 77),
            nameof(AllocationHierarchyProjection.CourseId) => Hierarchy(courseId: 88),
            nameof(AllocationHierarchyProjection.GroupId) => Hierarchy(groupId: 66),
            nameof(AllocationHierarchyProjection.SemesterId) => Hierarchy(semesterId: 55),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        var changed = BaseContext(hierarchy: changedHierarchy);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(baseline),
            AllocationAcademicContextIntegrity.Compute(changed));
    }

    [Fact]
    public void Test15_Faculty_binding_change_changes_checksum()
    {
        var a = BaseContext(faculty: [Faculty(10, 5, "Primary")]);
        var b = BaseContext(faculty: [Faculty(99, 5, "Primary")]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.ComputeFacultyAssignmentChecksum(a.FacultyAssignments),
            AllocationAcademicContextIntegrity.ComputeFacultyAssignmentChecksum(b.FacultyAssignments));
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));

        var c = BaseContext(faculty: [Faculty(10, 13, "Primary")]);
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(c));
    }

    [Fact]
    public void Test16_Algorithm_version_in_payload_and_affects_checksum()
    {
        var ctx = BaseContext();
        var payload = AllocationAcademicContextIntegrity.BuildCanonicalPayload(ctx);
        Assert.Equal("2.0.0", payload["integrityAlgorithmVersion"]?.GetValue<string>());

        var mutated = JsonNode.Parse(payload.ToJsonString())!.AsObject();
        mutated["integrityAlgorithmVersion"] = "9.9.9";
        var originalHash = AllocationAcademicContextIntegrity.Compute(ctx);
        var mutatedHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(mutated.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false }))));
        Assert.NotEqual(originalHash, mutatedHash);
    }

    [Fact]
    public void Test17_Null_ProgramId_serializes_deterministically()
    {
        var a = BaseContext(hierarchy: Hierarchy(programId: null));
        var b = BaseContext(hierarchy: Hierarchy(programId: null));
        var payload = AllocationAcademicContextIntegrity.BuildCanonicalPayload(a);
        Assert.True(payload["hierarchy"]!.AsObject().ContainsKey("programId"));
        Assert.Null(payload["hierarchy"]!["programId"]);
        Assert.Equal(
            AllocationAcademicContextIntegrity.Compute(a),
            AllocationAcademicContextIntegrity.Compute(b));
    }

    [Fact]
    public void Test18_Scope_population_vs_allocation_filter_boundary()
    {
        // Academic pool includes students 1..5. Scenario LastThreeDigits 001–003 would exclude 5,
        // but academic integrity still hashes the full context pool — Student 5 drift must matter.
        var withStudent5 = BaseContext(students: [Student(1), Student(2), Student(3), Student(4), Student(5)]);
        var withoutStudent5 = BaseContext(students: [Student(1), Student(2), Student(3), Student(4)]);

        Assert.NotEqual(
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(withStudent5.Students.Select(s => s.StudentId)),
            AllocationAcademicContextIntegrity.ComputePopulationChecksum(withoutStudent5.Students.Select(s => s.StudentId)));
        Assert.NotEqual(
            AllocationAcademicContextIntegrity.Compute(withStudent5),
            AllocationAcademicContextIntegrity.Compute(withoutStudent5));
    }

    [Fact]
    public void Test19_Generate_Review_invariant_ContextId_differs_Checksum_same_NotStale()
    {
        var generate = BaseContext(contextId: Guid.NewGuid());
        var storedChecksum = AllocationAcademicContextIntegrity.Compute(generate);

        var review = BaseContext(contextId: Guid.NewGuid());
        var currentChecksum = AllocationAcademicContextIntegrity.Compute(review);

        Assert.NotEqual(generate.ContextId, review.ContextId);
        Assert.Equal(storedChecksum, currentChecksum);

        var contextStale = !string.Equals(currentChecksum, storedChecksum, StringComparison.OrdinalIgnoreCase);
        Assert.False(contextStale);
    }

    [Fact]
    public void Test20_True_drift_MaximumCapacity_keeps_stale_gate()
    {
        var generate = BaseContext(capacities: [Capacity(5, max: 60)]);
        var storedChecksum = AllocationAcademicContextIntegrity.Compute(generate);

        var review = BaseContext(capacities: [Capacity(5, max: 50)]);
        var currentChecksum = AllocationAcademicContextIntegrity.Compute(review);

        Assert.NotEqual(storedChecksum, currentChecksum);
        var contextStale = !string.Equals(currentChecksum, storedChecksum, StringComparison.OrdinalIgnoreCase);
        Assert.True(contextStale);

        // Governance model: stale ⇒ approval blocked (gate expression preserved).
        var canApprove = !contextStale;
        Assert.False(canApprove);
    }

    [Fact]
    public void Test21_True_drift_population_keeps_stale_gate()
    {
        var generate = BaseContext(students: [Student(1), Student(2), Student(3)]);
        var storedChecksum = AllocationAcademicContextIntegrity.Compute(generate);

        var review = BaseContext(students: [Student(1), Student(2), Student(3), Student(4)]);
        var currentChecksum = AllocationAcademicContextIntegrity.Compute(review);

        Assert.NotEqual(storedChecksum, currentChecksum);
        var contextStale = !string.Equals(currentChecksum, storedChecksum, StringComparison.OrdinalIgnoreCase);
        Assert.True(contextStale);
        Assert.False(!contextStale); // canApprove blocked when stale
    }

    [Fact]
    public void Empty_faculty_hashes_empty_array()
    {
        var empty = AllocationAcademicContextIntegrity.ComputeFacultyAssignmentChecksum([]);
        var expected = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("[]")));
        Assert.Equal(expected, empty);
    }

    [Fact]
    public void ContextId_and_GeneratedAt_excluded_from_payload()
    {
        var payload = AllocationAcademicContextIntegrity.BuildCanonicalPayload(BaseContext());
        var json = payload.ToJsonString();
        Assert.DoesNotContain("contextId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("generatedAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentStrength", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("occupancyPercent", json, StringComparison.OrdinalIgnoreCase);
    }

    // --- helpers ---

    private static SectionAllocationContext BaseContext(
        Guid? contextId = null,
        AllocationHierarchyProjection? hierarchy = null,
        IReadOnlyList<AllocationSectionProjection>? sections = null,
        IReadOnlyList<AllocationCapacityProjection>? capacities = null,
        IReadOnlyList<AllocationStudentProjection>? students = null,
        IReadOnlyList<AllocationFacultyProjection>? faculty = null)
    {
        sections ??= [Section(5, "SCA-01")];
        capacities ??= [Capacity(5, max: 60, min: 0, reserved: 0)];
        students ??= [Student(1), Student(2), Student(3)];
        faculty ??= [Faculty(10, 5, "Primary")];
        hierarchy ??= Hierarchy();

        return new SectionAllocationContext
        {
            ContextId = contextId ?? Guid.NewGuid(),
            ContextVersion = "1",
            SchemaVersion = SectionAllocationContext.CurrentSchemaVersion,
            GeneratedAt = DateTime.UtcNow,
            Checksum = "",
            Hierarchy = hierarchy,
            Sections = sections,
            Capacities = capacities,
            Students = students,
            FacultyAssignments = faculty,
            SubjectAssignments = [],
            RoomAvailability = [],
            Policies = ["ignored prose"],
            Recommendations = ["ignored advice"],
            Metadata = new Dictionary<string, string> { ["TenantId"] = "ignored" },
            OverallHealth = "Healthy",
            OverallReadiness = "Ready",
            TimetableStatus = "Mapped",
        };
    }

    private static AllocationHierarchyProjection Hierarchy(
        int academicYearId = 1,
        int? programId = 2,
        int courseId = 1,
        int groupId = 2,
        int semesterId = 3)
        => new()
        {
            AcademicYearId = academicYearId,
            AcademicYearName = "AY",
            ProgramId = programId,
            ProgramName = "Prog",
            CourseId = courseId,
            CourseName = "Course",
            GroupId = groupId,
            GroupName = "Group",
            SemesterId = semesterId,
            SemesterName = "Sem",
        };

    private static AllocationSectionProjection Section(int id, string code = "S")
        => new()
        {
            SectionId = id,
            SectionCode = code,
            SectionName = code,
            SectionType = "Regular",
            Lifecycle = "Active",
            Health = "Healthy",
            Readiness = "Ready",
            DisplayOrder = id * 10,
        };

    private static AllocationCapacityProjection Capacity(
        int sectionId,
        int max = 60,
        int min = 0,
        int reserved = 0,
        int current = 0,
        int available = 60,
        double occupancy = 0,
        int waiting = 0,
        string status = "Ok")
        => new()
        {
            SectionId = sectionId,
            MaximumCapacity = max,
            MinimumCapacity = min,
            RecommendedCapacity = max,
            CurrentStrength = current,
            AvailableCapacity = available,
            ReservedSeats = reserved,
            WaitingList = waiting,
            OccupancyPercent = occupancy,
            CapacityStatus = status,
        };

    private static AllocationStudentProjection Student(int id)
        => new()
        {
            StudentId = id,
            StudentNumber = $"SN{id:D4}",
            StudentName = $"Student {id}",
            CurrentSectionId = null,
        };

    private static AllocationFacultyProjection Faculty(int facultyId, int sectionId, string role)
        => new()
        {
            FacultyId = facultyId,
            FacultyName = $"Faculty {facultyId}",
            SectionId = sectionId,
            Role = role,
        };
}
