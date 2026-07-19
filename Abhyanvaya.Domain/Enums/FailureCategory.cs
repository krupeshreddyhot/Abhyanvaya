namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Specific reason a <see cref="Entities.StudentEnrollmentItem"/> is <see cref="EnrollmentStatus.Failed"/>
/// or <see cref="EnrollmentStatus.RetryRequired"/>. Drives both the automatic-retry classification
/// (docs/AI20_ENROLLMENT_ENGINE.md §7) and the SuperAdmin Failure Screen's reason filter
/// (docs/AI20_ENROLLMENT_UI.md §4).
/// </summary>
public enum FailureCategory
{
    /// <summary>Source returned HTTP 404 for the constructed photo URL. Permanent — not auto-retried.</summary>
    PhotoNotFound = 1,

    /// <summary>Source returned HTTP 403. Permanent — not auto-retried; usually a config/credentials issue.</summary>
    AccessDenied = 2,

    /// <summary>Downloaded bytes are not a recognizable image (e.g. an HTML error page). Permanent.</summary>
    InvalidImage = 3,

    /// <summary>Downloaded bytes have valid magic bytes but fail full image decode. Permanent.</summary>
    CorruptImage = 4,

    /// <summary>Zero faces detected in the photo. Permanent — the same photo will detect the same zero faces.</summary>
    NoFaceDetected = 5,

    /// <summary>More than one face detected. Permanent — enrollment requires exactly one face.</summary>
    MultipleFacesDetected = 6,

    /// <summary>Face crop failed the sharpness (variance-of-Laplacian) check. Permanent.</summary>
    BlurRejected = 7,

    /// <summary>Source image or face crop below the minimum resolution floor. Permanent.</summary>
    LowResolutionRejected = 8,

    /// <summary>Upload to the storage provider failed. Transient — eligible for automatic retry.</summary>
    StorageUploadFailed = 9,

    /// <summary>The ONNX/embedding engine threw during detection or embedding. Transient — eligible for automatic retry.</summary>
    EmbeddingEngineFailed = 10,

    /// <summary>The item was stuck past the configured timeout and was force-reset by the recovery sweep.</summary>
    Timeout = 11,

    /// <summary>Any failure not covered by a more specific category.</summary>
    Unknown = 99
}
