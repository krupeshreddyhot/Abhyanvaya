using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

internal sealed class EnrollmentArtifactTypeRegistry : IEnrollmentArtifactTypeRegistry
{
    private readonly List<IEnrollmentArtifactTypeDefinition> _definitions;

    public EnrollmentArtifactTypeRegistry(IEnumerable<IEnrollmentArtifactTypeDefinition> definitions)
    {
        _definitions = definitions.ToList();
    }

    public IReadOnlyList<IEnrollmentArtifactTypeDefinition> GetAll() => _definitions;

    public IReadOnlyList<IEnrollmentArtifactTypeDefinition> GetEnabled(EnrollmentStoragePolicyDecision policy) =>
        _definitions
            .Where(d => policy.EnabledArtifactTypes.Contains(d.ArtifactType))
            .OrderBy(d => d.ArtifactType, StringComparer.Ordinal)
            .ToList();

    public IEnrollmentArtifactTypeDefinition? Get(string artifactType) =>
        _definitions.FirstOrDefault(d =>
            string.Equals(d.ArtifactType, artifactType, StringComparison.Ordinal));

    public void Register(IEnrollmentArtifactTypeDefinition definition) => _definitions.Add(definition);
}
