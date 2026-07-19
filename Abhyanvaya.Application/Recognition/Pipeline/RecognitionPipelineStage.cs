namespace Abhyanvaya.Application.Recognition.Pipeline;

/// <summary>Canonical stage identity for recognition pipeline manifests (AI20.PHASE2.3).</summary>
public enum RecognitionPipelineStage
{
    Embedding = 0,
    CandidateRetrieval = 1,
    VectorSearch = 2,
    Similarity = 3,
    Decision = 4,
    Persistence = 5,
}
