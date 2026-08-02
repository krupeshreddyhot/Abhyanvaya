namespace Abhyanvaya.Infrastructure.Recognition;

/// <summary>
/// Gallery embeddings must come from the same ONNX recognition model that produces classroom query vectors.
/// Cross-model cosine similarity is near zero and surfaces as 0% / Unassigned in the review UI.
/// </summary>
public static class EmbeddingModelCompatibility
{
    public static string NormalizeModelFileName(string? model) =>
        Path.GetFileName((model ?? string.Empty).Trim());

    public static bool MatchesRuntimeModel(string? embeddingModel, string? runtimeRecognitionModelFile)
    {
        var left = NormalizeModelFileName(embeddingModel);
        var right = NormalizeModelFileName(runtimeRecognitionModelFile);
        if (left.Length == 0 || right.Length == 0)
            return false;

        return left.Equals(right, StringComparison.OrdinalIgnoreCase)
               || Path.GetFileNameWithoutExtension(left)
                   .Equals(Path.GetFileNameWithoutExtension(right), StringComparison.OrdinalIgnoreCase);
    }
}
