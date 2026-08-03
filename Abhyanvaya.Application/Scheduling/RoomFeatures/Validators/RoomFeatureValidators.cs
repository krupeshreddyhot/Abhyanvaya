using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.RoomFeatures.Validators;

public sealed class CreateRoomFeatureRequestValidator : AbstractValidator<CreateRoomFeatureRequest>
{
    private static readonly string[] AllowedCategories = ["Equipment", "Lab", "Accessibility", "AV", "Other"];

    public CreateRoomFeatureRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50)
            .Must(c => AllowedCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Category must be one of: Equipment, Lab, Accessibility, AV, Other.");
    }
}

public sealed class UpdateRoomFeatureRequestValidator : AbstractValidator<UpdateRoomFeatureRequest>
{
    private static readonly string[] AllowedCategories = ["Equipment", "Lab", "Accessibility", "AV", "Other"];

    public UpdateRoomFeatureRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50)
            .Must(c => AllowedCategories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Category must be one of: Equipment, Lab, Accessibility, AV, Other.");
    }
}

public sealed class AssignRoomFeatureRequestValidator : AbstractValidator<AssignRoomFeatureRequest>
{
    public AssignRoomFeatureRequestValidator()
    {
        RuleFor(x => x.RoomFeatureId).GreaterThan(0);
    }
}

public sealed class CloneRoomFeatureAssignmentsRequestValidator : AbstractValidator<CloneRoomFeatureAssignmentsRequest>
{
    public CloneRoomFeatureAssignmentsRequestValidator()
    {
        RuleFor(x => x.FromRoomId).GreaterThan(0);
        RuleFor(x => x.ToRoomId).GreaterThan(0);
        RuleFor(x => x.ToRoomId).NotEqual(x => x.FromRoomId)
            .WithMessage("Target room must differ from source room.");
    }
}
