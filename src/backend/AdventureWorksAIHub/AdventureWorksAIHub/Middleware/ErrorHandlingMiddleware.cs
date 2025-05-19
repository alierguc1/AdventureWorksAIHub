using System.Net;
using System.Text.Json;

namespace AdventureWorksAIHub.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            _logger.LogError(exception, "An unhandled exception occurred");

            var statusCode = GetStatusCode(exception);
            var response = new
            {
                status = statusCode,
                message = GetErrorMessage(exception, statusCode),
                detail = GetErrorDetail(exception, context),
                traceId = context.TraceIdentifier
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(response, options);
            await context.Response.WriteAsync(json);
        }

        private int GetStatusCode(Exception exception)
        {
            // Map exception types to HTTP status codes
            return exception switch
            {
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                InvalidOperationException => (int)HttpStatusCode.BadRequest,
                // Add other exception types as needed
                _ => (int)HttpStatusCode.InternalServerError
            };
        }

        private string GetErrorMessage(Exception exception, int statusCode)
        {
            // Return user-friendly error messages
            return statusCode switch
            {
                400 => "The request was invalid.",
                401 => "You are not authorized to access this resource.",
                403 => "You do not have permission to access this resource.",
                404 => "The requested resource was not found.",
                500 => "An internal server error occurred.",
                _ => "An error occurred processing your request."
            };
        }

        private object GetErrorDetail(Exception exception, HttpContext context)
        {
            // In development mode, return the exception details
            // In production, return a generic message
            if (context.RequestServices.GetService(typeof(Microsoft.AspNetCore.Hosting.IWebHostEnvironment)) is Microsoft.AspNetCore.Hosting.IWebHostEnvironment env
                && env.EnvironmentName == "Development")
            {
                return new
                {
                    ExceptionType = exception.GetType().Name,
                    ExceptionMessage = exception.Message,
                    StackTrace = exception.StackTrace
                };
            }

            return null;
        }
    }
}