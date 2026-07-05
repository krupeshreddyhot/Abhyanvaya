using Microsoft.Extensions.Hosting;

namespace Abhyanvaya.API.Diagnostics;

/// <summary>
/// Resolves the configured InsightFace model directory into an absolute path anchored at the
/// application's content root, so model lookups behave identically regardless of the process's
/// current working directory — which varies across <c>dotnet run</c>, Visual Studio's debugger,
/// IIS, a Windows Service, and Docker/Linux container entrypoints. Relative paths remain fully
/// supported; they are simply resolved deterministically instead of depending on an ambient
/// working directory.
/// </summary>
public static class ModelPathResolver
{
    /// <summary>
    /// Returns <paramref name="configuredModelDirectory"/> unchanged if it is already rooted
    /// (absolute); otherwise resolves it relative to <see cref="IHostEnvironment.ContentRootPath"/>.
    /// </summary>
    public static string Resolve(string configuredModelDirectory, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(configuredModelDirectory))
        {
            return configuredModelDirectory;
        }

        return Path.IsPathRooted(configuredModelDirectory)
            ? configuredModelDirectory
            : Path.Combine(environment.ContentRootPath, configuredModelDirectory);
    }
}
