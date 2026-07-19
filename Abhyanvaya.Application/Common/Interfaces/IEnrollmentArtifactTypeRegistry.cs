using Abhyanvaya.Application.Enrollment.Storage;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Registers supported enrollment artifact types without hardcoding in the storage service.</summary>
public interface IEnrollmentArtifactTypeRegistry
{
    IReadOnlyList<IEnrollmentArtifactTypeDefinition> GetAll();

    IReadOnlyList<IEnrollmentArtifactTypeDefinition> GetEnabled(EnrollmentStoragePolicyDecision policy);

    IEnrollmentArtifactTypeDefinition? Get(string artifactType);

    void Register(IEnrollmentArtifactTypeDefinition definition);
}
