using CloudStorage.API.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CloudStorage.Tests.Unit;

public class ApiExceptionHandlerTests
{
    private readonly FakeProblemDetailsService _problemDetails = new();

    private ApiExceptionHandler CreateHandler()
    {
        return new ApiExceptionHandler(_problemDetails, NullLogger<ApiExceptionHandler>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/files";
        return context;
    }

    // Сценарий: в токене нет идентификатора пользователя — это именно 401.
    [Fact]
    public async Task TryHandleAsync_MissingUserIdClaim_Writes401WithUnauthorizedMessage()
    {
        var context = CreateHttpContext();

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new MissingUserIdClaimException(),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(_problemDetails.LastContext!.ProblemDetails);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
        Assert.Equal("Unauthorized", problem.Title);
        Assert.Equal("Не удалось определить пользователя по токену.", problem.Detail);
    }

    // Сценарий: ошибка доступа к диску — это серверная ошибка, а не проблема авторизации.
    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_Writes500WithGenericMessage()
    {
        var context = CreateHttpContext();

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new UnauthorizedAccessException("Access to the path 'C:\\uploads\\key' is denied."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(_problemDetails.LastContext!.ProblemDetails);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
        Assert.Equal("Internal Server Error", problem.Title);
        Assert.Equal("Произошла непредвиденная ошибка. Попробуйте позже.", problem.Detail);
        Assert.DoesNotContain("uploads", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? LastContext { get; private set; }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            LastContext = context;
            return ValueTask.FromResult(true);
        }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            LastContext = context;
            return ValueTask.CompletedTask;
        }
    }
}
