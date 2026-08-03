namespace Abhyanvaya.Application.UnitTests.Architecture;

/// <summary>
/// AI30 AC1.5 Architecture Guard — fails the build when Scheduling re-introduces Catalog master CRUD.
/// Source of truth: docs/AI30_MASTER_DATA_OWNERSHIP_MATRIX.md
/// </summary>
public sealed class ArchitectureOwnershipTests
{
    [Fact]
    public void MasterDataOwnership_IsCompliant()
    {
        var validator = new MasterOwnershipValidator();
        var report = validator.Validate();

        Assert.True(
            report.IsCompliant,
            "Architecture ownership validation failed.\n\n" + report.ToMarkdown());
    }

    [Theory]
    [InlineData("Department")]
    [InlineData("Course")]
    [InlineData("Group")]
    [InlineData("Semester")]
    [InlineData("Subject")]
    [InlineData("Staff")]
    [InlineData("Language")]
    [InlineData("Medium")]
    [InlineData("Gender")]
    [InlineData("Role")]
    public void CatalogOwnedMaster_HasNoSchedulingCrudFailures(string master)
    {
        var report = new MasterOwnershipValidator().Validate();
        var failures = report.Failures.Where(f => f.MasterEntity == master).ToList();
        Assert.True(
            failures.Count == 0,
            $"{master} ownership failures:\n" + string.Join("\n", failures.Select(f => $"{f.Message} ({f.Path})")));
    }

    [Fact]
    public void ArchitectureOwnershipReport_ListsCatalogMastersFromMatrix()
    {
        var report = new MasterOwnershipValidator().Validate();
        foreach (var master in MasterOwnershipValidator.CatalogOwnedMasters)
            Assert.Contains(master, report.CatalogOwnedMasters);
    }

    [Fact]
    public void ArchitectureOwnershipReport_Markdown_IsNonEmpty()
    {
        var report = new MasterOwnershipValidator().Validate();
        var md = report.ToMarkdown();
        Assert.Contains("# Architecture Ownership Report", md);
        Assert.Contains("Compliant:", md);
    }
}
