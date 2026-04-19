namespace Abhyanvaya.Domain.Entities
{
    /// <summary>
    /// Global university catalog (not tenant-scoped). College codes are unique per university.
    /// </summary>
    public class University
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
