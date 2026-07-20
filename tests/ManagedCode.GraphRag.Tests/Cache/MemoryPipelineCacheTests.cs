using System.Collections.Concurrent;
using System.Reflection;
using GraphRag.Cache;
using Microsoft.Extensions.Caching.Memory;

namespace ManagedCode.GraphRag.Tests.Cache;

public sealed class MemoryPipelineCacheTests
{
    [Test]
    public async Task SetAndGet_ReturnsStoredValue()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryPipelineCache(memoryCache);

        await cache.SetAsync("foo", 42);
        var value = await cache.GetAsync("foo");

        await Assert.That(value).IsEqualTo(42);
        await Assert.That(await cache.HasAsync("foo")).IsTrue();
    }

    [Test]
    public async Task ClearAsync_RemovesEntries()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryPipelineCache(memoryCache);

        await cache.SetAsync("foo", "bar");
        await cache.ClearAsync();

        await Assert.That(await cache.HasAsync("foo")).IsFalse();
    }

    [Test]
    public async Task ChildCache_IsolatedFromParent()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var parent = new MemoryPipelineCache(memoryCache);
        var child = parent.CreateChild("child");

        await child.SetAsync("value", "child");

        await Assert.That(await parent.HasAsync("value")).IsFalse();
        await Assert.That(await child.GetAsync("value")).IsEqualTo("child");
    }

    [Test]
    public async Task ClearAsync_RemovesChildEntries()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var parent = new MemoryPipelineCache(memoryCache);
        var child = parent.CreateChild("child");

        await parent.SetAsync("parentValue", "parent");
        await child.SetAsync("childValue", "child");

        await parent.ClearAsync();

        await Assert.That(await parent.HasAsync("parentValue")).IsFalse();
        await Assert.That(await child.HasAsync("childValue")).IsFalse();
    }

    [Test]
    public async Task DeleteAsync_RemovesTrackedKeyEvenWithDebugData()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryPipelineCache(memoryCache);

        await cache.SetAsync("debug", 123, new Dictionary<string, object?> { ["token"] = "value" });
        var keys = GetTrackedKeys(cache);
        await Assert.That(keys.Keys.Any(key => key.EndsWith(":debug", StringComparison.Ordinal))).IsTrue();

        await cache.DeleteAsync("debug");

        await Assert.That(GetTrackedKeys(cache).Keys.Any(key => key.EndsWith(":debug", StringComparison.Ordinal))).IsFalse();
        await Assert.That(await cache.HasAsync("debug")).IsFalse();
    }

    [Test]
    public async Task CreateChild_AfterParentWrites_StillClearsChildEntries()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var parent = new MemoryPipelineCache(memoryCache);

        await parent.SetAsync("root", "root");
        var child = parent.CreateChild("later-child");
        await child.SetAsync("inner", "child");

        await parent.ClearAsync();

        await Assert.That(await parent.HasAsync("root")).IsFalse();
        await Assert.That(await child.HasAsync("inner")).IsFalse();
    }

    private static ConcurrentDictionary<string, byte> GetTrackedKeys(MemoryPipelineCache cache)
    {
        var field = typeof(MemoryPipelineCache)
            .GetField("_keys", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not access keys field.");

        return (ConcurrentDictionary<string, byte>)field.GetValue(cache)!;
    }
}
