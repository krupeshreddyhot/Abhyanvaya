using Abhyanvaya.Application.ClassroomAttendance;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Coordinates classroom attendance workflow — never performs recognition (AI20.PHASE2.4).</summary>
public interface IClassroomRecognitionOrchestrator
{
    Task<AttendanceSessionResult> ProcessSessionAsync(
        ClassroomPhotoMessage message,
        CancellationToken cancellationToken = default);
}
