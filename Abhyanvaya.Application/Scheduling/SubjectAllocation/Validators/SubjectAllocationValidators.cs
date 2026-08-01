using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.SubjectAllocations.Validators;

public sealed class CreateSubjectAllocationRequestValidator : AbstractValidator<CreateSubjectAllocationRequest>
{
    public CreateSubjectAllocationRequestValidator()
    {
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.StaffId).GreaterThan(0);
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.SemesterId).GreaterThan(0);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.WeeklyHours).GreaterThan(0);
        RuleFor(x => x.EffectiveTo)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveTo.HasValue)
            .WithMessage("EffectiveTo must be after EffectiveFrom.");
    }
}

public sealed class UpdateSubjectAllocationRequestValidator : AbstractValidator<UpdateSubjectAllocationRequest>
{
    public UpdateSubjectAllocationRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.StaffId).GreaterThan(0);
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.GroupId).GreaterThan(0);
        RuleFor(x => x.SemesterId).GreaterThan(0);
        RuleFor(x => x.DepartmentId).GreaterThan(0);
        RuleFor(x => x.WeeklyHours).GreaterThan(0);
        RuleFor(x => x.EffectiveTo)
            .GreaterThan(x => x.EffectiveFrom)
            .When(x => x.EffectiveTo.HasValue)
            .WithMessage("EffectiveTo must be after EffectiveFrom.");
    }
}
