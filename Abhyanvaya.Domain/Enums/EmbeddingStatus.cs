namespace Abhyanvaya.Domain.Enums;

/// <summary>
/// Lifecycle state of a face-embedding generation job or stored vector.
/// </summary>
public enum EmbeddingStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Inactive = 4
}
