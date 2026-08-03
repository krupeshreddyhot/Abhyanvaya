using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.RoomFeatures.Validators;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1B;

public sealed class RoomFeatureAssignmentTests
{
    [Fact]
    public void CloneValidator_RejectsSameSourceAndTargetRoom()
    {
        var validator = new CloneRoomFeatureAssignmentsRequestValidator();
        var result = validator.Validate(new CloneRoomFeatureAssignmentsRequest { FromRoomId = 1, ToRoomId = 1 });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void AssignValidator_RequiresFeatureId()
    {
        var validator = new AssignRoomFeatureRequestValidator();
        var result = validator.Validate(new AssignRoomFeatureRequest { RoomFeatureId = 0 });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateFeatureValidator_RejectsInvalidCategory()
    {
        var validator = new CreateRoomFeatureRequestValidator();
        var result = validator.Validate(new CreateRoomFeatureRequest
        {
            Code = "Test",
            Name = "Test Feature",
            Category = "Invalid",
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateFeatureValidator_AcceptsValidCategory()
    {
        var validator = new CreateRoomFeatureRequestValidator();
        var result = validator.Validate(new CreateRoomFeatureRequest
        {
            Code = "Test",
            Name = "Test Feature",
            Category = "Equipment",
        });
        Assert.True(result.IsValid);
    }
}
