using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.AcademicCalendar.Validators;

public sealed class CreateAcademicYearRequestValidator : AbstractValidator<CreateAcademicYearRequest>
{
    public CreateAcademicYearRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");
    }
}

public sealed class UpdateAcademicYearRequestValidator : AbstractValidator<UpdateAcademicYearRequest>
{
    public UpdateAcademicYearRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date.");
    }
}

public sealed class CreateHolidayRequestValidator : AbstractValidator<CreateHolidayRequest>
{
    public CreateHolidayRequestValidator()
    {
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HolidayType).IsInEnum();
        RuleFor(x => x.Colour).MaximumLength(20).When(x => x.Colour is not null);
    }
}

public sealed class UpdateHolidayRequestValidator : AbstractValidator<UpdateHolidayRequest>
{
    public UpdateHolidayRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HolidayType).IsInEnum();
        RuleFor(x => x.Colour).MaximumLength(20).When(x => x.Colour is not null);
    }
}
