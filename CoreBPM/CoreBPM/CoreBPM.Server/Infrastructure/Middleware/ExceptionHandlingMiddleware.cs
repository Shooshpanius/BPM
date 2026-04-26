using System.Text.Json;
using CoreBPM.Server.Exceptions;

namespace CoreBPM.Server.Infrastructure.Middleware;

/// <summary>Middleware для единообразной обработки исключений.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Обрабатывает запрос и перехватывает исключения.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, ex.Message, null);
        }
        catch (Exceptions.ValidationException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, ex.Message, ex.Details);
        }
        catch (ForbiddenException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, ex.Message, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необработанное исключение: {Message}", ex.Message);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка сервера", null);
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string error,
        IReadOnlyList<string>? details)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error,
            details = details ?? Array.Empty<string>()
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
