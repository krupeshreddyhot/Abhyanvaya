namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// Serializes Prompt 21 / 21A architecture guard tests that share the compliance snapshot path
/// and UI scan. Prevents intermittent Failed:1 / Passed:28 under parallel --no-build runs.
/// </summary>
[CollectionDefinition("AI29.1D.ArchitectureGuard", DisableParallelization = true)]
public sealed class Ai291DArchitectureGuardTestCollection;
