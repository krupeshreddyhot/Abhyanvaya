namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Resolves registered face-embedding providers by name.
/// </summary>
public interface IEmbeddingProviderFactory
{
    /// <summary>Returns the provider registered for <paramref name="providerName"/>.</summary>
    /// <exception cref="NotSupportedException">When no provider matches the name.</exception>
    IEmbeddingGenerator GetProvider(string providerName);

    /// <summary>Returns the configured default provider, or the sole registered provider.</summary>
    /// <exception cref="NotSupportedException">When no providers are registered.</exception>
    IEmbeddingGenerator GetDefaultProvider();

    /// <summary>Lists all registered provider names (see <see cref="Domain.Constants.EmbeddingProviders"/>).</summary>
    IReadOnlyList<string> GetRegisteredProviders();
}
