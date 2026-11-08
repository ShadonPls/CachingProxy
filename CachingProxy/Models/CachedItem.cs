namespace CachingProxy.Models;

public class CachedItem
{
    public required byte[] Bytes { get; set; }
    public required Dictionary<string, string[]> Headers { get; set; }
}