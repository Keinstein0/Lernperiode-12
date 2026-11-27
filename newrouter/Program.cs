using newrouter.routes.httpNodeLifecycle;
using newrouter.services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddHttpClient<A2AProxyService>();
builder.Services.AddSingleton<NodeRegistryService>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<TaskRoutingService>();
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
