using Abhyanvaya.Application.DTOs.Scheduling;

using FluentValidation;



namespace Abhyanvaya.Application.Scheduling.Validators;



public sealed class CreateSubjectCategoryRequestValidator : AbstractValidator<CreateSubjectCategoryRequest>

{

    public CreateSubjectCategoryRequestValidator()

    {

        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

    }

}



public sealed class UpdateSubjectCategoryRequestValidator : AbstractValidator<UpdateSubjectCategoryRequest>

{

    public UpdateSubjectCategoryRequestValidator()

    {

        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

    }

}



public sealed class UpdateSubjectSchedulingCategoryRequestValidator : AbstractValidator<UpdateSubjectSchedulingCategoryRequest>

{

    public UpdateSubjectSchedulingCategoryRequestValidator()

    {

        RuleFor(x => x.SubjectId).GreaterThan(0);

        RuleFor(x => x.SubjectCategoryId).GreaterThan(0);

        RuleFor(x => x.DefaultDurationMinutes).GreaterThan(0).When(x => x.DefaultDurationMinutes.HasValue);

        RuleFor(x => x.RequiresRoomType).IsInEnum().When(x => x.RequiresRoomType.HasValue);

    }

}

