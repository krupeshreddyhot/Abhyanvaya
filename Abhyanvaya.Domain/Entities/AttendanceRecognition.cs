using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Stores the output produced by the AI recognition engine for one detected face
/// within an <see cref="AttendanceSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not an attendance record.</b>
/// <see cref="AttendanceRecognition"/> holds provisional AI results only. Official attendance
/// is persisted in <see cref="Attendance"/> (and related detail) after a teacher reviews
/// and approves the session—not when recognition completes.
/// </para>
/// <para>
/// <b>Teacher review workflow.</b>
/// Each row moves through <see cref="RecognitionStatus"/> and review flags
/// (<see cref="VerifiedByTeacher"/>, <see cref="TeacherOverride"/>, <see cref="ReviewNotes"/>)
/// before the session reaches <see cref="AttendanceSessionStatus.Approved"/> and attendance rows are created.
/// </para>
/// <para>
/// <b>Future: multiple AI providers.</b>
/// Provider and model metadata live on the parent <see cref="AttendanceSession"/>
/// (<see cref="AttendanceSession.RecognitionProvider"/>, <see cref="AttendanceSession.RecognitionModel"/>).
/// Additional provider-specific fields or a provider registry may be introduced without changing
/// the core shape of this entity.
/// </para>
/// <para>
/// <b>Future: multiple photos.</b>
/// A session may process more than one image; each face in each image is stored as a separate
/// recognition row keyed by <see cref="AttendanceSessionId"/> and <see cref="FaceNumber"/>.
/// </para>
/// <para>
/// <b>Future: unknown faces.</b>
/// Faces with no student match use <see cref="RecognitionStatus.Unknown"/> and a null
/// <see cref="StudentId"/> until a teacher assigns or ignores them
/// (<see cref="RecognitionStatus.Ignored"/>, <see cref="RecognitionStatus.ManuallyAssigned"/>).
/// </para>
/// <para>
/// <b>Future: teacher corrections.</b>
/// <see cref="TeacherOverride"/> and <see cref="RecognitionStatus.ManuallyAssigned"/> capture
/// when a teacher changes the AI-assigned student or status. <see cref="VerifiedByTeacher"/>
/// records explicit confirmation of a match.
/// </para>
/// <para>
/// <b>Audit history.</b>
/// Each teacher review action is recorded in <see cref="AttendanceRecognitionReviewHistory"/>
/// with before/after status and student assignment.
/// </para>
/// Recognition rows are removed when the parent <see cref="AttendanceSession"/> is deleted (cascade).
/// </remarks>
public class AttendanceRecognition : ITenantScoped
{
    /// <summary>Unique identifier for this recognition result.</summary>
    public Guid Id { get; set; }

    /// <summary>College (tenant) that owns this recognition row; denormalized from the session.</summary>
    public int TenantId { get; set; }

    /// <summary>Parent session that produced this recognition result.</summary>
    public Guid AttendanceSessionId { get; set; }

    /// <summary>
    /// Matched or teacher-assigned student; null for unknown or unreviewed faces.
    /// See <see cref="RecognitionStatus.Unknown"/> and future unknown-face handling.
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>
    /// Sequential face index within the session image (1-based ordering).
    /// Supports multiple faces—and future multiple photos—per session.
    /// </summary>
    public int FaceNumber { get; set; }

    /// <summary>
    /// Identifies which uploaded classroom image produced this recognition (1-based).
    /// Supports future multi-image sessions: Image 1 Face 3, Image 2 Face 7, etc.
    /// </summary>
    public short ImageSequence { get; set; } = 1;

    /// <summary>
    /// Storage key for the cropped face image generated during AI processing
    /// (e.g. recognitions/{tenantId}/{sessionId}/faces/00012.webp).
    /// </summary>
    public string? FaceImageKey { get; set; }

    /// <summary>
    /// AI matching outcome for this face.
    /// See <see cref="RecognitionStatus"/> for values including
    /// <see cref="RecognitionStatus.Recognized"/>,
    /// <see cref="RecognitionStatus.LowConfidence"/>,
    /// <see cref="RecognitionStatus.Duplicate"/>,
    /// <see cref="RecognitionStatus.Ignored"/>,
    /// <see cref="RecognitionStatus.Rejected"/>, and
    /// <see cref="RecognitionStatus.ManuallyAssigned"/>.
    /// </summary>
    public RecognitionStatus RecognitionStatus { get; set; } = RecognitionStatus.Unknown;

    /// <summary>Match confidence score (0–100 scale) from the AI engine.</summary>
    public decimal? ConfidenceScore { get; set; }

    /// <summary>Distance between face embedding and matched student photo embedding.</summary>
    public decimal? EmbeddingDistance { get; set; }

    /// <summary>Bounding box left edge (pixels) for overlay and review UI.</summary>
    public int BoundingBoxX { get; set; }

    /// <summary>Bounding box top edge (pixels) for overlay and review UI.</summary>
    public int BoundingBoxY { get; set; }

    /// <summary>Bounding box width (pixels) for overlay and review UI.</summary>
    public int BoundingBoxWidth { get; set; }

    /// <summary>Bounding box height (pixels) for overlay and review UI.</summary>
    public int BoundingBoxHeight { get; set; }

    /// <summary>Time taken to recognize this face in milliseconds.</summary>
    public int? RecognitionTimeMilliseconds { get; set; }

    /// <summary>True when a teacher confirmed this recognition during review.</summary>
    public bool VerifiedByTeacher { get; set; }

    /// <summary>
    /// True when a teacher manually corrected the AI-assigned student or status.
    /// Typically paired with <see cref="RecognitionStatus.ManuallyAssigned"/>.
    /// </summary>
    public bool TeacherOverride { get; set; }

    /// <summary>Teacher notes captured during review; supports audit and correction context.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>UTC timestamp when this recognition row was created by the AI pipeline.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>Optimistic concurrency token for concurrent teacher review updates.</summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>Navigation to the parent attendance session.</summary>
    public AttendanceSession AttendanceSession { get; set; } = null!;

    /// <summary>Navigation to the matched or manually assigned student.</summary>
    public Student? Student { get; set; }

    /// <summary>Immutable audit trail of teacher review actions for this recognition row.</summary>
    public ICollection<AttendanceRecognitionReviewHistory> ReviewHistory { get; set; } =
        new List<AttendanceRecognitionReviewHistory>();
}
