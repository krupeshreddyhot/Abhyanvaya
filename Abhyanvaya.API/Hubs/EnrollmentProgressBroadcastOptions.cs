namespace Abhyanvaya.API.Hubs;

public sealed class EnrollmentProgressBroadcastOptions
{
    public const string SectionName = "EnrollmentProgressBroadcast";

    public bool Enabled { get; set; } = true;
}
