using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.Governance.Validators;

public sealed class CreateScheduleVersionRequestValidator : AbstractValidator<CreateScheduleVersionRequest>
{
    public CreateScheduleVersionRequestValidator()
    {
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.VersionName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Remarks).MaximumLength(2000);
        RuleFor(x => x.TimetableName).NotEmpty().When(x => x.CreateEmptyTimetable).MaximumLength(200);
    }
}

public sealed class DuplicateScheduleVersionRequestValidator : AbstractValidator<DuplicateScheduleVersionRequest>
{
    public DuplicateScheduleVersionRequestValidator()
    {
        RuleFor(x => x.SourceVersionId).GreaterThan(0);
        RuleFor(x => x.VersionName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Remarks).MaximumLength(2000);
    }
}

public sealed class SubmitForReviewRequestValidator : AbstractValidator<SubmitForReviewRequest>
{
    public SubmitForReviewRequestValidator()
    {
        RuleFor(x => x.TimetableId).GreaterThan(0);
        RuleFor(x => x.Comments).MaximumLength(2000);
    }
}

public sealed class DecideApprovalStepRequestValidator : AbstractValidator<DecideApprovalStepRequest>
{
    public DecideApprovalStepRequestValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.StepOrder).GreaterThan(0);
        RuleFor(x => x.Comments).MaximumLength(2000);
        RuleFor(x => x.DecisionNotes).MaximumLength(2000);
        RuleFor(x => x.ReviewerRemarks).MaximumLength(2000);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Comments) || !string.IsNullOrWhiteSpace(x.DecisionNotes))
            .When(x => x.Decision is Domain.Enums.Scheduling.ApprovalDecision.Rejected
                or Domain.Enums.Scheduling.ApprovalDecision.Returned)
            .WithMessage("Comment is required when rejecting or returning for changes.");
    }
}

public sealed class CompareScheduleVersionsRequestValidator : AbstractValidator<CompareScheduleVersionsRequest>
{
    public CompareScheduleVersionsRequestValidator()
    {
        RuleFor(x => x.LeftVersionId).GreaterThan(0);
        RuleFor(x => x.RightVersionId).GreaterThan(0);
        RuleFor(x => x.Search).MaximumLength(200);
    }
}

public sealed class FreezeTimetableRequestValidator : AbstractValidator<FreezeTimetableRequest>
{
    public FreezeTimetableRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class UnlockFrozenTimetableRequestValidator : AbstractValidator<UnlockFrozenTimetableRequest>
{
    public UnlockFrozenTimetableRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public sealed class AddApprovalCommentRequestValidator : AbstractValidator<AddApprovalCommentRequest>
{
    public AddApprovalCommentRequestValidator()
    {
        RuleFor(x => x.RequestId).GreaterThan(0);
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ArchiveScheduleVersionRequestValidator : AbstractValidator<ArchiveScheduleVersionRequest>
{
    public ArchiveScheduleVersionRequestValidator()
    {
        RuleFor(x => x.ArchiveReasonId).GreaterThan(0);
        RuleFor(x => x.Comments).MaximumLength(2000);
    }
}

public sealed class EnqueueTimetableCloneRequestValidator : AbstractValidator<EnqueueTimetableCloneRequest>
{
    public EnqueueTimetableCloneRequestValidator()
    {
        RuleFor(x => x.SourceTimetableId).GreaterThan(0);
        RuleFor(x => x.TargetTimetableName).MaximumLength(200);
    }
}

public sealed class DismissSoftWarningRequestValidator : AbstractValidator<DismissSoftWarningRequest>
{
    public DismissSoftWarningRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
    }
}
