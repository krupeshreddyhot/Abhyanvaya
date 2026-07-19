namespace Abhyanvaya.Application.Enrollment.Validation;

public static class EnrollmentValidationRuleIds
{
    public const string ImageFormat = "ImageFormat";
    public const string CorruptImage = "CorruptImage";
    public const string MinimumSourceResolution = "MinimumSourceResolution";
    public const string MaximumSourceResolution = "MaximumSourceResolution";
    public const string ExactlyOneFace = "ExactlyOneFace";
    public const string FaceConfidence = "FaceConfidence";
    public const string MinimumFaceCropResolution = "MinimumFaceCropResolution";
    public const string FaceSizeCoverage = "FaceSizeCoverage";
    public const string BlurScore = "BlurScore";
    public const string Pose = "Pose";
    public const string Brightness = "Brightness";
    public const string Contrast = "Contrast";

    public const string Liveness = "Liveness";
    public const string MaskDetection = "MaskDetection";
    public const string EyeOpenness = "EyeOpenness";
    public const string SpoofDetection = "SpoofDetection";
    public const string Occlusion = "Occlusion";
    public const string Sunglasses = "Sunglasses";
    public const string Smile = "Smile";
    public const string Expression = "Expression";
}
