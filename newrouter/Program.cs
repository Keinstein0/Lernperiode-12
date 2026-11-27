using newrouter.routes.httpNodeLifecycle;
using newrouter.services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IAuthService, AuthService>();
builder.Services.AddHttpClient<IA2AProxyService, A2AProxyService>();
builder.Services.AddSingleton<INodeRegistryService, NodeRegistryService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<ITaskRoutingService, TaskRoutingService>();
builder.Services.AddAuthorization();

builder.Services
    .AddMcpServer()
    .AddAuthorizationFilters()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseAuthorization();

app.MapNodeLifecycleEndpoints();
app.MapMcp("/mcp");

app.Run();

public partial class Program { }
