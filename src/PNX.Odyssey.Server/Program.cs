using Pnx.Odyssey.Server;

string pluginKey = ResolvePluginKey();
string databasePath = Environment.GetEnvironmentVariable("ODYSSEY_DB")
    ?? Path.Combine(AppContext.BaseDirectory, "odyssey.db");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.AddSingleton(new StateStore(databasePath));
builder.Services.AddSingleton(provider => new OdysseyGateway(
    provider.GetRequiredService<StateStore>(),
    pluginKey,
    provider.GetRequiredService<ILogger<OdysseyGateway>>()));

WebApplication app = builder.Build();
app.UseWebSockets();
app.MapGet("/health", () => Results.Text("ok"));
app.Map("/odyssey", async (HttpContext context, OdysseyGateway gateway) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await gateway.Run(socket, context.RequestAborted);
});

app.Run();

static string ResolvePluginKey()
{
    string? inline = Environment.GetEnvironmentVariable("ODYSSEY_PLUGIN_KEY");
    if (!string.IsNullOrWhiteSpace(inline))
        return inline.Trim();

    string? file = Environment.GetEnvironmentVariable("ODYSSEY_PLUGIN_KEY_FILE");
    if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
    {
        string key = File.ReadAllText(file).Trim();
        if (key.Length > 0)
            return key;
    }

    throw new InvalidOperationException("Set ODYSSEY_PLUGIN_KEY or ODYSSEY_PLUGIN_KEY_FILE before starting the server.");
}
