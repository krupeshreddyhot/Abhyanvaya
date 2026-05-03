
namespace Abhyanvaya.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        int UserId { get; }
        string Role { get; }
        int TenantId { get; }
        /// <summary>Optional directory link; from JWT when present.</summary>
        int StaffId { get; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }
}
