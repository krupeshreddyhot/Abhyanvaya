using Abhyanvaya.Application.Scheduling;

using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1A;



public sealed class AvailabilityOverlapHelperTests

{

    [Fact]

    public void DateRangesOverlap_ReturnsTrue_WhenRangesIntersect()

    {

        Assert.True(AvailabilityOverlapHelper.DateRangesOverlap(

            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10),

            new DateOnly(2026, 6, 5), new DateOnly(2026, 6, 15)));

    }



    [Fact]

    public void DateRangesOverlap_ReturnsFalse_WhenRangesDoNotIntersect()

    {

        Assert.False(AvailabilityOverlapHelper.DateRangesOverlap(

            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5),

            new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 15)));

    }



    [Fact]

    public void SlotRangesOverlap_ReturnsTrue_WhenEitherSideIsAllDay()

    {

        Assert.True(AvailabilityOverlapHelper.SlotRangesOverlap(

            null, null, null, null,

            1, 2, TimeSpan.FromHours(9), TimeSpan.FromHours(10)));

    }



    [Fact]

    public void SlotRangesOverlap_ReturnsTrue_WhenTimeIntervalsIntersect()

    {

        Assert.True(AvailabilityOverlapHelper.SlotRangesOverlap(

            1, 2, TimeSpan.FromHours(9), TimeSpan.FromHours(11),

            3, 4, TimeSpan.FromHours(10), TimeSpan.FromHours(12)));

    }



    [Fact]

    public void SlotRangesOverlap_ReturnsFalse_WhenTimeIntervalsDoNotIntersect()

    {

        Assert.False(AvailabilityOverlapHelper.SlotRangesOverlap(

            1, 2, TimeSpan.FromHours(9), TimeSpan.FromHours(10),

            3, 4, TimeSpan.FromHours(11), TimeSpan.FromHours(12)));

    }



    [Fact]

    public void HasOverlap_ReturnsFalse_WhenDatesDoNotOverlap()

    {

        Assert.False(AvailabilityOverlapHelper.HasOverlap(

            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), null, null, null, null,

            new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 15), null, null, null, null));

    }

}

