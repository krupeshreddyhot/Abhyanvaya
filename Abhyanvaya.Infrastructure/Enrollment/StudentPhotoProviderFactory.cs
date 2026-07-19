using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Abhyanvaya.Infrastructure.Enrollment;

/// <summary>
/// Resolves registered <see cref="IStudentPhotoProvider"/> implementations by provider name.
/// Structurally identical to <see cref="Embedding.EmbeddingProviderFactory"/> — see that type's
/// remarks for why this shape (config-selected default, graceful single-provider fallback,
/// explicit error when ambiguous) is the established pattern for provider resolution in this codebase.
/// </summary>
public sealed class StudentPhotoProviderFactory : IStudentPhotoProviderFactory
{
    private readonly IReadOnlyDictionary<string, IStudentPhotoProvider> _providersByName;
    private readonly string? _defaultProviderName;

    public StudentPhotoProviderFactory(
        IEnumerable<IStudentPhotoProvider> providers,
        IConfiguration configuration)
    {
        _providersByName = providers
            .GroupBy(p => p.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        _defaultProviderName = configuration["StudentPhotoProvider:DefaultProvider"];
    }

    public IStudentPhotoProvider GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new NotSupportedException("Provider name is required.");
        }

        if (!_providersByName.TryGetValue(providerName, out var provider))
        {
            throw new NotSupportedException(
                $"Student photo provider '{providerName}' is not registered. Known providers: {string.Join(", ", GetRegisteredProviders())}.");
        }

        return provider;
    }

    public IStudentPhotoProvider GetDefaultProvider()
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
            throw new NotSupportedException("No student photo providers are registered.");
        }

        throw new NotSupportedException(
            $"Multiple student photo providers are registered ({string.Join(", ", GetRegisteredProviders())}). " +
            "Configure StudentPhotoProvider:DefaultProvider to select the default.");
    }

    public IReadOnlyList<string> GetRegisteredProviders() =>
        _providersByName.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
}
