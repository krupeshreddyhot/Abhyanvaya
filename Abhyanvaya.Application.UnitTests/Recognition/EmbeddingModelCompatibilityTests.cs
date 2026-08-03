using Abhyanvaya.Infrastructure.Recognition;

namespace Abhyanvaya.Application.UnitTests.Recognition;

public sealed class EmbeddingModelCompatibilityTests
{
    [Theory]
    [InlineData("w600k_r50.onnx", "w600k_r50.onnx", true)]
    [InlineData("w600k_r50", "w600k_r50.onnx", true)]
    [InlineData("e608k_150.onnx", "w600k_r50.onnx", false)]
    [InlineData("e608k_150.onnx", "e608k_150.onnx", true)]
    [InlineData("", "w600k_r50.onnx", false)]
    public void MatchesRuntimeModel_ComparesNormalizedFileNames(string embeddingModel, string runtime, bool expected) =>
        Assert.Equal(expected, EmbeddingModelCompatibility.MatchesRuntimeModel(embeddingModel, runtime));
}
