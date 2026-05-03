using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities
{
    public class User : BaseEntity
    {
        public required string Username { get; set; }

        // Store hashed password (never plain text)
        public required string PasswordHash { get; set; }

        /// <summary>When true, client must prompt for password change before full app use.</summary>
        public bool MustChangePassword { get; set; }

        /// <summary>SHA-256 hex of self-service reset token (opaque bearer).</summary>
        public string? PasswordResetTokenHash { get; set; }

        public DateTime? PasswordResetTokenExpires { get; set; }

        public UserRole Role { get; set; }

        // Faculty-specific restrictions
        public int CourseId { get; set; }
        public int GroupId { get; set; }

        /// <summary>Optional link to directory row when this login represents staff/faculty.</summary>
        public int? StaffId { get; set; }
        public Staff? Staff { get; set; }

        public ICollection<UserApplicationRole> UserApplicationRoles { get; set; } = new List<UserApplicationRole>();

        // Navigation properties
        public Course? Course { get; set; }
        public Group? Group { get; set; }
    }
}
