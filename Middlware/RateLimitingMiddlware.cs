using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using food_order_system1.Service;

namespace food_order_system1.Middlware
{
    public class RateLimitingMiddlware
    {
              private readonly RequestDelegate _next;
            private readonly ILogger<RateLimitingMiddlware> _logger;
    public RateLimitingMiddlware(RequestDelegate next , ILogger<RateLimitingMiddlware> logger)
    {
        _next = next;
        _logger = logger;
    }

     public async Task InvokeAsync(HttpContext context , ICustomRateLimiter limiter)
        {
            var user_id = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if(user_id == null)
            {
                user_id=context.Connection.RemoteIpAddress?.ToString() ?? "anonimous";
            }
  

             var (allowed , remaining,retryAfter)=await limiter.BucketCheackRateLimit(user_id);


        context.Response.Headers["X-RateLimit-Limit"] = "10";
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
       
            
         if(! allowed)
            {
                 context.Response.Headers["Retry-After"] = retryAfter.ToString();  
                _logger.LogWarning("too many request by user {user_id}, retry after {retryAfter}, seconds",user_id,retryAfter.ToString());
              
                context.Response.StatusCode=429;
                await context.Response.WriteAsJsonAsync(new
                {
                  Message= "too many requests",
                 RetryAfte=retryAfter
                }
                   
                );
                
              return;
            }
           await _next(context) ;
            
        }


        
    }
}