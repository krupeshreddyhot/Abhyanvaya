namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Identifies how attendance was captured for an <see cref="Entities.AttendanceSession"/>.
/// Each session records exactly one method; the platform supports manual and automated channels,
/// with room for future methods via new enum values.
/// </summary>
public enum AttendanceMethod
{
    /// <summary>Faculty marks present/absent directly in the application.</summary>
    Manual = 1,

    /// <summary>Class photo uploaded and processed by face-recognition services.</summary>
    AIPhoto = 2,

    /// <summary>Students scan a QR code displayed in the classroom.</summary>
    QRCode = 3,

    /// <summary>Students tap RFID cards or tags at a reader.</summary>
    RFID = 4,

    /// <summary>Students authenticate via fingerprint, face terminal, or similar hardware.</summary>
    Biometric = 5,

    /// <summary>Rows imported from spreadsheets or external systems.</summary>
    Imported = 6,

    /// <summary>Submitted by an external integration through the API.</summary>
    API = 7
}
