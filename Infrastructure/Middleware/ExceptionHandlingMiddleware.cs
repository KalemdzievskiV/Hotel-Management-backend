using System.Net;
using System.Text.Json;
using HotelManagement.Infrastructure.Exceptions;
using HotelManagement.Models;

namespace HotelManagement.Infrastructure.Middleware;

/// <summary>
/// Turns exceptions into consistent ApiResponse errors. Expected failures (broken business rules,
/// bad input, missing records) keep their message; anything else is a bug and returns a generic 500.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            PlanLimitException => HttpStatusCode.PaymentRequired,
            BusinessRuleException => HttpStatusCode.BadRequest,
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            _ => HttpStatusCode.InternalServerError
        };

        var isUnexpected = statusCode == HttpStatusCode.InternalServerError;
        if (isUnexpected)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogWarning("Request failed ({StatusCode}): {Message}", (int)statusCode, exception.Message);

        // Expected failures carry a user-facing message; for bugs only show details in development
        var isDevelopment = _environment.IsDevelopment();
        var message = !isUnexpected || isDevelopment
            ? exception.Message
            : "An error occurred processing your request.";
        var errors = isUnexpected && isDevelopment && exception.StackTrace != null
            ? new List<string> { exception.StackTrace }
            : null;

        var response = ApiResponse<object>.ErrorResponse(message, errors, (int)statusCode);
        if (exception is PlanLimitException limit)
        {
            response.Data = new
            {
                code = "plan_limit",
                limit = limit.Limit,
                currentPlan = limit.CurrentPlan.ToString(),
                allowed = limit.Allowed,
                upgradeTo = limit.UpgradeTo?.ToString()
            };
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
