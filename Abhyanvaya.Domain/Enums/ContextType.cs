namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Operational context scope for SaaS modules. Only <see cref="Global"/> and <see cref="College"/>
/// are implemented in AI22.5; additional values reserve future hierarchy expansion.
/// </summary>
public enum ContextType
{
    Global = 0,
    University = 1,
    College = 2,
    Campus = 3,
    Department = 4,
    Course = 5,
    Section = 6,
}
