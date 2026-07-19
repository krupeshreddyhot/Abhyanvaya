namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Resolves registered <see cref="IStudentPhotoProvider"/> implementations by name. Mirrors
/// <see cref="IEmbeddingProviderFactory"/> exactly: the active provider is selected entirely through
/// configuration (<c>StudentPhotoProvider:DefaultProvider</c>), never hardcoded in this factory or in
/// any caller — see docs/AI20_ENROLLMENT_ARCHITECTURE.md §4 and docs/AI20_PHOTO_IMPORT.md.
/// </summary>
public interface IStudentPhotoProviderFactory
{
    /// <summary>Returns the provider registered for <paramref name="providerName"/>.</summary>
    /// <exception cref="NotSupportedException">When no provider matches the name.</exception>
    IStudentPhotoProvider GetProvider(string providerName);

    /// <summary>Returns the configured default provider, or the sole registered provider.</summary>
    /// <exception cref="NotSupportedException">When no providers are registered, or more than one is registered with no configured default.</exception>
    IStudentPhotoProvider GetDefaultProvider();

    /// <summary>Lists all registered provider names (see <see cref="Domain.Constants.StudentPhotoProviders"/>).</summary>
    IReadOnlyList<string> GetRegisteredProviders();
}
