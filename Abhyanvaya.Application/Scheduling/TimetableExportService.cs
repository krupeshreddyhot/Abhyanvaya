using Abhyanvaya.Application.DTOs.Scheduling;
using ClosedXML.Excel;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimetableExportService : ITimetableExportService
{
    private static readonly string[] DayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private readonly ITimetableService _timetableService;

    public TimetableExportService(ITimetableService timetableService) => _timetableService = timetableService;

    public async Task<byte[]> ExportFacultyExcelAsync(int timetableId, int staffId, CancellationToken cancellationToken = default)
    {
        var projection = await _timetableService.GetFacultyProjectionAsync(timetableId, staffId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        return BuildWorkbook(projection.Timetable.Name, $"Faculty: {projection.Entries.FirstOrDefault()?.StaffName ?? staffId.ToString()}", projection.Entries);
    }

    public async Task<byte[]> ExportStudentExcelAsync(int timetableId, int courseId, int groupId, int semesterId, CancellationToken cancellationToken = default)
    {
        var projection = await _timetableService.GetStudentProjectionAsync(timetableId, courseId, groupId, semesterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        var label = projection.Entries.FirstOrDefault();
        var title = label is null
            ? $"Course {courseId} / Group {groupId} / Semester {semesterId}"
            : $"{label.CourseName} / {label.GroupName} / {label.SemesterName}";
        return BuildWorkbook(projection.Timetable.Name, title, projection.Entries);
    }

    public async Task<byte[]> ExportRoomExcelAsync(int timetableId, int roomId, CancellationToken cancellationToken = default)
    {
        var projection = await _timetableService.GetRoomProjectionAsync(timetableId, roomId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        return BuildWorkbook(projection.Timetable.Name, $"Room: {projection.Entries.FirstOrDefault()?.RoomName ?? roomId.ToString()}", projection.Entries);
    }

    public async Task<byte[]> ExportDepartmentExcelAsync(int timetableId, int departmentId, CancellationToken cancellationToken = default)
    {
        var projection = await _timetableService.GetDepartmentProjectionAsync(timetableId, departmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        return BuildWorkbook(projection.Timetable.Name, $"Department: {projection.Entries.FirstOrDefault()?.DepartmentName ?? departmentId.ToString()}", projection.Entries);
    }

    private static byte[] BuildWorkbook(string timetableName, string viewTitle, IReadOnlyList<TimetableEntryDto> entries)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Timetable");
        sheet.Cell(1, 1).Value = timetableName;
        sheet.Cell(2, 1).Value = viewTitle;

        var headers = new[] { "Day", "Period", "Start", "End", "Subject", "Staff", "Room", "Course", "Group", "Semester", "Remarks" };
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(4, c + 1).Value = headers[c];

        var row = 5;
        foreach (var e in entries.OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime))
        {
            sheet.Cell(row, 1).Value = DayNames[e.DayOfWeek];
            sheet.Cell(row, 2).Value = e.TimeSlotName ?? e.TimeSlotId.ToString();
            sheet.Cell(row, 3).Value = e.StartTime?.ToString(@"hh\:mm");
            sheet.Cell(row, 4).Value = e.EndTime?.ToString(@"hh\:mm");
            sheet.Cell(row, 5).Value = e.SubjectName ?? e.SubjectId.ToString();
            sheet.Cell(row, 6).Value = e.StaffName ?? e.StaffId.ToString();
            sheet.Cell(row, 7).Value = e.RoomName ?? e.RoomId.ToString();
            sheet.Cell(row, 8).Value = e.CourseName ?? e.CourseId.ToString();
            sheet.Cell(row, 9).Value = e.GroupName ?? e.GroupId.ToString();
            sheet.Cell(row, 10).Value = e.SemesterName ?? e.SemesterId.ToString();
            sheet.Cell(row, 11).Value = e.Remarks ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
