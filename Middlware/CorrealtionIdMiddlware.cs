using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Context;

namespace food_order_system1.Middlware
{
    public class CorrelationIdMiddleware
    {
          private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    { if (context == null)
        throw new ArgumentNullException(nameof(context));

    // 1. Try get correlation id safely
    var correlationId = context.Request?.Headers?["X-Correlation-Id"].FirstOrDefault();

        // 2. If not, create one
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = "unonymous";
        }

        // 3. Add it to response headers (so client can see it)
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // 4. Push into Serilog context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
    }
}