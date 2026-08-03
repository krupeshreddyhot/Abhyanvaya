using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.AcademicCalendar.Validators;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

public sealed class AcademicCalendarValidatorTests
{
    private readonly CreateAcademicYearRequestValidator _createYearValidator = new();
    private readonly CreateHolidayRequestValidator _createHolidayValidator = new();

    [Fact]
    public void CreateAcademicYear_Fails_WhenEndDateNotAfterStartDate()
    {
        var request = new CreateAcademicYearRequest
        {
            Name = "2026-27",
            Code = "AY2627",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 1),
        };

        var result = _createYearValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAcademicYearRequest.EndDate));
    }

    [Fact]
    public void CreateAcademicYear_Succeeds_WhenDatesValid()
    {
        var request = new CreateAcademicYearRequest
        {
            Name = "2026-27",
            Code = "AY2627",
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2027, 5, 31),
        };

        var result = _createYearValidator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateHoliday_Fails_WhenNameMissing()
    {
        var request = new CreateHolidayRequest
        {
            AcademicYearId = 1,
            Name = "",
            Date = new DateOnly(2026, 8, 15),
            HolidayType = HolidayType.National,
        };

        var result = _createHolidayValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateHolidayRequest.Name));
    }
}

public sealed class TimeSlotOverlapHelperTests
{
    [Fact]
    public void HasOverlap_ReturnsTrue_WhenSameDayIntervalsOverlap()
    {
        var existing = new List<(int Id, TimeSlotInterval Interval)>
        {
            (1, new TimeSlotInterval(1, 1, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0))),
        };
        var candidate = new TimeSlotInterval(1, 2, new TimeSpan(9, 30, 0), new TimeSpan(10, 30, 0));

        Assert.True(TimeSlotOverlapHelper.HasOverlap(existing, candidate));
    }

    [Fact]
    public void HasOverlap_ReturnsFalse_WhenDifferentDays()
    {
        var existing = new List<(int Id, TimeSlotInterval Interval)>
        {
            (1, new TimeSlotInterval(1, 1, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0))),
        };
        var candidate = new TimeSlotInterval(2, 1, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));

        Assert.False(TimeSlotOverlapHelper.HasOverlap(existing, candidate));
    }

    [Fact]
    public void HasOverlap_ReturnsTrue_WhenNullDayAppliesToAllDays()
    {
        var existing = new List<(int Id, TimeSlotInterval Interval)>
        {
            (1, new TimeSlotInterval(null, 1, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0))),
        };
        var candidate = new TimeSlotInterval(3, 2, new TimeSpan(9, 15, 0), new TimeSpan(10, 15, 0));

        Assert.True(TimeSlotOverlapHelper.HasOverlap(existing, candidate));
    }

    [Fact]
    public void HasDuplicatePeriodNumber_ReturnsTrue_ForSameSetAndDay()
    {
        var existing = new List<(int Id, TimeSlotInterval Interval)>
        {
            (1, new TimeSlotInterval(1, 2, new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0))),
        };
        var candidate = new TimeSlotInterval(1, 2, new TimeSpan(11, 0, 0), new TimeSpan(12, 0, 0));

        Assert.True(TimeSlotOverlapHelper.HasDuplicatePeriodNumber(existing, candidate));
    }

    [Fact]
    public void HasOverlap_ExcludesCandidateId_WhenUpdating()
    {
        var existing = new List<(int Id, TimeSlotInterval Interval)>
        {
            (5, new TimeSlotInterval(1, 1, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0))),
        };
        var candidate = new TimeSlotInterval(1, 1, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), ExcludeId: 5);

        Assert.False(TimeSlotOverlapHelper.HasOverlap(existing, candidate));
    }
}

public sealed class AcademicYearCloneHelperTests
{
    [Fact]
    public void ShiftDate_MovesDatesByYearDelta()
    {
        var sourceStart = new DateOnly(2025, 6, 1);
        var targetStart = new DateOnly(2026, 6, 1);
        var holiday = new DateOnly(2025, 8, 15);

        var shifted = AcademicYearCloneHelper.ShiftDate(holiday, sourceStart, targetStart);

        Assert.Equal(new DateOnly(2026, 8, 15), shifted);
    }

    [Fact]
    public void ShiftDate_PreservesRelativeOffsetWithinYear()
    {
        var sourceStart = new DateOnly(2025, 6, 1);
        var targetStart = new DateOnly(2026, 6, 1);
        var termEnd = new DateOnly(2025, 12, 15);

        var shifted = AcademicYearCloneHelper.ShiftDate(termEnd, sourceStart, targetStart);

        Assert.Equal(new DateOnly(2026, 12, 15), shifted);
    }
}
