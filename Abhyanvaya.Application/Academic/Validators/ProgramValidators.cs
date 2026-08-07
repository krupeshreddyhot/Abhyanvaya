using Abhyanvaya.Application.DTOs.Academic;
using FluentValidation;

namespace Abhyanvaya.Application.Academic.Validators;

public sealed class CreateProgramRequestValidator : AbstractValidator<CreateProgramRequest>
{
    public CreateProgramRequestValidator()
    {
        RuleFor(x => x.ProgramCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ProgramName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(512);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Icon).MaximumLength(64);
        RuleFor(x => x.ThemeColor).MaximumLength(32);
        RuleFor(x => x.AcademicCalendarId).Must(id => id is null or > 0)
            .WithMessage("AcademicCalendarId must be null or a positive id.");
    }
}

public sealed class UpdateProgramRequestValidator : AbstractValidator<UpdateProgramRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "Inactive", "Archived"
    };

    public UpdateProgramRequestValidator()
    {
        RuleFor(x => x.ProgramCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ProgramName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(512);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).NotEmpty().Must(s => AllowedStatuses.Contains(s))
            .WithMessage("Status must be Active, Inactive, or Archived.");
        RuleFor(x => x.Icon).MaximumLength(64);
        RuleFor(x => x.ThemeColor).MaximumLength(32);
        RuleFor(x => x.AcademicCalendarId).Must(id => id is null or > 0)
            .WithMessage("AcademicCalendarId must be null or a positive id.");
    }
}

public sealed class AssignCourseProgramRequestValidator : AbstractValidator<AssignCourseProgramRequest>
{
    public AssignCourseProgramRequestValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.ProgramId).Must(id => id is null or > 0)
            .WithMessage("ProgramId must be null or a positive id. A Course cannot belong to two Programs.");
    }
}

public sealed class UpsertProgramPolicyRequestValidator : AbstractValidator<UpsertProgramPolicyRequest>
{
    public UpsertProgramPolicyRequestValidator()
    {
        RuleFor(x => x.MinimumAttendancePercent)
            .InclusiveBetween(0, 100)
            .When(x => x.MinimumAttendancePercent.HasValue);
        RuleFor(x => x.PassMarks)
            .InclusiveBetween(0, 100)
            .When(x => x.PassMarks.HasValue);
        RuleFor(x => x.CreditsRequired).GreaterThanOrEqualTo(0).When(x => x.CreditsRequired.HasValue);
        RuleFor(x => x.MaximumBacklogs).GreaterThanOrEqualTo(0).When(x => x.MaximumBacklogs.HasValue);
        RuleFor(x => x.MaximumSubjects).GreaterThanOrEqualTo(0).When(x => x.MaximumSubjects.HasValue);
        RuleFor(x => x.AcademicRules).MaximumLength(4000);
    }
}
