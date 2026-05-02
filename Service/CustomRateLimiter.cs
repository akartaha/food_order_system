using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using food_order_system1.Modles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace food_order_system1.Service
{
    public interface ICustomRateLimiter
    {

        Task<(bool allowed, int remain, int ReteryAfer)> CustomCheackRateLimit(string user_id);
    
        Task<(bool allowed, int remain, int ReteryAfer)> BucketCheackRateLimit(string user_id);
    }
    public class CustomRateLimiter : ICustomRateLimiter
    {
        private readonly IDatabase _redis;
        public CustomRateLimiter(IConnectionMultiplexer redis)
        {
            _redis = redis.GetDatabase();

        }

          public async Task<(bool allowed, int remain, int ReteryAfer)> BucketCheackRateLimit(string user_id)
        {
              string key = $"Rate_Limit_{user_id}";
              int capacity=10;
              double refillRate=10.0/60.0;
              long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
               
            string LuaScript=@"
                local key = KEYS[1]

                local capacity = tonumber(ARGV[1])
                local refillRate = tonumber(ARGV[2])
                local now = tonumber(ARGV[3])

                -- get bucket from redis
                local data = redis.call('GET', key)

                local tokens
                local lastRefill

                if not data then
                    tokens = capacity
                    lastRefill = now
                else
                    local obj = cjson.decode(data)
                    tokens = obj.tokens
                    lastRefill = obj.lastRefill
                end

                -- refill tokens
                local passedSeconds = math.max(0, now - lastRefill)
                local tokensToAdd = passedSeconds * refillRate
                tokens = math.min(capacity, tokens + tokensToAdd)

                -- check if allowed
                if tokens < 1 then
                    local retryAfter = math.ceil((1 - tokens) / refillRate)
                    return {0, math.floor(tokens), retryAfter}
                end

                -- consume token
                tokens = tokens - 1

                -- save updated bucket
                local newData = cjson.encode({
                    tokens = tokens,
                    lastRefill = now
                })

                redis.call('SET', key, newData, 'EX', 300)

                -- return success
                return {1, math.floor(tokens), 0}
            ";

              var result=(RedisResult[]) await _redis.ScriptEvaluateAsync(
                LuaScript,
                new RedisKey[] {key},
                new RedisValue[] {capacity , refillRate,now} 
              );

              var allowed= (int) result[0] == 1;
              var remain = (long) result[1] ;
               var retryAfter = (long) result[2] ;

               return (allowed, (int)remain,(int) retryAfter);
        }
        public async Task<(bool allowed, int remain, int ReteryAfer)> CustomCheackRateLimit(string user_id)
        {
            string key = $"Rate_Limit_{user_id}";

            // atomic incriment 
            var count = await _redis.StringIncrementAsync(key);

            // first request build expiration 

            if (count == 1)
            {
                await _redis.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
            }
            int limit = 10;
            if (count > limit)
            {
                return (false, 0, 60);
            }
            int remain = limit - (int)count;
            return (true, remain, 60);
        }

    }
}