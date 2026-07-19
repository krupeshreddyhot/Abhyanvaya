using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.DTOs.Recognition;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Coordinates multi-face recognition dispatch — no attendance decisions (AI20.PHASE2.4).</summary>
public interface IMultiFaceRecognitionCoordinator
{
    Task<IReadOnlyList<FaceRecognitionOutcome>> RecognizeFacesAsync(
        AttendanceSessionContext context,
        IReadOnlyList<DetectedFaceDto> faces,
        byte[] imageBytes,
        CancellationToken cancellationToken = default);
}
