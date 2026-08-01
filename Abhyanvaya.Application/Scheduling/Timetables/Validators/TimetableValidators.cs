using Abhyanvaya.Application.DTOs.Scheduling;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling.Timetables.Validators;

public sealed class CreateTimetableRequestValidator : AbstractValidator<CreateTimetableRequest>
{
    public CreateTimetableRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(50).When(x => x.Code is not null);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId.HasValue);
        RuleFor(x => x.TimeSlotSetId).GreaterThan(0).When(x => x.TimeSlotSetId.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
    }
}

public sealed class UpdateTimetableRequestValidator : AbstractValidator<UpdateTimetableRequest>
{
    public UpdateTimetableRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(50).When(x => x.Code is not null);
        RuleFor(x => x.AcademicYearId).GreaterThan(0);
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId.HasValue);
        RuleFor(x => x.TimeSlotSetId).GreaterThan(0).When(x => x.TimeSlotSetId.HasValue);
        RuleFor(x => x.Notes).MaximumLength(2000).When(x => x.Notes is not null);
    }
}

public sealed class CreateTimetableEntryRequestValidator : AbstractValidator<CreateTimetableEntryRequest>
{
    public CreateTimetableEntryRequestValidator()
    {
        RuleFor(x => x.DayOfWeek).InclusiveBetween((byte)0, (byte)6);
        RuleFor(x => x.TimeSlotId).GreaterThan(0);
        RuleFor(x => x.SubjectAllocationId).GreaterThan(0);
        RuleFor(x => x.RoomId).GreaterThan(0).When(x => x.RoomId.HasValue);
        RuleFor(x => x.Remarks).MaximumLength(500).When(x => x.Remarks is not null);
    }
}

public sealed class UpdateTimetableEntryRequestValidator : AbstractValidator<UpdateTimetableEntryRequest>
{
    public UpdateTimetableEntryRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DayOfWeek).InclusiveBetween((byte)0, (byte)6);
        RuleFor(x => x.TimeSlotId).GreaterThan(0);
        RuleFor(x => x.SubjectAllocationId).GreaterThan(0);
        RuleFor(x => x.RoomId).GreaterThan(0).When(x => x.RoomId.HasValue);
        RuleFor(x => x.Remarks).MaximumLength(500).When(x => x.Remarks is not null);
    }
}

public sealed class BulkPasteEntriesRequestValidator : AbstractValidator<BulkPasteEntriesRequest>
{
    public BulkPasteEntriesRequestValidator()
    {
        RuleForEach(x => x.Entries).SetValidator(new UpsertTimetableEntryRequestValidator());
    }
}

public sealed class UpsertTimetableEntryRequestValidator : AbstractValidator<UpsertTimetableEntryRequest>
{
    public UpsertTimetableEntryRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.DayOfWeek).InclusiveBetween((byte)0, (byte)6);
        RuleFor(x => x.TimeSlotId).GreaterThan(0);
        RuleFor(x => x.SubjectAllocationId).GreaterThan(0);
        RuleFor(x => x.RoomId).GreaterThan(0).When(x => x.RoomId.HasValue);
        RuleFor(x => x.Remarks).MaximumLength(500).When(x => x.Remarks is not null);
    }
}

public sealed class MoveTimetableEntryRequestValidator : AbstractValidator<MoveTimetableEntryRequest>
{
    public MoveTimetableEntryRequestValidator()
    {
        RuleFor(x => x.DayOfWeek).InclusiveBetween((byte)0, (byte)6);
        RuleFor(x => x.TimeSlotId).GreaterThan(0);
        RuleFor(x => x.RoomId).GreaterThan(0).When(x => x.RoomId.HasValue);
    }
}

public sealed class CopyTimetableEntryRequestValidator : AbstractValidator<CopyTimetableEntryRequest>
{
    public CopyTimetableEntryRequestValidator()
    {
        RuleFor(x => x.TargetDayOfWeek).InclusiveBetween((byte)0, (byte)6);
        RuleFor(x => x.TargetTimeSlotId).GreaterThan(0);
        RuleFor(x => x.RoomId).GreaterThan(0).When(x => x.RoomId.HasValue);
    }
}
