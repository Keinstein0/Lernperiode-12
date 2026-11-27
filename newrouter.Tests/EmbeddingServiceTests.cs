using A2A;
using newrouter.services;

namespace newrouter.Tests;

public class EmbeddingServiceTests
{
    private readonly EmbeddingService _sut = new();

    [Fact]
    public void ComputeTextEmbedding_SameInput_IsDeterministic()
    {
        var v1 = _sut.ComputeTextEmbedding("python code analysis");
        var v2 = _sut.ComputeTextEmbedding("python code analysis");

        Assert.Equal(v1, v2);
    }

    [Fact]
    public void ComputeTextEmbedding_EmptyOrWhitespace_ReturnsZeroVector()
    {
        var v = _sut.ComputeTextEmbedding("   ");
        Assert.All(v, x => Assert.Equal(0f, x));
    }

    [Fact]
    public void CosineSimilarity_IdenticalText_ReturnsOne()
    {
        var v = _sut.ComputeTextEmbedding("git repository diff");
        var sim = _sut.CosineSimilarity(v, v);
        Assert.Equal(1.0, sim, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_DisjointText_ReturnsZero()
    {
        var a = _sut.ComputeTextEmbedding("alpha beta");
        var b = _sut.ComputeTextEmbedding("gamma delta");
        var sim = _sut.CosineSimilarity(a, b);
        Assert.Equal(0.0, sim, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_MismatchedOrEmptyVectors_ReturnsZero()
    {
        Assert.Equal(0.0, _sut.CosineSimilarity([], [1f, 2f]));
        Assert.Equal(0.0, _sut.CosineSimilarity([1f], [1f, 2f]));
    }

    [Fact]
    public void ComputeAgentCardEmbedding_IncludesNameDescriptionAndSkills()
    {
        var card = new AgentCard
        {
            Name = "Coder",
            Description = "Writes C# code",
            Skills = [new AgentSkill { Id = "s1", Name = "Refactoring", Description = "Cleans up code" }]
        };

        var cardVec = _sut.ComputeAgentCardEmbedding(card);
        var queryVec = _sut.ComputeTextEmbedding("refactoring C# code");

        Assert.True(_sut.CosineSimilarity(cardVec, queryVec) > 0.3);
    }
}
