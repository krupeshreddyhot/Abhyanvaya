using System.Text.Json;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.15A Prompt 2 — optional Section scope on mark/edit DTOs.
/// Contract only: no authorization / eligibility enforcement in this prompt.
/// </summary>
public sealed class AI29_1D_15A_Prompt2_AttendanceSaveScopeContractTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Mark_And_Edit_Dtos_Expose_Optional_Section_Fields()
    {
        var markProps = typeof(MarkAttendanceRequest).GetProperties().Select(p => p.Name).ToHashSet();
        var editProps = typeof(EditAttendanceRequest).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("SectionId", markProps);
        Assert.Contains("SectionIds", markProps);
        Assert.Contains("SectionId", editProps);
        Assert.Contains("SectionIds", editProps);

        Assert.True(typeof(MarkAttendanceRequest).GetProperty("SectionId")!.PropertyType == typeof(int?));
        Assert.True(typeof(EditAttendanceRequest).GetProperty("SectionId")!.PropertyType == typeof(int?));
    }

    [Fact]
    public void Omitted_Section_Normalizes_To_Empty_Legacy_Behavior()
    {
        var mark = new MarkAttendanceRequest
        {
            SubjectId = 1,
            Date = DateTime.UtcNow.Date,
            Students = [new StudentAttendanceDto { StudentNumber = "S1", Status = AttendanceStatus.Present }],
        };
        var edit = new EditAttendanceRequest
        {
            SubjectId = 1,
            Date = DateTime.UtcNow.Date,
            Students = mark.Students,
        };

        Assert.Empty(AttendanceSaveScope.Normalize(mark));
        Assert.Empty(AttendanceSaveScope.Normalize(edit));
        Assert.False(AttendanceSaveScope.HasSectionScope(AttendanceSaveScope.Normalize(mark)));
    }

    [Fact]
    public void One_Section_Via_SectionIds()
    {
        var mark = new MarkAttendanceRequest { SectionIds = [11] };
        var ids = AttendanceSaveScope.Normalize(mark);

        Assert.Equal(new[] { 11 }, ids);
        Assert.True(AttendanceSaveScope.IsSingleSection(ids));
        Assert.False(AttendanceSaveScope.IsCombinedSection(ids));
    }

    [Fact]
    public void One_Section_Via_SectionId_Convenience()
    {
        var mark = new MarkAttendanceRequest { SectionId = 11 };
        Assert.Equal(new[] { 11 }, AttendanceSaveScope.Normalize(mark));
    }

    [Fact]
    public void Multiple_Sections_Are_Combined_Scope()
    {
        var edit = new EditAttendanceRequest { SectionIds = [11, 12, 13] };
        var ids = AttendanceSaveScope.Normalize(edit);

        Assert.Equal(3, ids.Count);
        Assert.Contains(11, ids);
        Assert.Contains(12, ids);
        Assert.Contains(13, ids);
        Assert.True(AttendanceSaveScope.IsCombinedSection(ids));
    }

    [Fact]
    public void Empty_Array_Normalizes_To_No_Section_Scope()
    {
        var mark = new MarkAttendanceRequest { SectionIds = [] };
        Assert.Empty(AttendanceSaveScope.Normalize(mark));
        Assert.Empty(AttendanceSaveScope.NormalizeRequestedIds(null, []));
    }

    [Fact]
    public void Duplicate_Section_Ids_Are_Normalized_Distinct_Not_Rejected()
    {
        // Existing AttendanceSectionScope convention: Distinct, not rejection.
        var ids = AttendanceSaveScope.NormalizeRequestedIds(11, [11, 12, 12, 0, -1]);
        Assert.Equal(2, ids.Count);
        Assert.Contains(11, ids);
        Assert.Contains(12, ids);
    }

    [Fact]
    public void SectionId_Merges_Into_SectionIds_Without_Duplicating()
    {
        var ids = AttendanceSaveScope.Normalize(
            new MarkAttendanceRequest { SectionId = 12, SectionIds = [11, 12] });
        Assert.Equal(2, ids.Count);
        Assert.Contains(11, ids);
        Assert.Contains(12, ids);
    }

    [Fact]
    public void CamelCase_Json_RoundTrip_Preserves_Optional_Section_Scope()
    {
        var json = """
            {
              "subjectId": 4,
              "date": "2026-08-09T00:00:00Z",
              "sectionId": 11,
              "sectionIds": [11, 12],
              "students": [{ "studentNumber": "A-001", "status": 1 }]
            }
            """;

        var mark = JsonSerializer.Deserialize<MarkAttendanceRequest>(json, CamelCase);
        Assert.NotNull(mark);
        Assert.Equal(4, mark!.SubjectId);
        Assert.Equal(11, mark.SectionId);
        Assert.Equal(new[] { 11, 12 }, mark.SectionIds);

        var normalized = AttendanceSaveScope.Normalize(mark);
        Assert.Equal(2, normalized.Count);

        var editJson = JsonSerializer.Serialize(
            new EditAttendanceRequest
            {
                SubjectId = 4,
                Date = mark.Date,
                SectionIds = [11, 12, 13],
                Students = mark.Students,
            },
            CamelCase);
        Assert.Contains("sectionIds", editJson);
        Assert.DoesNotContain("SectionIds", editJson);

        var edit = JsonSerializer.Deserialize<EditAttendanceRequest>(editJson, CamelCase);
        Assert.Equal(new[] { 11, 12, 13 }, edit!.SectionIds);
    }

    [Fact]
    public void Legacy_Clients_Omitting_Section_Fields_Still_Deserialize()
    {
        var json = """
            {
              "subjectId": 4,
              "date": "2026-08-09T00:00:00Z",
              "students": [{ "studentNumber": "A-001", "status": 1 }]
            }
            """;

        var mark = JsonSerializer.Deserialize<MarkAttendanceRequest>(json, CamelCase);
        Assert.NotNull(mark);
        Assert.Null(mark!.SectionId);
        Assert.Null(mark.SectionIds);
        Assert.Empty(AttendanceSaveScope.Normalize(mark));
    }

    [Fact]
    public void Contract_Does_Not_Add_Combined_Class_Write_Fields_Or_Second_Resolver()
    {
        var markProps = typeof(MarkAttendanceRequest).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain(markProps, n => n.Contains("Combined", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(markProps, n => n.Equals("SectionGroupId", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("AttendanceSessionResolver", typeof(AttendanceSessionResolver).Name);
    }
}
