using Microsoft.Extensions.Options;
using SharedLib.Options;

namespace ChatAPI.Middlewares;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<ChatApi> _configuration;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ChatApi> configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        string? apiKey = context.Request.Headers["ApiKey"];

        if (string.IsNullOrEmpty(apiKey) || !IsValidApiKey(apiKey))
        {
            context.Response.StatusCode = 401; // Unauthorized
            await context.Response.WriteAsync("Invalid API key");
            return;
        }

        await _next(context);
    }

    private bool IsValidApiKey(string apiKey)
    {
        string? apiKeyConfigured = _configuration.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKeyConfigured))
            throw new Exception("API Key is not configured");

        return string.Equals(apiKeyConfigured, apiKey);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}