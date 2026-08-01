using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1B;

public sealed class SubjectDeliveryValidationTests
{
    [Fact]
    public void LaboratoryDelivery_RequiresLabRoomType()
    {
        var valid = SubjectDeliveryValidationHelper.ValidateRoomTypeForDelivery("Laboratory", RoomType.ScienceLab, out var error);
        Assert.True(valid);
        Assert.Null(error);

        var invalid = SubjectDeliveryValidationHelper.ValidateRoomTypeForDelivery("Laboratory", RoomType.Classroom, out error);
        Assert.False(invalid);
        Assert.NotNull(error);
    }

    [Fact]
    public void TheoryDelivery_RequiresClassroom()
    {
        var valid = SubjectDeliveryValidationHelper.ValidateRoomTypeForDelivery("Theory", RoomType.Classroom, out var error);
        Assert.True(valid);
        Assert.Null(error);

        var invalid = SubjectDeliveryValidationHelper.ValidateRoomTypeForDelivery("Theory", RoomType.ComputerLab, out error);
        Assert.False(invalid);
        Assert.NotNull(error);
    }

    [Fact]
    public void OnlineDelivery_AllowsOptionalRoomType()
    {
        var validNull = SubjectDeliveryValidationHelper.ValidateRoomTypeForDelivery("Online", null, out var error);
        Assert.True(validNull);
        Assert.Null(error);

        var validClassroom = SubjectDeliveryValidationHelper.ValidateRoomTypeForDelivery("Online", RoomType.Classroom, out error);
        Assert.True(validClassroom);
        Assert.Null(error);
    }
}
