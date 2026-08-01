using Abhyanvaya.Application.DTOs.Scheduling;

using FluentValidation;



namespace Abhyanvaya.Application.Scheduling.Validators;



public sealed class CreateFacultyAvailabilityRequestValidator : AbstractValidator<CreateFacultyAvailabilityRequest>

{

    public CreateFacultyAvailabilityRequestValidator()

    {

        RuleFor(x => x.StaffId).GreaterThan(0);

        RuleFor(x => x.AcademicYearId).GreaterThan(0);

        RuleFor(x => x.AvailabilityType).IsInEnum();

        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);

        RuleFor(x => x.Reason).MaximumLength(500);

        RuleFor(x => x.Remarks).MaximumLength(1000);

    }

}



public sealed class UpdateFacultyAvailabilityRequestValidator : AbstractValidator<UpdateFacultyAvailabilityRequest>

{

    public UpdateFacultyAvailabilityRequestValidator()

    {

        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.StaffId).GreaterThan(0);

        RuleFor(x => x.AcademicYearId).GreaterThan(0);

        RuleFor(x => x.AvailabilityType).IsInEnum();

        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);

        RuleFor(x => x.Reason).MaximumLength(500);

        RuleFor(x => x.Remarks).MaximumLength(1000);

    }

}

