namespace Abhyanvaya.Application.DTOs.Academic;

public sealed class SectionMergeValidateRequest
{
    public IReadOnlyList<int> SourceSectionIds { get; init; } = [];
    public int? TargetSectionId { get; init; }
    public DateOnly? EffectiveDate { get; init; }
}

public sealed class SectionMergePreviewDto
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int CombinedStudentCount { get; init; }
    public int CombinedFacultyCount { get; init; }
    public int TargetMaximumCapacity { get; init; }
    public IReadOnlyList<int> SourceSectionIds { get; init; } = [];
    public int? TargetSectionId { get; init; }
}

public sealed class SectionMergeCommitRequest
{
    public IReadOnlyList<int> SourceSectionIds { get; init; } = [];
    public int TargetSectionId { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public string? Notes { get; init; }
}

public sealed class SectionMergeTransactionDto
{
    public int Id { get; init; }
    public Guid TransactionId { get; init; }
    public int TargetSectionId { get; init; }
    public IReadOnlyList<int> SourceSectionIds { get; init; } = [];
    public DateOnly EffectiveDate { get; init; }
    public string Status { get; init; } = "";
    public string? Notes { get; init; }
    public bool IsReversed { get; init; }
}

public sealed class SectionSplitValidateRequest
{
    public int SourceSectionId { get; init; }
    public int ChildCount { get; init; } = 2;
    public string StrategyCode { get; init; } = "Manual";
    public DateOnly? EffectiveDate { get; init; }
}

public sealed class SectionSplitPreviewDto
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int SourceSectionId { get; init; }
    public int SourceStudentCount { get; init; }
    public string StrategyCode { get; init; } = "Manual";
    public IReadOnlyList<SectionSplitChildPlanDto> ProposedChildren { get; init; } = [];
}

public sealed class SectionSplitChildPlanDto
{
    public string ProposedCode { get; init; } = "";
    public string ProposedName { get; init; } = "";
    public int ProposedCapacity { get; init; }
    public int PlannedStudentCount { get; init; }
}

public sealed class SectionSplitCommitRequest
{
    public int SourceSectionId { get; init; }
    public string StrategyCode { get; init; } = "Manual";
    public DateOnly EffectiveDate { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<SectionSplitChildPlanDto> Children { get; init; } = [];
}

public sealed class SectionSplitTransactionDto
{
    public int Id { get; init; }
    public Guid TransactionId { get; init; }
    public int SourceSectionId { get; init; }
    public IReadOnlyList<int> ChildSectionIds { get; init; } = [];
    public string StrategyCode { get; init; } = "";
    public DateOnly EffectiveDate { get; init; }
    public string Status { get; init; } = "";
    public string? Notes { get; init; }
    public bool IsReversed { get; init; }
}

public sealed class SectionLineageDto
{
    public int ParentSectionId { get; init; }
    public int ChildSectionId { get; init; }
    public string RelationKind { get; init; } = "";
    public Guid? TransactionId { get; init; }
    public DateOnly EffectiveDate { get; init; }
}
