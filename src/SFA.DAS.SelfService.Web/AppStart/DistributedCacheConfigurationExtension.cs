using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFA.DAS.SelfService.Core.Configuration;
using StackExchange.Redis;

namespace SFA.DAS.SelfService.Web.AppStart
{
    public static class DistributedCacheConfigurationExtension
    {
        public static void AddDistributedCache(this IServiceCollection services, IConfiguration configuration, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                services.AddDistributedMemoryCache();
                services.AddDataProtection()
                    .SetApplicationName(ReleaseConstants.ApplicationName);
            }
            else
            {
                var redisConnectionString = configuration["RedisConnectionString"];

                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = $"{redisConnectionString},DefaultDatabase=1";
                });

                var redis = ConnectionMultiplexer.Connect($"{redisConnectionString},DefaultDatabase=0");
                services.AddDataProtection()
                    .SetApplicationName(ReleaseConstants.ApplicationName)
                    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
            }
        }
    }
}