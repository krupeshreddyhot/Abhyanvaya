using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.SubjectDelivery.Validators;

public sealed class CreateSubjectDeliveryTypeRequestValidator : AbstractValidator<CreateSubjectDeliveryTypeRequest>
{
    public CreateSubjectDeliveryTypeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateSubjectDeliveryTypeRequestValidator : AbstractValidator<UpdateSubjectDeliveryTypeRequest>
{
    public UpdateSubjectDeliveryTypeRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateSubjectDeliveryFieldsRequestValidator : AbstractValidator<UpdateSubjectDeliveryFieldsRequest>
{
    public UpdateSubjectDeliveryFieldsRequestValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.DeliveryTypeId).GreaterThan(0);
        RuleFor(x => x.ExpectedCapacity).GreaterThan(0).When(x => x.ExpectedCapacity.HasValue);
    }
}
