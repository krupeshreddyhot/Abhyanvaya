using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.15A Prompt 9 — regression + architecture guards for attendance save scope and faculty allocation.
/// </summary>
public sealed class AI29_1D_15A_Prompt9_RegressionArchitectureGuardTests
{
    private static string RepoPath(params string[] parts) =>
        Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", ".." }.Concat(parts).ToArray()));

    [Fact]
    public void Architecture_Single_AttendanceSessionResolver()
    {
        var resolvers = typeof(AttendanceSessionResolver).Assembly
            .GetTypes()
            .Where(t => t.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.Contains(resolvers, n => n!.EndsWith(".AttendanceSessionResolver", StringComparison.Ordinal));
        Assert.Single(resolvers.Where(n => n!.EndsWith(".AttendanceSessionResolver", StringComparison.Ordinal)));
        Assert.DoesNotContain(resolvers, n => n!.Contains("AttendanceSessionResolverV2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Architecture_No_Second_SectionGroup_Or_FacultySection_Entity()
    {
        var domain = typeof(SectionGroup).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
        Assert.Contains("SectionGroup", domain);
        Assert.Contains("FacultySectionAssignment", domain);
        Assert.DoesNotContain("FacultySection", domain); // exact alternate entity name
        Assert.DoesNotContain("CombinedFacultySection", domain);
        Assert.DoesNotContain("CombinedSectionAssignment", domain);
        Assert.DoesNotContain("OperationalClassEntity", domain);

        var fsa = typeof(FacultySectionAssignment).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("SectionGroupId", fsa);
        Assert.DoesNotContain("SubjectId", fsa);
    }

    [Fact]
    public void Architecture_Subject_Master_Has_No_Section_Relationship()
    {
        var subjectType = typeof(FacultySectionAssignment).Assembly
            .GetTypes()
            .First(t => t.Name == "Subject" && t.Namespace?.Contains("Entities") == true);
        var props = subjectType.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("SectionId", props);
        Assert.DoesNotContain("SectionIds", props);
        Assert.DoesNotContain("SectionGroupId", props);
    }

    [Fact]
    public void Architecture_Allocation_And_Scoring_Engines_Untouched_As_Singletons()
    {
        Assert.Equal("AllocationEngine", typeof(AllocationEngine).Name);
        var scoring = typeof(AllocationEngine).Assembly.GetTypes()
            .Where(t => t.Name.Contains("Scor", StringComparison.OrdinalIgnoreCase)
                        && t.Name.Contains("Allocation", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Name)
            .ToList();
        // Guard: no Parallel/V2 allocation scoring type introduced by 15A.
        Assert.DoesNotContain(scoring, n => n.Contains("V2", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(scoring, n => n.Contains("Parallel", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Attendance_Save_Contract_Remains_Additive_And_Optional_Section()
    {
        var mark = typeof(MarkAttendanceRequest).GetProperties().Select(p => p.Name).ToHashSet();
        var edit = typeof(EditAttendanceRequest).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("SubjectId", mark);
        Assert.Contains("Date", mark);
        Assert.Contains("Students", mark);
        Assert.Contains("SectionId", mark);
        Assert.Contains("SectionIds", mark);
        Assert.Contains("SectionId", edit);
        Assert.Contains("SectionIds", edit);

        Assert.Empty(AttendanceSaveScope.Normalize(new MarkAttendanceRequest()));
        Assert.False(AttendanceSaveScope.HasSectionScope(AttendanceSaveScope.Normalize(new MarkAttendanceRequest())));
    }

    [Fact]
    public void Attendance_Write_Rejects_Unauthorized_Students_Atomically()
    {
        Assert.Equal(0, AttendanceSaveScope.CountAtomicCommitOrZero(100, 99));
        Assert.Equal(100, AttendanceSaveScope.CountAtomicCommitOrZero(100, 100));
        Assert.Equal(
            AttendanceSaveScope.UnauthorizedStudentsMessage,
            AttendanceSaveScope.EnsureAllSubmittedStudentsAuthorized(["A-001", "B-001"], ["A-001"]));
        Assert.Contains("No attendance was saved", AttendanceSaveScope.UnauthorizedStudentsMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Attendance_Controller_Uses_Resolver_Independent_Write_Scope_And_Transactions()
    {
        var source = File.ReadAllText(RepoPath("Abhyanvaya.API", "Controllers", "AttendanceController.cs"));
        Assert.Contains("ValidateWriteSectionScopeAsync", source);
        Assert.Contains("ValidateEverySubmittedStudentInSectionScopeAsync", source);
        Assert.Contains("BuildAtomicMarkRows", source);
        Assert.Contains("ExecuteInTransactionAsync", source);
        Assert.DoesNotContain("new AttendanceSessionResolver", source);
    }

    [Fact]
    public void Faculty_Assign_Api_Contract_Still_Uses_FacultyId_Staff_Id()
    {
        var props = typeof(AssignFacultySectionRequest).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("FacultyId", props);
        Assert.Contains("SectionId", props);
        Assert.Contains("AcademicYearId", props);
        Assert.Equal("FacultySectionDto", typeof(FacultySectionDto).Name);
    }

    [Fact]
    public void Faculty_Assign_Authorization_Helper_Present()
    {
        Assert.Equal(
            "FacultySectionAssignmentAuthorization",
            typeof(FacultySectionAssignmentAuthorization).Name);
        Assert.False(string.IsNullOrWhiteSpace(FacultySectionAssignmentAuthorization.UnauthorizedFacultyMessage));
        Assert.False(string.IsNullOrWhiteSpace(FacultySectionAssignmentAuthorization.SectionOutOfAcademicScopeMessage));
    }

    [Fact]
    public void Faculty_Allocation_Ui_Removed_Manual_Staff_Id_Entry()
    {
        var panel = File.ReadAllText(RepoPath(
            "abhyanvaya-ui", "src", "components", "sections", "FacultySectionAllocationPanel.tsx"));
        Assert.Contains("FacultyStaffSelector", panel);
        Assert.DoesNotContain("Faculty (Staff) Id", panel);
        Assert.Contains("operationalClassLabel", panel);
        Assert.Contains("Underlying Sections", panel);
        Assert.Contains("Assignment IDs", panel);
    }

    [Fact]
    public void Attendance_Ui_Write_Payload_Builder_Exists_Without_Client_Eligibility()
    {
        var scope = File.ReadAllText(RepoPath(
            "abhyanvaya-ui", "src", "utils", "attendanceMarkingScope.ts"));
        Assert.Contains("buildAttendanceWritePayload", scope);
        Assert.Contains("server is authoritative", scope, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("buildAttendanceSaveScope", scope);
    }
}
