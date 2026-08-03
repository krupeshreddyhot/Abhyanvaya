namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

/// <summary>Configurable detection thresholds (AI30 Phase 2B.5). Rules unchanged; values are injectable.</summary>
public sealed class ConflictRuleThresholds
{
    public const string SectionName = "ConflictRules";

    public int MaximumContinuousClasses { get; set; } = 3;
    public int MaximumDailyClasses { get; set; } = 8;
    public int MinimumBreakMinutes { get; set; } = 0;
    public int FacultyTravelBufferMinutes { get; set; } = 45;
    public decimal RoomCapacityMarginPercent { get; set; }
    public decimal LabUtilizationPercent { get; set; } = 85;
    public bool LunchWindowEnabled { get; set; } = true;
    public int ContiguousGapMinutes { get; set; } = 15;

    public static ConflictRuleThresholds Defaults { get; } = new();

    public static class Keys
    {
        public const string MaximumContinuousClasses = "MaximumContinuousClasses";
        public const string MaximumDailyClasses = "MaximumDailyClasses";
        public const string MinimumBreakMinutes = "MinimumBreakMinutes";
        public const string FacultyTravelBufferMinutes = "FacultyTravelBufferMinutes";
        public const string RoomCapacityMarginPercent = "RoomCapacityMarginPercent";
        public const string LabUtilizationPercent = "LabUtilizationPercent";
        public const string LunchWindowEnabled = "LunchWindowEnabled";
        public const string ContiguousGapMinutes = "ContiguousGapMinutes";
    }
}
