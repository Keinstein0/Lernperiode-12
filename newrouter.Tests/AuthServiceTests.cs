using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using newrouter.services;

namespace newrouter.Tests;

public class AuthServiceTests
{
    private static AuthService CreateAuthService(
        HttpMessageHandler? handler = null,
        string? authHeader = null,
        int tokenLifetimeMinutes = 60)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        var context = new DefaultHttpContext();
        if (authHeader is not null)
        {
            context.Request.Headers.Authorization = authHeader;
        }

        var accessor = new HttpContextAccessor { HttpContext = context };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthNode:BaseUrl"] = "http://auth.local",
                ["AuthNode:ServiceToken"] = "svc-token",
                ["RouterAuth:SigningKey"] = "test-signing-key-32-chars-minimum!",
                ["RouterAuth:TokenLifetimeMinutes"] = tokenLifetimeMinutes.ToString()
            })
            .Build();

        return new AuthService(http, accessor, config);
    }

    [Fact]
    public void GetCallerTokenOrThrow_ValidBearer_ReturnsToken()
    {
        var svc = CreateAuthService(authHeader: "Bearer my-caller-jwt");
        Assert.Equal("my-caller-jwt", svc.GetCallerTokenOrThrow());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic abc123")]
    public void GetCallerTokenOrThrow_MissingOrInvalid_ThrowsUnauthorized(string? header)
    {
        var svc = CreateAuthService(authHeader: header);
        Assert.Throws<UnauthorizedAccessException>(() => svc.GetCallerTokenOrThrow());
    }

    [Fact]
    public void IssueAndValidateRouterNodeToken_ValidToken_Succeeds()
    {
        var svc = CreateAuthService();
        var (token, _) = svc.IssueRouterNodeToken("b2-1");

        Assert.True(svc.ValidateRouterNodeToken(token, "b2-1"));
        Assert.False(svc.ValidateRouterNodeToken(token, "b2-2"));
        Assert.False(svc.ValidateRouterNodeToken(token + "tamper", "b2-1"));
    }

    [Fact]
    public void ValidateRouterNodeToken_ExpiredToken_ReturnsFalse()
    {
        var svc = CreateAuthService(tokenLifetimeMinutes: -5);
        var (token, _) = svc.IssueRouterNodeToken("b2-1");

        Assert.False(svc.ValidateRouterNodeToken(token, "b2-1"));
    }
}
