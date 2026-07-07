namespace Abhyanvaya.Application.DTOs.AttendanceRecognition;

/// <summary>Batch of recognition reviews for one attendance session.</summary>
public sealed class AttendanceRecognitionBatchReviewRequest
{
    public Guid AttendanceSessionId { get; set; }

    public List<AttendanceRecognitionReviewRequest> Reviews { get; set; } = new();
}
