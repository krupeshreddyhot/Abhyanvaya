using Microsoft.Extensions.Hosting;

namespace Abhyanvaya.Infrastructure.InsightFace;

public static class InsightFaceModelPathResolver
{
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

    public static bool AllModelsPresent(InsightFaceOptions options, IHostEnvironment environment)
    {
        var directory = Resolve(options.ModelDirectory, environment);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var detection = Path.Combine(directory, options.DetectionModelFile);
        var recognition = Path.Combine(directory, options.RecognitionModelFile);
        return File.Exists(detection) && File.Exists(recognition)
               && new FileInfo(detection).Length > 4096
               && new FileInfo(recognition).Length > 4096;
    }
}
