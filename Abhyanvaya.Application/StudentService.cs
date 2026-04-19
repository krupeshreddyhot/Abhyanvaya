using System.Globalization;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Student;
using Abhyanvaya.Domain.Entities;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application
{
    public class StudentService : IStudentService
    {
        private const int MaxErrorMessages = 100;
        private static readonly string[] DateFormats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy" };

        private readonly IApplicationDbContext _context;

        public StudentService(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UploadStudentsResultDto> UploadStudentsAsync(
            Stream fileStream,
            int tenantId,
            CancellationToken cancellationToken = default)
        {
            var result = new UploadStudentsResultDto();

            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.First();
            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                result.Errors.Add("The worksheet is empty.");
                return result;
            }

            var headers = BuildHeaderMap(headerRow);
            var studentNumberCol = Col(headers, "StudentNumber", "student_number", "Student_Number");
            if (studentNumberCol == null)
            {
                result.Errors.Add("Required column \"StudentNumber\" was not found in the header row.");
                return result;
            }

            var nameCol = Col(headers, "Name", "name");
            if (nameCol == null)
            {
                result.Errors.Add("Required column \"Name\" was not found in the header row.");
                return result;
            }

            var tenantCollege = await _context.Colleges
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

            if (tenantCollege == null)
            {
                result.Errors.Add("No college profile exists for this tenant. Complete Tenant Setup before importing students.");
                return result;
            }

            var defaultFirstLanguageId = await GetOrCreateEnglishLanguageIdAsync(tenantId, cancellationToken);

            var collegeCodeCol = Col(headers, "college_code", "College_Code");

            var existingNumbers = await _context.Students
                .Where(s => s.TenantId == tenantId)
                .Select(s => s.StudentNumber)
                .ToListAsync(cancellationToken);

            var existingSet = new HashSet<string>(existingNumbers, StringComparer.Ordinal);
            var seenInFile = new HashSet<string>(StringComparer.Ordinal);

            var rows = worksheet.RowsUsed().Skip(1);
            var toAdd = new List<Student>();

            foreach (var row in rows)
            {
                if (result.Errors.Count >= MaxErrorMessages)
                    break;

                var excelRow = row.RowNumber();
                var studentNumber = NormalizeStudentNumber(GetCellText(row, studentNumberCol.Value));

                if (string.IsNullOrWhiteSpace(studentNumber))
                    continue;

                if (seenInFile.Contains(studentNumber))
                {
                    result.Errors.Add($"Row {excelRow}: duplicate StudentNumber \"{studentNumber}\" in this file.");
                    result.Skipped++;
                    continue;
                }

                seenInFile.Add(studentNumber);

                if (existingSet.Contains(studentNumber))
                {
                    result.Errors.Add($"Row {excelRow}: StudentNumber \"{studentNumber}\" already exists for this tenant.");
                    result.Skipped++;
                    continue;
                }

                if (collegeCodeCol != null)
                {
                    var codeRaw = GetCellText(row, collegeCodeCol.Value);
                    if (!string.IsNullOrWhiteSpace(codeRaw) &&
                        !string.Equals(codeRaw.Trim(), tenantCollege.Code, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Errors.Add(
                            $"Row {excelRow}: college_code \"{codeRaw.Trim()}\" does not match this tenant college \"{tenantCollege.Code}\".");
                        result.Skipped++;
                        continue;
                    }
                }

                var name = GetCellText(row, nameCol);
                if (string.IsNullOrWhiteSpace(name))
                {
                    result.Errors.Add($"Row {excelRow}: Name is required.");
                    result.Skipped++;
                    continue;
                }

                if (!TryParseRequiredInt(row, headers, excelRow, out var courseId, result, "CourseId", "course_id"))
                {
                    result.Skipped++;
                    continue;
                }

                if (!TryParseRequiredInt(row, headers, excelRow, out var groupId, result, "GorupID", "GroupId", "group_id", "Group_ID"))
                {
                    result.Skipped++;
                    continue;
                }

                if (!TryParseRequiredInt(row, headers, excelRow, out var genderId, result, "GenderId", "gender_id"))
                {
                    result.Skipped++;
                    continue;
                }

                if (!TryParseRequiredInt(row, headers, excelRow, out var mediumId, result, "MediumId", "medium_id"))
                {
                    result.Skipped++;
                    continue;
                }

                if (!TryParseRequiredInt(row, headers, excelRow, out var languageId, result, "LanguageId", "language_id"))
                {
                    result.Skipped++;
                    continue;
                }

                if (!TryParseRequiredInt(row, headers, excelRow, out var semesterId, result, "SemesterId", "semester_id"))
                {
                    result.Skipped++;
                    continue;
                }

                var batch = TryParseOptionalInt(row, headers, "Batch", "batch");
                var dob = TryParseDate(row, headers, "DateOfBirth", "date_of_birth", "DOB");

                var appraCol = Col(headers, "AppraId", "AppraID", "appra_id");
                var appraId = appraCol == null ? null : NullIfEmpty(GetCellText(row, appraCol.Value));

                var mobile = NullIfEmpty(GetCellText(row, Col(headers, "MobileNumber", "mobile_number")));
                var altMobile = NormalizePhone(GetCellText(row, Col(headers, "AlternateMobileNumber", "alternate_mobile_number")));
                var email = NullIfEmpty(GetCellText(row, Col(headers, "Email", "email")));
                var parentMobile = NormalizePhone(GetCellText(row, Col(headers, "ParentMobileNumber", "parent_mobile_number")));
                var parentAltMobile = NormalizePhone(GetCellText(row, Col(headers, "ParentAlternateMobileNumber", "parent_alternate_mobile_number")));
                var father = NullIfEmpty(GetCellText(row, Col(headers, "FatherName", "father_name")));
                var mother = NullIfEmpty(GetCellText(row, Col(headers, "MotherName", "mother_name")));

                var firstLanguageId = defaultFirstLanguageId;
                var firstLangCol = Col(headers, "FirstLanguageId", "first_language_id", "First_Language_Id");
                if (firstLangCol != null)
                {
                    var flText = GetCellText(row, firstLangCol.Value);
                    if (!string.IsNullOrWhiteSpace(flText) &&
                        int.TryParse(flText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var flId) &&
                        flId > 0)
                    {
                        var flOk = await _context.Languages.AnyAsync(
                            l => l.Id == flId && l.TenantId == tenantId,
                            cancellationToken);
                        if (!flOk)
                        {
                            result.Errors.Add($"Row {excelRow}: FirstLanguageId {flId} is not valid for this tenant.");
                            result.Skipped++;
                            continue;
                        }

                        firstLanguageId = flId;
                    }
                }

                var student = new Student
                {
                    StudentNumber = studentNumber,
                    AppraId = appraId,
                    Name = name.Trim(),
                    CourseId = courseId,
                    GroupId = groupId,
                    GenderId = genderId,
                    MediumId = mediumId,
                    FirstLanguageId = firstLanguageId,
                    LanguageId = languageId,
                    SemesterId = semesterId,
                    Batch = batch,
                    DateOfBirth = dob,
                    MobileNumber = mobile,
                    AlternateMobileNumber = altMobile,
                    Email = email,
                    ParentMobileNumber = parentMobile,
                    ParentAlternateMobileNumber = parentAltMobile,
                    FatherName = father,
                    MotherName = mother,
                    TenantId = tenantId,
                    CreatedDate = DateTime.UtcNow
                };

                toAdd.Add(student);
                existingSet.Add(studentNumber);
            }

            if (toAdd.Count > 0)
            {
                await _context.AddRangeAsync(toAdd);
                await _context.SaveChangesAsync(cancellationToken);
                result.Imported = toAdd.Count;
            }

            return result;
        }

        private async Task<int> GetOrCreateEnglishLanguageIdAsync(int tenantId, CancellationToken cancellationToken)
        {
            var lang = await _context.Languages
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Name == "English", cancellationToken);
            if (lang != null)
                return lang.Id;

            lang = new Language
            {
                Name = "English",
                TenantId = tenantId,
                CreatedDate = DateTime.UtcNow
            };
            await _context.AddAsync(lang);
            await _context.SaveChangesAsync(cancellationToken);
            return lang.Id;
        }

        private static bool TryParseRequiredInt(
            IXLRow row,
            Dictionary<string, int> headers,
            int excelRow,
            out int value,
            UploadStudentsResultDto result,
            params string[] headerNames)
        {
            value = 0;
            var col = Col(headers, headerNames);
            if (col == null)
            {
                result.Errors.Add($"Row {excelRow}: missing column \"{headerNames[0]}\".");
                return false;
            }

            var text = GetCellText(row, col.Value);
            if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value <= 0)
            {
                result.Errors.Add($"Row {excelRow}: \"{headerNames[0]}\" must be a positive integer.");
                return false;
            }

            return true;
        }

        private static int? TryParseOptionalInt(IXLRow row, Dictionary<string, int> headers, params string[] headerNames)
        {
            var col = Col(headers, headerNames);
            if (col == null)
                return null;

            var text = GetCellText(row, col.Value);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        private static DateTime? TryParseDate(IXLRow row, Dictionary<string, int> headers, params string[] headerNames)
        {
            var col = Col(headers, headerNames);
            if (col == null)
                return null;

            var text = GetCellText(row, col.Value);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            foreach (var fmt in DateFormats)
            {
                if (DateTime.TryParseExact(text.Trim(), fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc);
            }

            if (DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var fallback))
                return DateTime.SpecifyKind(fallback.Date, DateTimeKind.Utc);

            return null;
        }

        private static string? NullIfEmpty(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            return s.Trim();
        }

        private static string? NormalizePhone(string? s)
        {
            var t = NullIfEmpty(s);
            if (t == null || t == "-")
                return null;
            return t;
        }

        private static string NormalizeStudentNumber(string raw)
        {
            return raw.Trim();
        }

        private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.CellsUsed())
            {
                var key = cell.GetText().Trim();
                if (string.IsNullOrEmpty(key))
                    continue;
                map[key] = cell.Address.ColumnNumber;
            }

            return map;
        }

        private static int? Col(Dictionary<string, int> headers, params string[] names)
        {
            foreach (var name in names)
            {
                if (headers.TryGetValue(name, out var col))
                    return col;
            }

            return null;
        }

        private static string GetCellText(IXLRow row, int colIndex)
        {
            if (colIndex <= 0)
                return "";

            var cell = row.Cell(colIndex);
            if (cell.IsEmpty())
                return "";

            try
            {
                var t = cell.GetText().Trim();
                if (!string.IsNullOrEmpty(t))
                    return t;
            }
            catch
            {
                // fall through
            }

            if (cell.DataType == XLDataType.Number)
            {
                var d = cell.GetDouble();
                if (Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < 1e15)
                    return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            }

            return cell.Value.ToString().Trim();
        }

        private static string GetCellText(IXLRow row, int? colIndex)
        {
            return colIndex == null ? "" : GetCellText(row, colIndex.Value);
        }
    }
}
