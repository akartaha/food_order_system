using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Serilog.Context;

namespace food_order_system1.Middlware
{
    public class UserEnrichmentMiddleware
    {
        private readonly RequestDelegate _next;
    public UserEnrichmentMiddleware(RequestDelegate next)
    {
        
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        using (LogContext.PushProperty("UserId", userId ?? "Anonymous"))
        {
            await _next(context);
        }
    }
}}