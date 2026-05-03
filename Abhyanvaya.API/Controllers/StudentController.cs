using Microsoft.AspNetCore.Authorization;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Application.DTOs.Student;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Abhyanvaya.Application;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AuthorizationPolicies.CanViewStudents)]
    public class StudentController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IStudentService _studentService;

        public StudentController(IApplicationDbContext context, ICurrentUserService currentUser, IStudentService studentService)
        {
            _context = context;
            _currentUser = currentUser;
            _studentService = studentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetStudents(
            string? search = null,
            int? batch = null,
            int? courseId = null,
            int? groupId = null,
            int? semesterId = null,
            int pageNumber = 1,
            int pageSize = 30)
        {
            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0) pageSize = 30;
            if (pageSize > 200) pageSize = 200;

            // SuperAdmin JWT has TenantId 0; explicit tenant filter would return no rows. Global query filter already scopes non–Super Admin.
            IQueryable<Student> query = _context.Students;
            if (!string.Equals(_currentUser.Role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.TenantId == _currentUser.TenantId);
            }

            if (_currentUser.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    x.CourseId == _currentUser.CourseId &&
                    x.GroupId == _currentUser.GroupId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(x =>
                    x.StudentNumber.ToLower().Contains(s) ||
                    x.Name.ToLower().Contains(s) ||
                    (x.MobileNumber != null && x.MobileNumber.ToLower().Contains(s)) ||
                    (x.AlternateMobileNumber != null && x.AlternateMobileNumber.ToLower().Contains(s)));
            }

            if (batch.HasValue)
            {
                query = query.Where(x => x.Batch == batch.Value);
            }

            if (courseId.HasValue && courseId.Value > 0)
            {
                query = query.Where(x => x.CourseId == courseId.Value);
            }

            if (groupId.HasValue && groupId.Value > 0)
            {
                query = query.Where(x => x.GroupId == groupId.Value);
            }

            if (semesterId.HasValue && semesterId.Value > 0)
            {
                query = query.Where(x => x.SemesterId == semesterId.Value);
            }

            var totalCount = await query.CountAsync();

            var students = await query
                .OrderBy(x => x.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.AppraId,
                    x.StudentNumber,
                    x.Name,
                    x.CourseId,
                    CourseName = x.Course != null ? x.Course.Name : "",
                    x.GroupId,
                    GroupName = x.Group != null ? x.Group.Name : "",
                    x.SemesterId,
                    SemesterName = x.Semester != null ? x.Semester.Name : "",
                    x.GenderId,
                    GenderName = x.Gender != null ? x.Gender.Name : "",
                    x.MediumId,
                    MediumName = x.Medium != null ? x.Medium.Name : "",
                    x.FirstLanguageId,
                    FirstLanguageName = x.FirstLanguage != null ? x.FirstLanguage.Name : "",
                    x.LanguageId,
                    LanguageName = x.Language != null ? x.Language.Name : "",
                    x.Batch,
                    x.DateOfBirth,
                    x.MobileNumber,
                    x.AlternateMobileNumber,
                    x.Email,
                    x.ParentMobileNumber,
                    x.ParentAlternateMobileNumber,
                    x.FatherName,
                    x.MotherName
                })
                .ToListAsync();

            return Ok(new
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = students
            });
        }
        [HttpPost]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        public async Task<IActionResult> Create(CreateStudentRequest request)
        {
            var exists = await _context.Students
                .AnyAsync(x => x.StudentNumber == request.StudentNumber);

            if (exists)
                return BadRequest("Student already exists");

            if (!await _context.Languages.AnyAsync(l =>
                    l.Id == request.LanguageId &&
                    l.TenantId == _currentUser.TenantId))
                return BadRequest("Invalid second language.");

            int firstLanguageId;
            if (request.FirstLanguageId is > 0)
            {
                if (!await _context.Languages.AnyAsync(l =>
                        l.Id == request.FirstLanguageId.Value &&
                        l.TenantId == _currentUser.TenantId))
                    return BadRequest("Invalid first language.");
                firstLanguageId = request.FirstLanguageId.Value;
            }
            else
            {
                firstLanguageId = await GetOrCreateEnglishLanguageIdAsync();
            }

            var student = new Student
            {
                AppraId = request.AppraId,
                StudentNumber = request.StudentNumber,
                Name = request.Name,
                CourseId = request.CourseId,
                GroupId = request.GroupId,
                SemesterId = request.SemesterId,
                MediumId = request.MediumId,
                FirstLanguageId = firstLanguageId,
                LanguageId = request.LanguageId,
                Batch =  request.Batch,
                DateOfBirth = request.DateOfBirth,
                GenderId = request.GenderId,
                MobileNumber = request.MobileNumber,
                AlternateMobileNumber = request.AlternateMobileNumber,
                Email = request.Email,
                ParentMobileNumber = request.ParentMobileNumber,
                ParentAlternateMobileNumber = request.ParentAlternateMobileNumber,
                FatherName = request.FatherName,
                MotherName = request.MotherName,
                CreatedDate = DateTime.UtcNow
            };

            await _context.AddAsync(student);
            await _context.SaveChangesAsync();

            return Ok(student);
        }
        [HttpPut]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        public async Task<IActionResult> Update(UpdateStudentRequest request)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(x => x.Id == request.Id);

            if (student == null)
                return NotFound();

            var normalizedNumber = request.StudentNumber.Trim();
            var duplicate = await _context.Students.AnyAsync(x =>
                x.Id != request.Id &&
                x.TenantId == _currentUser.TenantId &&
                x.StudentNumber == normalizedNumber);

            if (duplicate)
                return BadRequest("Another student already uses this Student Number.");

            if (!await _context.Languages.AnyAsync(l =>
                    l.Id == request.FirstLanguageId &&
                    l.TenantId == _currentUser.TenantId))
                return BadRequest("Invalid first language.");

            if (!await _context.Languages.AnyAsync(l =>
                    l.Id == request.LanguageId &&
                    l.TenantId == _currentUser.TenantId))
                return BadRequest("Invalid second language.");

            student.AppraId = request.AppraId;
                student.StudentNumber = normalizedNumber;
                student.Name = request.Name;
                student.CourseId = request.CourseId;
                student.GroupId = request.GroupId;
                student.SemesterId = request.SemesterId;
                student.MediumId = request.MediumId;
                student.FirstLanguageId = request.FirstLanguageId;
                student.LanguageId = request.LanguageId;
                student.Batch = request.Batch;
                student.DateOfBirth = request.DateOfBirth;
                student.GenderId = request.GenderId;
                student.MobileNumber = request.MobileNumber;
                student.AlternateMobileNumber = request.AlternateMobileNumber;
                student.Email = request.Email;
                student.ParentMobileNumber = request.ParentMobileNumber;
                student.ParentAlternateMobileNumber = request.ParentAlternateMobileNumber;
                student.FatherName = request.FatherName;
                student.MotherName = request.MotherName;
            student.UpdatedDate = DateTime.UtcNow;
            student.UpdatedBy = _currentUser.UserId;

            await _context.SaveChangesAsync();

            return Ok(student);
        }

        [HttpGet("export")]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        public async Task<IActionResult> ExportStudents(
            string? search = null,
            int? batch = null,
            int? courseId = null,
            int? groupId = null,
            int? semesterId = null)
        {
            var query = _context.Students
                .AsNoTracking()
                .Where(x => x.TenantId == _currentUser.TenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(x =>
                    x.StudentNumber.ToLower().Contains(s) ||
                    x.Name.ToLower().Contains(s) ||
                    (x.MobileNumber != null && x.MobileNumber.ToLower().Contains(s)) ||
                    (x.AlternateMobileNumber != null && x.AlternateMobileNumber.ToLower().Contains(s)));
            }

            if (batch.HasValue)
            {
                query = query.Where(x => x.Batch == batch.Value);
            }

            if (courseId.HasValue && courseId.Value > 0)
            {
                query = query.Where(x => x.CourseId == courseId.Value);
            }

            if (groupId.HasValue && groupId.Value > 0)
            {
                query = query.Where(x => x.GroupId == groupId.Value);
            }

            if (semesterId.HasValue && semesterId.Value > 0)
            {
                query = query.Where(x => x.SemesterId == semesterId.Value);
            }

            var rows = await query
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.StudentNumber,
                    x.Name,
                    CourseName = x.Course != null ? x.Course.Name : "",
                    GroupName = x.Group != null ? x.Group.Name : "",
                    SemesterName = x.Semester != null ? x.Semester.Name : "",
                    x.Batch,
                    x.MobileNumber,
                    x.AlternateMobileNumber,
                    x.Email
                })
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("StudentNumber,Name,Course,Group,Semester,Batch,Mobile,AlternateMobile,Email");
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    Csv(r.StudentNumber),
                    Csv(r.Name),
                    Csv(r.CourseName),
                    Csv(r.GroupName),
                    Csv(r.SemesterName),
                    Csv(r.Batch?.ToString() ?? ""),
                    Csv(r.MobileNumber ?? ""),
                    Csv(r.AlternateMobileNumber ?? ""),
                    Csv(r.Email ?? "")));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"students-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        private static string Csv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        private async Task<int> GetOrCreateEnglishLanguageIdAsync()
        {
            var tid = _currentUser.TenantId;
            var lang = await _context.Languages
                .FirstOrDefaultAsync(l => l.TenantId == tid && l.Name == "English");
            if (lang != null)
                return lang.Id;

            lang = new Language
            {
                Name = "English",
                TenantId = tid,
                CreatedDate = DateTime.UtcNow
            };
            await _context.AddAsync(lang);
            await _context.SaveChangesAsync();
            return lang.Id;
        }
        [HttpPost("upload")]
        [Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
        public async Task<IActionResult> UploadStudents(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file");

            var ext = Path.GetExtension(file.FileName);
            if (!string.Equals(ext, ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Upload an Excel workbook (.xlsx).");

            await using var stream = file.OpenReadStream();

            var result = await _studentService.UploadStudentsAsync(stream, _currentUser.TenantId, cancellationToken);

            return Ok(result);
        }
    }
}



