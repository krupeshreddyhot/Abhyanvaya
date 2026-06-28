namespace Abhyanvaya.API.Media;

/// <summary>Resolves the active <see cref="IStorageProvider"/> from application configuration.</summary>
public interface IStorageProviderFactory
{
    IStorageProvider GetActiveProvider();

    string GetActiveProviderName();
}
