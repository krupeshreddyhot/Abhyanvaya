namespace Abhyanvaya.Domain.Enums.Scheduling;

public enum TimetableApprovalRequestStatus : byte
{
    Pending = 1,
    InReview = 2,
    Approved = 3,
    Rejected = 4,
    Returned = 5,
    Cancelled = 6,
}
