using Abhyanvaya.Application.Common.Interfaces;
using Polly;

namespace Abhyanvaya.Infrastructure.Services
{
    using System.Text.Json;
    using Abhyanvaya.Infrastructure.Resilience;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;

        public RedisCacheService(IDistributedCache cache, IConfiguration config)
        {
            _cache = cache;            
        }

        public async Task<T?> GetAsync<T>(string key)
        {
           
            try
            {
                return await ResiliencePolicies.WrapPolicy.ExecuteAsync(async () =>
                {
                    var data = await _cache.GetStringAsync(key);
                    return data == null ? default : JsonSerializer.Deserialize<T>(data);
                });
            }
            catch
            {
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
          
            try
            {
                await ResiliencePolicies.WrapPolicy.ExecuteAsync(async () =>
                {
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
                    };

                    var json = JsonSerializer.Serialize(value);
                    await _cache.SetStringAsync(key, json, options);
                });
            }
            catch
            {
                // ignore cache failure
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _cache.RemoveAsync(key);
            }
            catch
            {
                // ignore
            }
        }
    }
}
