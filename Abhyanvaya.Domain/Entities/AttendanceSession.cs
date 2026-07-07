using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Domain.Entities;

/// <summary>
/// Represents one attendance-taking event for a tenant-scoped class context.
/// A session is created for every attendance attempt—manual or automated—and tracks
/// workflow state, capture metadata, and optional AI processing results before
/// materializing <see cref="Attendance"/> rows.
/// </summary>
/// <remarks>
/// Supported capture channels (see <see cref="AttendanceMethod"/>):
/// <list type="bullet">
///   <item><description>Manual attendance — faculty marks students in the UI.</description></item>
///   <item><description>AI photo attendance — classroom image and recognition pipeline.</description></item>
///   <item><description>QR code attendance — student self-check-in via scanned code.</description></item>
///   <item><description>RFID attendance — card or tag reader events.</description></item>
///   <item><description>Biometric attendance — fingerprint or terminal hardware.</description></item>
///   <item><description>Future methods — extend <see cref="AttendanceMethod"/> without schema changes.</description></item>
/// </list>
/// Legacy manual rows may exist on <see cref="Attendance"/> with a null
/// <see cref="Attendance.AttendanceSessionId"/> until the manual workflow is fully session-based.
/// <para>
/// <b>Academic context (current release).</b>
/// Until the Timetable module ships, this entity stores denormalized class context directly:
/// <see cref="CourseId"/>, <see cref="GroupId"/>, <see cref="SemesterId"/>, <see cref="SubjectId"/>,
/// <see cref="PeriodNumber"/>, and <see cref="AttendanceDate"/>.
/// These fields allow attendance to operate independently of a published timetable.
/// </para>
/// <para>
/// <b>Academic context (future release — Timetable module).</b>
/// A future version will introduce <c>ClassSchedule</c> as the canonical source of when and where
/// a class meets. <see cref="AttendanceSession"/> will then reference <c>ClassSchedule</c>
/// (e.g. via <c>ClassScheduleId</c>) instead of relying solely on the denormalized fields above.
/// The existing columns are expected to remain during transition for backward compatibility and
/// reporting; new sessions created from the timetable will populate the schedule reference first.
/// Timetable is not implemented in this release—no schema or API changes are included yet.
/// </para>
/// <para>
/// <b>Soft delete.</b>
/// Unlike <see cref="BaseEntity"/> descendants, this type does not use <c>IsDeleted</c>.
/// Voided sessions are represented by <see cref="AttendanceSessionStatus.Cancelled"/>; rows remain
/// for audit and reporting. Tenant isolation is enforced via <see cref="ITenantScoped"/>.
/// </para>
/// <para>
/// <b>Staff accountability.</b>
/// <see cref="StaffId"/> identifies the staff member who initiated the session (manual, AI, QR, RFID, biometric).
/// This supports faculty attendance reports, AI adoption, workload, substitute tracking, and audit trails.
/// The initiator may differ from <see cref="ApprovedBy"/>, who approves the session after review.
/// </para>
/// <para>
/// <b>Session origin.</b>
/// <see cref="AttendanceSource"/> records which client or channel created the session
/// (web, mobile, API, background worker). Future values may be added to the enum.
/// </para>
/// <para>
/// <b>Multiple attempts (same class slot).</b>
/// <see cref="SessionNumber"/> distinguishes retries when attendance is taken more than once
/// for the same tenant, course, group, semester, subject, <see cref="AttendanceDate"/>, and
/// <see cref="PeriodNumber"/> (e.g. Period 2 Session 1 after AI failure, Period 2 Session 2 on retry).
/// <see cref="SessionName"/> provides an optional human-readable label (Morning Lecture, Lab Session, etc.).
/// </para>
/// <para>
/// <b>Recognition summary (pre-calculated).</b>
/// Count and percentage fields (<see cref="RecognizedCount"/> through
/// <see cref="RecognitionCompletionPercent"/>) store denormalized AI review metrics on the session
/// for dashboards and list views. They are updated when recognition processing or teacher review
/// completes—not computed on every read. Legacy counters <see cref="RecognizedFaces"/> and
/// <see cref="UnknownFaces"/> remain for backward compatibility.
/// </para>
/// </remarks>
public partial class AttendanceSession : ITenantScoped
{
    /// <summary>Unique identifier for this attendance event.</summary>
    public Guid Id { get; set; }

    /// <summary>College (tenant) that owns this session.</summary>
    public int TenantId { get; set; }

    /// <summary>
    /// Academic course for the class session.
    /// Denormalized academic context until the Timetable module provides <c>ClassSchedule</c>.
    /// </summary>
    public int CourseId { get; set; }

    /// <summary>
    /// Student group or stream within the course.
    /// Denormalized academic context until the Timetable module provides <c>ClassSchedule</c>.
    /// </summary>
    public int GroupId { get; set; }

    /// <summary>
    /// Semester in which the subject is offered.
    /// Denormalized academic context until the Timetable module provides <c>ClassSchedule</c>.
    /// </summary>
    public int SemesterId { get; set; }

    /// <summary>
    /// Subject being taught when attendance is taken.
    /// Denormalized academic context until the Timetable module provides <c>ClassSchedule</c>.
    /// </summary>
    public int SubjectId { get; set; }

    /// <summary>
    /// Calendar date of the attendance event (local college date).
    /// Denormalized academic context until the Timetable module provides <c>ClassSchedule</c>.
    /// </summary>
    public DateTime AttendanceDate { get; set; }

    /// <summary>
    /// Optional timetable period or slot number for the session.
    /// Denormalized academic context until the Timetable module provides <c>ClassSchedule</c>.
    /// </summary>
    public int? PeriodNumber { get; set; }

    /// <summary>
    /// Sequential attempt number when more than one session exists for the same class slot
    /// (tenant, course, group, semester, subject, date, and period). Defaults to 1 for the first attempt;
    /// retries increment to 2, 3, 4, and so on.
    /// </summary>
    public short SessionNumber { get; set; } = 1;

    /// <summary>
    /// Optional display name for the session (e.g. Morning Lecture, Lab Session, Extra Class, Tutorial, Guest Lecture).
    /// </summary>
    public string? SessionName { get; set; }

    /// <summary>How attendance was captured for this event.</summary>
    public AttendanceMethod AttendanceMethod { get; set; } = AttendanceMethod.Manual;

    /// <summary>
    /// Client or channel that originated this session (web, mobile, API, background worker).
    /// Defaults to <see cref="Enums.AttendanceSource.Web"/> when created from the browser application.
    /// </summary>
    public AttendanceSource AttendanceSource { get; set; } = AttendanceSource.Web;

    /// <summary>
    /// Optional timetable schedule that originated this session.
    /// Denormalized academic fields remain for reporting and manual sessions.
    /// </summary>
    public Guid? ClassScheduleId { get; set; }

    /// <summary>Current workflow state of this attendance event.</summary>
    public AttendanceSessionStatus Status { get; private set; } = AttendanceSessionStatus.Draft;

    /// <summary>Name of the external recognition provider (e.g. AWS Rekognition).</summary>
    public string? RecognitionProvider { get; set; }

    /// <summary>Model or API version used for face recognition.</summary>
    public string? RecognitionModel { get; set; }

    /// <summary>
    /// Version identifier of the AI recognition pipeline that produced session recognitions
    /// (e.g. InsightFace-1.0, ArcFace-v3). Populated during AI processing.
    /// </summary>
    public string? RecognitionPipelineVersion { get; set; }

    /// <summary>Error message when processing fails or validation is rejected.</summary>
    public string? ProcessingError { get; set; }

    /// <summary>Expected number of enrolled students for this class session.</summary>
    public int TotalStudents { get; set; }

    /// <summary>Number of faces detected in the session image.</summary>
    public int DetectedFaces { get; set; }

    /// <summary>Number of faces matched to enrolled students.</summary>
    public int RecognizedFaces { get; set; }

    /// <summary>Number of detected faces that could not be matched.</summary>
    public int UnknownFaces { get; set; }

    /// <summary>Pre-calculated count of recognitions with <see cref="Enums.RecognitionStatus.Recognized"/>.</summary>
    public int RecognizedCount { get; set; }

    /// <summary>Pre-calculated count of recognitions with <see cref="Enums.RecognitionStatus.Unknown"/>.</summary>
    public int UnknownCount { get; set; }

    /// <summary>Pre-calculated count of recognitions with <see cref="Enums.RecognitionStatus.Rejected"/>.</summary>
    public int RejectedCount { get; set; }

    /// <summary>Pre-calculated count of recognitions with <see cref="Enums.RecognitionStatus.Ignored"/>.</summary>
    public int IgnoredCount { get; set; }

    /// <summary>Pre-calculated count of recognitions with <see cref="Enums.RecognitionStatus.Duplicate"/>.</summary>
    public int DuplicateCount { get; set; }

    /// <summary>Pre-calculated count of recognitions with <see cref="Enums.RecognitionStatus.ManuallyAssigned"/>.</summary>
    public int ManualAssignmentCount { get; set; }

    /// <summary>Pre-calculated count of recognitions with <see cref="Enums.RecognitionStatus.LowConfidence"/>.</summary>
    public int LowConfidenceCount { get; set; }

    /// <summary>Pre-calculated mean match confidence (0–100 scale) across detected faces.</summary>
    public decimal? AverageConfidence { get; set; }

    /// <summary>
    /// Pre-calculated percentage of recognition rows that have completed teacher review
    /// or reached a terminal AI status (0–100 scale).
    /// </summary>
    public decimal? RecognitionCompletionPercent { get; set; }

    /// <summary>Wall-clock processing duration in milliseconds.</summary>
    public int? ProcessingMilliseconds { get; set; }

    /// <summary>UTC timestamp when processing started.</summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>UTC timestamp when processing completed (success or failure).</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>UTC timestamp when the session record was created.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>User login that created this session record (audit).</summary>
    public int? CreatedBy { get; set; }

    /// <summary>
    /// Staff member who initiated the attendance session (faculty or substitute).
    /// Used for attendance-by-faculty, AI adoption, workload, and audit reports.
    /// May differ from <see cref="ApprovedBy"/>, who approves the session after review.
    /// </summary>
    public int? StaffId { get; set; }

    /// <summary>User who approved the session after review.</summary>
    public int? ApprovedBy { get; set; }

    /// <summary>UTC timestamp when the session was approved.</summary>
    public DateTime? ApprovedUtc { get; set; }

    /// <summary>Number of times automated processing has been retried.</summary>
    public int RetryCount { get; set; }

    /// <summary>Optimistic concurrency token for concurrent updates.</summary>
    public byte[] RowVersion { get; set; } = null!;

    /// <summary>Navigation to the course.</summary>
    public Course? Course { get; set; }

    /// <summary>Navigation to the group.</summary>
    public Group? Group { get; set; }

    /// <summary>Navigation to the semester.</summary>
    public Semester? Semester { get; set; }

    /// <summary>Navigation to the subject.</summary>
    public Subject? Subject { get; set; }

    /// <summary>Navigation to the originating timetable schedule, when applicable.</summary>
    public ClassSchedule? ClassSchedule { get; set; }

    /// <summary>Navigation to the staff member who initiated this session.</summary>
    public Staff? Staff { get; set; }

    /// <summary>Navigation to the approving user.</summary>
    public User? ApprovedByUser { get; set; }

    /// <summary>Attendance rows linked to this session after processing and approval.</summary>
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    /// <summary>AI recognition results for detected faces; not official attendance until approved.</summary>
    public ICollection<AttendanceRecognition> Recognitions { get; set; } = new List<AttendanceRecognition>();
}
