namespace Abhyanvaya.Application.DTOs.Staff
{
    public class StaffLookupAdminDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsExclusivePerDepartment { get; set; }
        public bool IsExclusivePerCollege { get; set; }
    }

    public class StaffLookupWriteRequest
    {
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsExclusivePerDepartment { get; set; }
        public bool IsExclusivePerCollege { get; set; }
    }
}
