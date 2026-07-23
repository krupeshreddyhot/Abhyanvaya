namespace Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Domain.Enums;

/// <summary>
/// Orchestrates classroom photo processing: detect → match → persist recognitions.
/// Does not finalize attendance; teacher review remains mandatory.
/// </summary>
public interface IClassroomRecognitionPipeline
{
    Task ProcessAsync(ClassroomPhotoMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Background job payload for classroom photo AI processing (AI22.7A Phase 3 scoped).</summary>
public sealed record ClassroomPhotoMessage(
    Guid AttendanceSessionId,
    int TenantId,
    string ImageStorageKey,
    int? RequestedByUserId,
    DateTime EnqueuedUtc,
    ClassroomRecognitionScope Scope = ClassroomRecognitionScope.FullSession,
    Guid? TargetImageId = null);
