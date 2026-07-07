using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Embedding;

/// <summary>
/// Resolves registered <see cref="IEmbeddingGenerator"/> implementations by provider name.
/// </summary>
public sealed class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly IReadOnlyDictionary<string, IEmbeddingGenerator> _providersByName;
    private readonly string? _defaultProviderName;

    public EmbeddingProviderFactory(
        IEnumerable<IEmbeddingGenerator> generators,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _providersByName = generators
            .GroupBy(g => g.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        _defaultProviderName = configuration["Embedding:DefaultProvider"];
    }

    public IEmbeddingGenerator GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new NotSupportedException("Provider name is required.");
        }

        if (!_providersByName.TryGetValue(providerName, out var provider))
        {
            throw new NotSupportedException(
                $"Face-embedding provider '{providerName}' is not registered. Known providers: {string.Join(", ", GetRegisteredProviders())}.");
        }

        return provider;
    }

    public IEmbeddingGenerator GetDefaultProvider()
    {
        if (!string.IsNullOrWhiteSpace(_defaultProviderName)
            && _providersByName.TryGetValue(_defaultProviderName, out var configured))
        {
            return configured;
        }

        if (_providersByName.Count == 1)
        {
            return _providersByName.Values.First();
        }

        if (_providersByName.Count == 0)
        {
            throw new NotSupportedException("No face-embedding providers are registered.");
        }

        throw new NotSupportedException(
            $"Multiple face-embedding providers are registered ({string.Join(", ", GetRegisteredProviders())}). " +
            "Configure Embedding:DefaultProvider to select the default.");
    }

    public IReadOnlyList<string> GetRegisteredProviders() =>
        _providersByName.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
}
