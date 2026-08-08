namespace Abhyanvaya.Domain.Academic;

/// <summary>
/// AI29.1B — Default section type codes. Configuration may extend; business logic must not switch on enums.
/// </summary>
public static class SectionTypeCodes
{
    public const string Regular = "Regular";
    public const string Honours = "Honours";
    public const string Bridge = "Bridge";
    public const string Tutorial = "Tutorial";
    public const string Practical = "Practical";
    public const string Laboratory = "Laboratory";
    public const string Remedial = "Remedial";
    public const string Weekend = "Weekend";
    public const string Evening = "Evening";
    public const string SpecialBatch = "SpecialBatch";

    public static IReadOnlyList<string> Defaults { get; } =
    [
        Regular, Honours, Bridge, Tutorial, Practical, Laboratory, Remedial, Weekend, Evening, SpecialBatch
    ];
}
