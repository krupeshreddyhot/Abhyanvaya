namespace Abhyanvaya.API.Media;

public sealed class StorageWriteOptions
{
    public required string ContentType { get; init; }
    public string? CacheControl { get; init; }
}
