using CachingProxy.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var originUrl = builder.Configuration.GetValue<string>("origin");
var port = builder.Configuration.GetValue<int>("port", 3000);
var clearCache = builder.Configuration.GetValue<bool>("clear-cache");

if (string.IsNullOrEmpty(originUrl) && !clearCache)
{
    Console.WriteLine("Usage: dotnet run --port <number> --origin <url>\n       dotnet run --clear-cache");
    return;
}

if (clearCache)
{
    using var client = new HttpClient();
    var response = await client.PostAsync($"http://localhost:{port}/admin/clear-cache", null);
    Console.WriteLine(response.IsSuccessStatusCode ? "Cache cleared." : "Failed to clear cache.");
    return;
}

builder.WebHost.UseUrls($"http://localhost:{port}");

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("ProxyClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 100
    });

var app = builder.Build();

app.MapProxyRoutes(originUrl!);

app.Run();
public partial class Program { }