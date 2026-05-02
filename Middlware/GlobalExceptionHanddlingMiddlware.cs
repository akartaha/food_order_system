using System.Net;
using System.Text.Json;
using food_order_system1.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog.Context;

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

        var userId = context.Items["UserId"]?.ToString();
        var correlationId = context.Items["CorrelationId"]?.ToString();
        
        _logger.LogError(ex,
            "Unhandled exception. Path: {Path}, Method: {Method} by User: {UserId}, CorrelationId: {CorrelationId}",
            context.Request.Path,
            context.Request.Method,
            userId,
            correlationId);

        await HandleExceptionAsync(context, ex);
    }
}

public async Task HandleExceptionAsync(HttpContext context, Exception ex)
{
    if (context.Response.HasStarted)
    {
        _logger.LogWarning("Response already started. Exception handling skipped.");
        return;
    }

    context.Response.Clear();
    context.Response.StatusCode = MapToStatusCode(ex);
    context.Response.ContentType = "application/json";

    var response = new
    {
        message = GetTitle(ex),
        detail = IsDevelopment() ? ex.Message : null
    };

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