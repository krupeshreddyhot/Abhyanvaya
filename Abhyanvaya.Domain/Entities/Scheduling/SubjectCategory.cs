using Abhyanvaya.Domain.Common;



namespace Abhyanvaya.Domain.Entities.Scheduling;



public class SubjectCategory : BaseEntity

{

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

}

