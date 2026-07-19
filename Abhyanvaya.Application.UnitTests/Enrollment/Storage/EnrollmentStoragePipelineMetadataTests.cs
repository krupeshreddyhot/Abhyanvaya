using System.Text.RegularExpressions;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Storage;

public sealed class EnrollmentStoragePipelineMetadataTests
{
    private static readonly Regex FeatureFlagPattern = new(
        @"^[A-Za-z][A-Za-z0-9.]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void DescribePipeline_ReturnsOrderedMetadataIncludingRollback()
    {
        var executor = CreateExecutor();

        var metadata = executor.DescribePipeline();

        Assert.Equal(11, metadata.Count);
        Assert.Equal(
            metadata.Select(m => m.Order).OrderBy(o => o).ToList(),
            metadata.Select(m => m.Order).ToList());
        Assert.Equal("Rollback", metadata[^1].Name);
        Assert.Equal(StorageStepCategory.Rollback, metadata[^1].Category);
    }

    [Fact]
    public void DescribePipeline_AllStepsHaveKnownCategories()
    {
        var executor = CreateExecutor();

        foreach (var step in executor.DescribePipeline())
        {
            Assert.True(StorageStepCategory.IsKnown(step.Category), $"Unknown category: {step.Category}");
            Assert.False(string.IsNullOrWhiteSpace(step.Name));
            Assert.False(string.IsNullOrWhiteSpace(step.Description));
            Assert.False(string.IsNullOrWhiteSpace(step.Version));
        }
    }

    [Fact]
    public void DescribePipeline_OptionalStepsHaveFeatureFlags()
    {
        var executor = CreateExecutor();
        var optionalSteps = executor.DescribePipeline().Where(s => s.Optional).ToList();

        Assert.Equal(2, optionalSteps.Count);
        Assert.All(optionalSteps, step =>
        {
            Assert.NotNull(step.FeatureFlag);
            Assert.Matches(FeatureFlagPattern, step.FeatureFlag!);
        });
        Assert.Contains(optionalSteps, s => s.Name == "Compression");
        Assert.Contains(optionalSteps, s => s.Name == "Encryption");
    }

    [Fact]
    public void DescribePipeline_RollbackCapableStepsAreUploadAndMetadata()
    {
        var executor = CreateExecutor();
        var rollbackSteps = executor.DescribePipeline()
            .Where(s => s.SupportsRollback)
            .Select(s => s.Name)
            .ToList();

        Assert.Equal(["Upload", "Metadata"], rollbackSteps);
    }

    [Fact]
    public void StepMetadata_OrderMatchesExecutionContract()
    {
        var executor = CreateExecutor();
        var executionSteps = executor.DescribePipeline()
            .Where(s => s.Category != StorageStepCategory.Rollback)
            .ToList();

        var expectedOrder = new[]
        {
            ("ValidateInput", 10),
            ("ResolvePolicy", 20),
            ("PrepareArtifacts", 30),
            ("Checksum", 40),
            ("Compression", 45),
            ("Encryption", 47),
            ("DuplicateDetection", 50),
            ("Upload", 60),
            ("Metadata", 70),
            ("Manifest", 80),
        };

        Assert.Equal(expectedOrder.Length, executionSteps.Count);
        for (var i = 0; i < expectedOrder.Length; i++)
        {
            Assert.Equal(expectedOrder[i].Item1, executionSteps[i].Name);
            Assert.Equal(expectedOrder[i].Item2, executionSteps[i].Order);
        }
    }

    private static IEnrollmentStoragePipelineExecutor CreateExecutor() =>
        EnrollmentStorageTestFactory.CreatePipelineExecutor(
            Mock.Of<IEnrollmentStoragePolicy>(),
            EnrollmentStorageTestFactory.CreateRegistry(),
            Mock.Of<IObjectStorageProvider>(),
            Mock.Of<IChecksumService>(),
            Mock.Of<IEnrollmentStorageRecordRepository>(),
            Mock.Of<IUnitOfWork>(),
            TimeProvider.System);
}
