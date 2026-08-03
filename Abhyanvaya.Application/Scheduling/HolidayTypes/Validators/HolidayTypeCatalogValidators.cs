using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.HolidayTypes.Validators;

public sealed class CreateHolidayTypeCatalogRequestValidator : AbstractValidator<CreateHolidayTypeCatalogRequest>
{
    public CreateHolidayTypeCatalogRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Colour).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateHolidayTypeCatalogRequestValidator : AbstractValidator<UpdateHolidayTypeCatalogRequest>
{
    public UpdateHolidayTypeCatalogRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Colour).NotEmpty().MaximumLength(20);
    }
}
