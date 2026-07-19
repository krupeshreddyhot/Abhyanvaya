using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Application.Enrollment.Pipeline.Manifest;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration;

public sealed class EnrollmentPipelineRegistry : IEnrollmentPipelineRegistry
{
    private const string PersistenceStageName = "Persistence";

    private readonly IPipelineManifestProvider _manifestProvider;
    private readonly IReadOnlyList<IEnrollmentPipelineStage> _stages;

    public EnrollmentPipelineRegistry(
        IPipelineManifestProvider manifestProvider,
        IEnumerable<IEnrollmentPipelineStage> stages)
    {
        _manifestProvider = manifestProvider;
        _stages = stages.ToList();
    }

    public IReadOnlyList<IEnrollmentPipelineStage> GetOrderedStages(int pipelineVersion)
    {
        var manifest = _manifestProvider.GetManifest(EnrollmentPipelineDefaults.PipelineName, pipelineVersion);
        var handlersByManifest = _stages
            .Where(stage => stage.ManifestStage.HasValue)
            .GroupBy(stage => stage.ManifestStage!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var persistenceStage = _stages.FirstOrDefault(stage =>
            string.Equals(stage.Name, PersistenceStageName, StringComparison.Ordinal));

        var ordered = new List<IEnrollmentPipelineStage>();

        foreach (var entry in manifest.Stages.Where(stage => stage.Enabled).OrderBy(stage => stage.Order))
        {
            if (entry.Stage == EnrollmentPipelineStage.Finalize)
            {
                if (handlersByManifest.TryGetValue(EnrollmentPipelineStage.Finalize, out var finalizeStage))
                {
                    ordered.Add(finalizeStage);
                }

                continue;
            }

            if (!handlersByManifest.TryGetValue(entry.Stage, out var handler))
            {
                continue;
            }

            ordered.Add(handler);

            if (entry.Stage == EnrollmentPipelineStage.Embedding && persistenceStage is not null)
            {
                ordered.Add(persistenceStage);
            }
        }

        return ordered;
    }
}
