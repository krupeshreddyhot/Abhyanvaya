using Abhyanvaya.Application.DTOs.Scheduling;

using FluentValidation;



namespace Abhyanvaya.Application.Scheduling.Validators;



public sealed class CreateRoomAvailabilityRequestValidator : AbstractValidator<CreateRoomAvailabilityRequest>

{

    public CreateRoomAvailabilityRequestValidator()

    {

        RuleFor(x => x.RoomId).GreaterThan(0);

        RuleFor(x => x.AcademicYearId).GreaterThan(0);

        RuleFor(x => x.AvailabilityType).IsInEnum();

        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);

        RuleFor(x => x.Reason).MaximumLength(500);

    }

}



public sealed class UpdateRoomAvailabilityRequestValidator : AbstractValidator<UpdateRoomAvailabilityRequest>

{

    public UpdateRoomAvailabilityRequestValidator()

    {

        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.RoomId).GreaterThan(0);

        RuleFor(x => x.AcademicYearId).GreaterThan(0);

        RuleFor(x => x.AvailabilityType).IsInEnum();

        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);

        RuleFor(x => x.Reason).MaximumLength(500);

    }

}

