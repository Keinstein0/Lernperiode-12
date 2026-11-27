using System.Collections.Concurrent;
using System.ComponentModel;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ItemService>();
builder.Services.AddAuthorization();

builder.Services
    .AddMcpServer()
    .AddAuthorizationFilters()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseAuthorization();

// Only the MCP endpoint is mapped—no redundant /api/items REST routes
app.MapMcp("/mcp").RequireAuthorization();

app.Run();

public sealed record Item(Guid Id, string Name, int Quantity);

public sealed class ItemService
{
    private readonly ConcurrentDictionary<Guid, Item> _items = new();

    public IReadOnlyCollection<Item> List() => _items.Values.ToArray();
    public Item? Get(Guid id) => _items.GetValueOrDefault(id);

    public Item Create(string name, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        var item = new Item(Guid.NewGuid(), name.Trim(), quantity);
        _items[item.Id] = item;
        return item;
    }

    public Item? Update(Guid id, string name, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        if (!_items.ContainsKey(id)) return null;
        var updated = new Item(id, name.Trim(), quantity);
        _items[id] = updated;
        return updated;
    }

    public bool Delete(Guid id) => _items.TryRemove(id, out _);
}

[McpServerToolType]
public sealed class ItemTools
{
    [McpServerTool, Description("Lists all items.")]
    public static IReadOnlyCollection<Item> ListItems(ItemService service) => service.List();

    [McpServerTool, Description("Gets a single item by ID.")]
    public static Item? GetItem(ItemService service, Guid id) => service.Get(id);

    [McpServerTool, Description("Creates a new item with a name and quantity.")]
    public static Item CreateItem(ItemService service, string name, int quantity) =>
        service.Create(name, quantity);

    [McpServerTool, Description("Updates an existing item by ID.")]
    public static Item? UpdateItem(ItemService service, Guid id, string name, int quantity) =>
        service.Update(id, name, quantity);

    [McpServerTool, Description("Deletes an item by ID.")]
    public static bool DeleteItem(ItemService service, Guid id) => service.Delete(id);
}