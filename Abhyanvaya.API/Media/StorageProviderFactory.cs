using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Media;

public sealed class StorageProviderFactory : IStorageProviderFactory
{
    private readonly MediaOptions _mediaOptions;
    private readonly LocalStorageProvider _local;
    private readonly S3StorageProvider _s3;

    public StorageProviderFactory(
        IOptions<MediaOptions> mediaOptions,
        LocalStorageProvider local,
        S3StorageProvider s3)
    {
        _mediaOptions = mediaOptions.Value;
        _local = local;
        _s3 = s3;
    }

    public string GetActiveProviderName() => _mediaOptions.GetActiveProviderName();

    public IStorageProvider GetActiveProvider() =>
        GetActiveProviderName() == S3StorageProvider.ProviderName ? _s3 : _local;
}
