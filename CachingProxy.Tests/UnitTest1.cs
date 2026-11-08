using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CachingProxy.Tests;

public class ProxyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProxyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("origin", "https://reqres.in");
        }).CreateClient();
    }

    [Fact]
    public async Task Request_CachesResponse_And_ReturnsHit_OnSecondCall()
    {
        var requestUrl = "/api/users/2";

        var firstResponse = await _client.GetAsync(requestUrl);
        var firstCacheHeader = firstResponse.Headers.GetValues("X-Cache").FirstOrDefault();

        Assert.Equal("MISS", firstCacheHeader);

        var secondResponse = await _client.GetAsync(requestUrl);
        var secondCacheHeader = secondResponse.Headers.GetValues("X-Cache").FirstOrDefault();

        Assert.Equal("HIT", secondCacheHeader);
    }

    [Fact]
    public async Task ClearCacheEndpoint_RemovesData_And_ResetsToMiss()
    {
        var requestUrl = "/api/users/3";

        await _client.GetAsync(requestUrl);

        var clearResponse = await _client.PostAsync("/admin/clear-cache", new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);

        var afterClearResponse = await _client.GetAsync(requestUrl);
        var cacheHeader = afterClearResponse.Headers.GetValues("X-Cache").FirstOrDefault();

        Assert.Equal("MISS", cacheHeader);
    }
}