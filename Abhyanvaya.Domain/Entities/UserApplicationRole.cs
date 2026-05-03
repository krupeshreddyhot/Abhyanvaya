namespace Abhyanvaya.Domain.Entities
{
    public class UserApplicationRole
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int ApplicationRoleId { get; set; }
        public ApplicationRole ApplicationRole { get; set; } = null!;
    }
}
