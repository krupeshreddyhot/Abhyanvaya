
namespace Abhyanvaya.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        int UserId { get; }
        string Role { get; }
        int TenantId { get; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }
}
