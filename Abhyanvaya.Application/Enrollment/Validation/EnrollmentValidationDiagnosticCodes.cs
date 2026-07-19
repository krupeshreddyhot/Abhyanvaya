namespace Abhyanvaya.Application.Enrollment.Validation;

public static class EnrollmentValidationDiagnosticCodes
{
    public const string UnsupportedFormat = "VAL_UNSUPPORTED_FORMAT";
    public const string CorruptImage = "VAL_CORRUPT_IMAGE";
    public const string SourceResTooLow = "VAL_SOURCE_RES_TOO_LOW";
    public const string SourceResTooHigh = "VAL_SOURCE_RES_TOO_HIGH";
    public const string NoFace = "VAL_NO_FACE";
    public const string MultipleFaces = "VAL_MULTIPLE_FACES";
    public const string LowFaceConfidence = "VAL_LOW_FACE_CONFIDENCE";
    public const string FaceCropTooSmall = "VAL_FACE_CROP_TOO_SMALL";
    public const string FaceTooSmallInFrame = "VAL_FACE_TOO_SMALL_IN_FRAME";
    public const string BlurRejected = "VAL_BLUR_REJECTED";
    public const string PoseRejected = "VAL_POSE_REJECTED";
    public const string BrightnessRejected = "VAL_BRIGHTNESS_REJECTED";
    public const string ContrastRejected = "VAL_CONTRAST_REJECTED";
    public const string Cancelled = "VAL_CANCELLED";
}
