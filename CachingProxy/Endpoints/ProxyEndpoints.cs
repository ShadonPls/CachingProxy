using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using CachingProxy.Models;
using CachingProxy.Utils;

namespace CachingProxy.Endpoints;

public static class ProxyEndpoints
{
    private const int MaxCacheSizeBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new();

    public static void MapProxyRoutes(this IEndpointRouteBuilder app, string originUrl)
    {
        app.MapFallback("{**catchAll}", async (HttpContext context, IMemoryCache cache, IHttpClientFactory clientFactory) =>
        {
            var requestPath = context.Request.Path + context.Request.QueryString;
            var cacheKey = $"{context.Request.Method}_{requestPath}";

            async Task WriteCachedResponse(CachedItem item)
            {
                context.Response.Headers["X-Cache"] = "HIT";
                foreach (var header in item.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
                await context.Response.Body.WriteAsync(item.Bytes);
            }

            if (cache.TryGetValue(cacheKey, out CachedItem? cachedItem))
            {
                await WriteCachedResponse(cachedItem!);
                return;
            }

            var myLock = KeyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await myLock.WaitAsync();
            try
            {
                if (cache.TryGetValue(cacheKey, out cachedItem))
                {
                    await WriteCachedResponse(cachedItem!);
                    return;
                }

                var httpClient = clientFactory.CreateClient("ProxyClient");
                using var originRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), originUrl + requestPath);

                foreach (var header in context.Request.Headers)
                {
                    if (!HttpUtils.IsHopByHopHeader(header.Key) && !header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    {
                        originRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }

                using var originResponse = await httpClient.SendAsync(originRequest, HttpCompletionOption.ResponseHeadersRead);

                context.Response.StatusCode = (int)originResponse.StatusCode;
                context.Response.Headers["X-Cache"] = "MISS";

                var headersToCache = new Dictionary<string, string[]>();
                foreach (var header in originResponse.Headers.Concat(originResponse.Content.Headers))
                {
                    if (!HttpUtils.IsHopByHopHeader(header.Key))
                    {
                        var headerValues = header.Value.ToArray();
                        context.Response.Headers[header.Key] = headerValues;
                        headersToCache[header.Key] = headerValues;
                    }
                }

                var contentLength = originResponse.Content.Headers.ContentLength;
                if (contentLength > MaxCacheSizeBytes || !originResponse.IsSuccessStatusCode)
                {
                    await originResponse.Content.CopyToAsync(context.Response.Body);
                    return;
                }

                var bytes = await originResponse.Content.ReadAsByteArrayAsync();
                var itemToCache = new CachedItem { Bytes = bytes, Headers = headersToCache };

                cache.Set(cacheKey, itemToCache, CacheTtl);
                await context.Response.Body.WriteAsync(bytes);
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Proxy");
                logger.LogError(ex, "Proxy request failed");
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                await context.Response.WriteAsync("Bad Gateway");
            }
            finally
            {
                myLock.Release();
            }
        });

        app.MapPost("/admin/clear-cache", ([Microsoft.AspNetCore.Mvc.FromServices] IMemoryCache cache) =>
        {
            if (cache is MemoryCache memoryCache) memoryCache.Compact(1.0);
            return Results.Ok();
        });
    }
}