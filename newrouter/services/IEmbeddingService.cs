using A2A;

namespace newrouter.services;

public interface IEmbeddingService
{
    float[] ComputeAgentCardEmbedding(AgentCard card);
    float[] ComputeTextEmbedding(string text);
    double CosineSimilarity(float[] a, float[] b);
}
