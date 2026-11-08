namespace CachingProxy.Utils;

public static class HttpUtils
{
    private static readonly string[] HopByHopHeaders =
    {
        "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade",
        "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer"
    };

    public static bool IsHopByHopHeader(string header)
    {
        return HopByHopHeaders.Contains(header, StringComparer.OrdinalIgnoreCase);
    }
}