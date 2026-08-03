namespace Abhyanvaya.Application.DTOs.Department
{
    public class DepartmentDto
    {
        public int Id { get; set; }
        public int CollegeId { get; set; }
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CreateDepartmentRequest
    {
        public int CollegeId { get; set; }
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateDepartmentRequest
    {
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
