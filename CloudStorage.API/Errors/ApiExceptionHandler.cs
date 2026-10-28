using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CloudStorage.API.Errors;

public class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Отменённый клиентом запрос не является ошибкой сервера.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var (statusCode, title, detail) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning("Exception {ExceptionType} mapped to {StatusCode} while processing {Method} {Path}",
                exception.GetType().Name, statusCode, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException or KeyNotFoundException =>
                (StatusCodes.Status404NotFound, "Not Found", "Запрошенный файл не найден."),
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized", "Не удалось определить пользователя по токену."),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "Bad Request", "Некорректный запрос."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Internal Server Error",
                    "Произошла непредвиденная ошибка. Попробуйте позже.")
        };
    }
}