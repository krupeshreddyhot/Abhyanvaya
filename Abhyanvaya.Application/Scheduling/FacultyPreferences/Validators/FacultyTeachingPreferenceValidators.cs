using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.FacultyPreferences.Validators;

public sealed class CreateFacultyTeachingPreferenceRequestValidator : AbstractValidator<CreateFacultyTeachingPreferenceRequest>
{
    public CreateFacultyTeachingPreferenceRequestValidator()
    {
        RuleFor(x => x.StaffId).GreaterThan(0);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.MaximumContinuousClasses).GreaterThan(0);
        RuleFor(x => x.MinimumBreakBetweenClasses).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PreferredTeachingMode).IsInEnum();
        RuleFor(x => x)
            .Must(x => !x.PreferredFirstPeriod.HasValue || !x.PreferredLastPeriod.HasValue || x.PreferredFirstPeriod <= x.PreferredLastPeriod)
            .WithMessage("Preferred first period must be less than or equal to preferred last period.");
    }
}

public sealed class UpdateFacultyTeachingPreferenceRequestValidator : AbstractValidator<UpdateFacultyTeachingPreferenceRequest>
{
    public UpdateFacultyTeachingPreferenceRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.StaffId).GreaterThan(0);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.MaximumContinuousClasses).GreaterThan(0);
        RuleFor(x => x.MinimumBreakBetweenClasses).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PreferredTeachingMode).IsInEnum();
        RuleFor(x => x)
            .Must(x => !x.PreferredFirstPeriod.HasValue || !x.PreferredLastPeriod.HasValue || x.PreferredFirstPeriod <= x.PreferredLastPeriod)
            .WithMessage("Preferred first period must be less than or equal to preferred last period.");
    }
}
