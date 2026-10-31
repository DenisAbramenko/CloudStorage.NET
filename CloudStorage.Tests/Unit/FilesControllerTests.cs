using System.Reflection;
using System.Security.Claims;
using CloudStorage.API.Controllers;
using CloudStorage.API.DTOs;
using CloudStorage.API.Models;
using CloudStorage.API.Services.Mapping;
using CloudStorage.Tests.Unit.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CloudStorage.Tests.Unit;

public class FilesControllerTests
{
    private readonly FakeFileStorageService _storage = new();

    private FilesController CreateController(ClaimsPrincipal? user = null)
    {
        var services = new ServiceCollection();
        services.AddControllers();
        services.AddProblemDetails();
        services.AddLogging();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = user ?? CreateUser("user-1")
        };

        return new FilesController(_storage, new StoredFileMapper())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));
    }

    private static IFormFile CreateFile(int length = 5)
    {
        return new FakeFormFile("note.txt", "text/plain", length);
    }

    // Сценарий 1: нельзя загрузить пустой файл.
    [Fact]
    public async Task UploadFile_EmptyFile_Returns400WithValidationProblem()
    {
        var controller = CreateController();

        var result = await controller.UploadFile(CreateFile(length: 0), CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.True(problem.Errors.ContainsKey("file"));
        Assert.Equal(0, _storage.UploadCalls);
    }

    [Fact]
    public async Task UploadFile_NullFile_Returns400WithValidationProblem()
    {
        var controller = CreateController();

        var result = await controller.UploadFile(null, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal(0, _storage.UploadCalls);
    }

    [Fact]
    public async Task UploadFile_ValidFile_Returns201WithDto()
    {
        var storedFile = new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = "note.txt",
            StorageKey = "key",
            ContentType = "text/plain",
            Size = 5,
            UserId = "user-1"
        };
        _storage.UploadResult = storedFile;
        var controller = CreateController();

        var result = await controller.UploadFile(CreateFile(), CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var dto = Assert.IsType<StoredFileDto>(objectResult.Value);
        Assert.Equal(storedFile.Id, dto.Id);
        Assert.Equal("user-1", _storage.LastUserId);
    }

    [Fact]
    public async Task UploadFile_IsLimitedTo100Mb()
    {
        // Лимит задан атрибутом на действии; тест страхует контракт, пока правило не перенесено в Application.
        var method = typeof(FilesController).GetMethod(nameof(FilesController.UploadFile))!;
        var attribute = Assert.Single(method.GetCustomAttributes<RequestSizeLimitAttribute>());

        Assert.Equal(100L * 1024 * 1024, ((IRequestSizeLimitMetadata)attribute).MaxRequestBodySize);
    }

    [Fact]
    public async Task DownloadFile_UnknownFile_Returns404()
    {
        _storage.DownloadResult = null;
        var controller = CreateController();

        var result = await controller.DownloadFile(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteFile_UnknownOrForeignFile_Returns404()
    {
        _storage.DeleteResult = false;
        var controller = CreateController();

        var result = await controller.DeleteFile(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteFile_OwnFile_Returns204()
    {
        _storage.DeleteResult = true;
        var controller = CreateController();

        var result = await controller.DeleteFile(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task GetAllFiles_TakesUserIdFromClaims()
    {
        var controller = CreateController(CreateUser("user-42"));

        var result = await controller.GetAllFiles(new FilesQueryParameters(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("user-42", _storage.LastUserId);
    }

    [Fact]
    public async Task GetAllFiles_WithoutUserIdClaim_ThrowsUnauthorizedAccessException()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => controller.GetAllFiles(new FilesQueryParameters(), CancellationToken.None));
    }
}