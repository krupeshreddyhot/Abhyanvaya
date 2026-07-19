using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Infrastructure.ArtifactStorage;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Abhyanvaya.Application.UnitTests.ArtifactStorage;

public sealed class LocalArtifactStorageProviderTests
{
    [Fact]
    public async Task LocalArtifactStorageProvider_UploadsAndVerifiesOnDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "abhyanvaya-artifacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var environment = CreateEnvironment(root, "Development");
        var options = Microsoft.Extensions.Options.Options.Create(new ArtifactStorageOptions
        {
            Provider = "local",
            PhysicalRoot = "artifacts",
        });

        var provider = new LocalArtifactStorageProvider(
            environment,
            options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalArtifactStorageProvider>.Instance);

        var content = new byte[] { 1, 2, 3, 4, 5 };
        var metadata = new ArtifactMetadata
        {
            ArtifactType = "aligned-face",
            ContentType = "image/jpeg",
            FileSize = content.Length,
            Checksum = "ABC123",
            Version = "1.0",
            CreatedUtc = DateTime.UtcNow,
            RetentionPolicy = "STANDARD",
            StorageClass = "STANDARD",
        };

        await using (var stream = new MemoryStream(content))
        {
            await provider.UploadAsync("tenant-1/student-42/face.jpg", stream, metadata);
        }

        Assert.True(await provider.VerifyExistsAsync("tenant-1/student-42/face.jpg", content.Length));
        Assert.Equal("local", provider.ProviderName);
        Assert.Equal("local", provider.Bucket);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ResolveProviderName_UsesLocalInDevelopmentWhenUnset()
    {
        var environment = CreateEnvironment(Path.GetTempPath(), "Development");
        var options = new ArtifactStorageOptions { Provider = string.Empty };

        var provider = ArtifactStorageProviderSelection.ResolveProviderName(options, environment);

        Assert.Equal(LocalArtifactStorageProvider.ProviderId, provider);
    }

    [Fact]
    public void ResolveProviderName_UsesR2InProductionWhenUnset()
    {
        var environment = CreateEnvironment(Path.GetTempPath(), "Production");
        var options = new ArtifactStorageOptions { Provider = string.Empty };

        var provider = ArtifactStorageProviderSelection.ResolveProviderName(options, environment);

        Assert.Equal(R2StorageProvider.ProviderId, provider);
    }

    private static IHostEnvironment CreateEnvironment(string contentRootPath, string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(contentRootPath);
        environment.Setup(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
