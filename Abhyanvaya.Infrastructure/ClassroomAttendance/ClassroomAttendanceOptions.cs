using Abhyanvaya.Application.ClassroomAttendance;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class ClassroomAttendanceOptions
{
    public const string SectionName = "ClassroomAttendance";

    public float MinimumConfidence { get; set; } = 55f;

    public bool RequireTeacherApproval { get; set; } = true;

    public bool ManualReviewEnabled { get; set; } = true;

    public TimeSpan LateArrivalThreshold { get; set; } = TimeSpan.FromMinutes(15);

    public bool AllowDuplicateStudents { get; set; }

    public bool AllowReRecognition { get; set; } = true;

    public float UnknownFaceThreshold { get; set; } = 45f;

    public int DefaultTopK { get; set; } = 10;

    public int PipelineVersion { get; set; } = 1;
}

public sealed class ConfigurableAttendancePolicy : IAttendancePolicy
{
    private readonly ClassroomAttendanceOptions _options;

    public ConfigurableAttendancePolicy(IOptions<ClassroomAttendanceOptions> options)
    {
        _options = options.Value;
    }

    public float MinimumConfidence => _options.MinimumConfidence;

    public bool RequireTeacherApproval => _options.RequireTeacherApproval;

    public bool ManualReviewEnabled => _options.ManualReviewEnabled;

    public TimeSpan? AttendanceWindowStart => null;

    public TimeSpan? AttendanceWindowEnd => null;

    public TimeSpan LateArrivalThreshold => _options.LateArrivalThreshold;

    public bool AllowDuplicateStudents => _options.AllowDuplicateStudents;

    public bool AllowReRecognition => _options.AllowReRecognition;

    public float UnknownFaceThreshold => _options.UnknownFaceThreshold;
}
