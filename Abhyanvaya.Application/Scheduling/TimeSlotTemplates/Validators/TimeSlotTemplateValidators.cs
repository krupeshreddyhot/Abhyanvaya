using Abhyanvaya.Application.DTOs.Scheduling;

using FluentValidation;



namespace Abhyanvaya.Application.Scheduling.Validators;



public sealed class CreateTimeSlotTemplateRequestValidator : AbstractValidator<CreateTimeSlotTemplateRequest>

{

    public CreateTimeSlotTemplateRequestValidator()

    {

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(1000);

        RuleFor(x => x.TemplateType).IsInEnum();

    }

}



public sealed class UpdateTimeSlotTemplateRequestValidator : AbstractValidator<UpdateTimeSlotTemplateRequest>

{

    public UpdateTimeSlotTemplateRequestValidator()

    {

        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(1000);

        RuleFor(x => x.TemplateType).IsInEnum();

    }

}

