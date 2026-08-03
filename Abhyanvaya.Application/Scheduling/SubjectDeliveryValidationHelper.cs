using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public static class SubjectDeliveryValidationHelper
{
    private static readonly HashSet<RoomType> LabRoomTypes =
    [
        RoomType.ComputerLab,
        RoomType.ScienceLab,
        RoomType.CommerceLab,
    ];

    public static bool IsLabDeliveryCode(string? code) =>
        string.Equals(code, "Laboratory", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "Lab", StringComparison.OrdinalIgnoreCase);

    public static bool IsTheoryDeliveryCode(string? code) =>
        string.Equals(code, "Theory", StringComparison.OrdinalIgnoreCase);

    public static bool IsOnlineDeliveryCode(string? code) =>
        string.Equals(code, "Online", StringComparison.OrdinalIgnoreCase);

    public static bool ValidateRoomTypeForDelivery(string? deliveryCode, RoomType? requiresRoomType, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(deliveryCode))
            return true;

        if (IsLabDeliveryCode(deliveryCode))
        {
            if (!requiresRoomType.HasValue || !LabRoomTypes.Contains(requiresRoomType.Value))
            {
                error = "Laboratory delivery requires a lab room type (ComputerLab, ScienceLab, or CommerceLab).";
                return false;
            }
        }
        else if (IsTheoryDeliveryCode(deliveryCode))
        {
            if (!requiresRoomType.HasValue || requiresRoomType.Value != RoomType.Classroom)
            {
                error = "Theory delivery requires Classroom as the required room type.";
                return false;
            }
        }
        else if (IsOnlineDeliveryCode(deliveryCode))
        {
            return true;
        }

        return true;
    }
}
