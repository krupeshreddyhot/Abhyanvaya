using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities
{
    public class User : BaseEntity
    {
        public required string Username { get; set; }

        // Store hashed password (never plain text)
        public required string PasswordHash { get; set; }

        public UserRole Role { get; set; }

        // Faculty-specific restrictions
        public int CourseId { get; set; }
        public int GroupId { get; set; }

        // Navigation properties
        public Course? Course { get; set; }
        public Group? Group { get; set; }
    }
}
