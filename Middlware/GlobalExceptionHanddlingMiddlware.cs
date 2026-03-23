using System.Net;
using System.Text.Json;
using food_order_system1.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace food_order_system1.Middleware
{
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

        public GlobalExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

public async Task HandleExceptionAsync(HttpContext context, Exception ex)
{ 
     _logger.LogError(ex, ex.Message);
    if (context.Response.HasStarted)
    {
        // can't modify headers, just log
        Console.WriteLine("Response has already started, cannot write exception");
        return;
    }

    context.Response.Clear();
    context.Response.StatusCode = MapToStatusCode(ex);
    context.Response.ContentType = "application/json";
    _logger.LogError(ex, ex.Message);
    var response = new { message = ex.Message };
    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
}

       private int MapToStatusCode(Exception ex) => ex switch
        {
            BusinessException => StatusCodes.Status400BadRequest,
            ArgumentNullException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            DbUpdateException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        private string GetTitle(Exception ex) => ex switch
        {
            BusinessException => ex.Message,
            ArgumentNullException => "Required value was null.",
            ArgumentException => "Invalid request parameters.",
            UnauthorizedAccessException => "You are not allowed to access this resource.",
            KeyNotFoundException => "Requested resource was not found.",
            DbUpdateException => "Database update failed.",
            _ => "Server error"
        };

        private bool IsDevelopment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }
    }
}