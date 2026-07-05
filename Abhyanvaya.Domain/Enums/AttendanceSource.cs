namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Identifies the client or channel that originated an <see cref="Entities.AttendanceSession"/>.
/// Distinct from <see cref="AttendanceMethod"/>, which describes how attendance was captured.
/// New values may be added without schema changes.
/// </summary>
public enum AttendanceSource
{
    /// <summary>Session created from the web application (browser).</summary>
    Web = 1,

    /// <summary>Session created from a mobile client (native or hybrid app).</summary>
    Mobile = 2,

    /// <summary>Session created by an external system via the REST API.</summary>
    API = 3,

    /// <summary>Session created automatically by a background worker or scheduled job.</summary>
    BackgroundWorker = 4
}
