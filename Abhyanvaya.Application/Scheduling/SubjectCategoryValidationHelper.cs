using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.Scheduling;



public static class SubjectCategoryValidationHelper

{

    private static readonly HashSet<RoomType> LabRoomTypes =

    [

        RoomType.ComputerLab,

        RoomType.ScienceLab,

        RoomType.CommerceLab,

    ];



    public static bool IsLabCategoryCode(string? code) =>

        string.Equals(code, "Laboratory", StringComparison.OrdinalIgnoreCase)

        || string.Equals(code, "Lab", StringComparison.OrdinalIgnoreCase);



    public static bool IsTheoryCategoryCode(string? code) =>

        string.Equals(code, "Theory", StringComparison.OrdinalIgnoreCase);



    public static bool ValidateRoomTypeForCategory(string? categoryCode, RoomType? requiresRoomType, out string? error)

    {

        error = null;

        if (string.IsNullOrWhiteSpace(categoryCode) || !requiresRoomType.HasValue)

            return true;



        if (IsLabCategoryCode(categoryCode))

        {

            if (!LabRoomTypes.Contains(requiresRoomType.Value))

            {

                error = "Laboratory subjects require a lab room type (ComputerLab, ScienceLab, or CommerceLab).";

                return false;

            }

        }

        else if (IsTheoryCategoryCode(categoryCode))

        {

            if (requiresRoomType.Value != RoomType.Classroom)

            {

                error = "Theory subjects require Classroom as the required room type.";

                return false;

            }

        }



        return true;

    }

}

