using Abhyanvaya.Application.Scheduling;

using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1A;



public sealed class SubjectCategoryValidationTests

{

    [Fact]

    public void LaboratoryCategory_RequiresLabRoomType()

    {

        var valid = SubjectCategoryValidationHelper.ValidateRoomTypeForCategory("Laboratory", RoomType.ComputerLab, out var error);

        Assert.True(valid);

        Assert.Null(error);



        var invalid = SubjectCategoryValidationHelper.ValidateRoomTypeForCategory("Laboratory", RoomType.Classroom, out error);

        Assert.False(invalid);

        Assert.NotNull(error);

    }



    [Fact]

    public void TheoryCategory_RequiresClassroom()

    {

        var valid = SubjectCategoryValidationHelper.ValidateRoomTypeForCategory("Theory", RoomType.Classroom, out var error);

        Assert.True(valid);

        Assert.Null(error);



        var invalid = SubjectCategoryValidationHelper.ValidateRoomTypeForCategory("Theory", RoomType.ScienceLab, out error);

        Assert.False(invalid);

        Assert.NotNull(error);

    }



    [Fact]

    public void OtherCategories_AllowAnyRoomType()

    {

        var valid = SubjectCategoryValidationHelper.ValidateRoomTypeForCategory("Tutorial", RoomType.Seminar, out var error);

        Assert.True(valid);

        Assert.Null(error);

    }

}

