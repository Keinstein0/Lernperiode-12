using System.Security.Cryptography;
using System.Text;
using A2A;

namespace newrouter.services;

public sealed class EmbeddingService
{
    private const int Dimensions = 128;

    public float[] ComputeAgentCardEmbedding(AgentCard card)
    {
        var sb = new StringBuilder();
        sb.Append(card.Name).Append(' ').Append(card.Description);
        if (card.Skills is not null)
        {
            foreach (var skill in card.Skills)
            {
                sb.Append(' ').Append(skill.Name).Append(' ').Append(skill.Description);
            }
        }

        return ComputeTextEmbedding(sb.ToString());
    }

    public float[] ComputeTextEmbedding(string text)
    {
        var vector = new float[Dimensions];
        if (string.IsNullOrWhiteSpace(text))
        {
            return vector;
        }

        var tokens = text.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var bucket = BitConverter.ToUInt32(hash, 0) % Dimensions;
            var sign = (hash[4] & 1) == 0 ? 1f : -1f;
            vector[bucket] += sign;
        }

        Normalize(vector);
        return vector;
    }

    public double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0d;
        }

        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0d ? 0d : dot / denom;
    }

    private static void Normalize(float[] vector)
    {
        double sumSq = 0;
        for (var i = 0; i < vector.Length; i++)
        {
            sumSq += vector[i] * vector[i];
        }

        if (sumSq <= 0) return;
        var norm = (float)Math.Sqrt(sumSq);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }
}
