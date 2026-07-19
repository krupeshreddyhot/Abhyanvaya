using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment;
using Abhyanvaya.Application.Enrollment.Versioning;
using Abhyanvaya.Infrastructure.Enrollment.Configuration;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Versioning;

public sealed class ConfigurationPipelineVersionProvider : IPipelineVersionProvider
{
    private readonly EnrollmentPipelineOptions _options;

    public ConfigurationPipelineVersionProvider(IOptions<EnrollmentPipelineOptions> options)
    {
        _options = options.Value;
    }

    public PipelineVersion GetActiveVersionForNewBatch(EnrollmentBatchRequest request) =>
        new(Math.Max(1, _options.ActiveVersion));

    public bool VersionExists(PipelineVersion version) =>
        version.Value == 1
        || _options.Versions.ContainsKey(version.Value)
        || version.Value == _options.ActiveVersion;
}
