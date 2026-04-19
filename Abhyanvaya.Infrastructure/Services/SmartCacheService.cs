using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Services;
using Microsoft.Extensions.Configuration;

public class SmartCacheService : ICacheService
{
    private readonly bool _useRedis;
    private readonly RedisCacheService _redis;
    private readonly MemoryCacheService _memory;

    public SmartCacheService(
        RedisCacheService redis,
        MemoryCacheService memory,
        IConfiguration config)
    {
        _redis = redis;
        _memory = memory;
        _useRedis = config.GetValue<bool>("UseRedis");
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        // ✅ 1. Check Memory FIRST (fast)
        var memoryData = await _memory.GetAsync<T>(key);
        if (memoryData != null)
        {
            Console.WriteLine("[CACHE HIT - MEMORY]");
            return memoryData;
        }

        // ✅ 2. Then Redis (optional)
        if (_useRedis)
        {
            try
            {
                var redisData = await _redis.GetAsync<T>(key);
                if (redisData != null)
                {
                    Console.WriteLine("[CACHE HIT - REDIS]");

                    // 🔥 sync memory for next time
                    await _memory.SetAsync(key, redisData, TimeSpan.FromHours(1));

                    return redisData;
                }
            }
            catch
            {
                Console.WriteLine("[REDIS FAILED]");
            }
        }

        Console.WriteLine("[CACHE MISS]");
        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        // ✅ Always store in memory (fast access)
        await _memory.SetAsync(key, value, expiry);

        // ✅ Then Redis (if enabled)
        if (_useRedis)
        {
            try
            {
                await _redis.SetAsync(key, value, expiry);
            }
            catch
            {
                Console.WriteLine("[REDIS SET FAILED]");
            }
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _redis.RemoveAsync(key);
        }
        catch
        {
            // ignore
        }

        await _memory.RemoveAsync(key);
    }
}