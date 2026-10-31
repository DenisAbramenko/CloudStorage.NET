using System.Reflection;
using System.Security.Claims;
using CloudStorage.API.Controllers;
using CloudStorage.API.DTOs;
using CloudStorage.API.Errors;
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
    public async Task GetAllFiles_ValidQuery_ReturnsPagedResultWithRequestedParameters()
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
        _storage.PageResult = new PagedResult<StoredFile>([storedFile], Total: 7, Page: 2, PageSize: 5);
        var query = new FilesQueryParameters
        {
            Page = 2,
            PageSize = 5,
            SortBy = FileSortField.Name,
            SortDirection = FileSortDirection.Asc
        };
        var controller = CreateController();

        var result = await controller.GetAllFiles(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<StoredFileDto>>(ok.Value);
        Assert.Equal(7, page.Total);
        Assert.Equal(2, page.Page);
        Assert.Equal(5, page.PageSize);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(storedFile.Id, Assert.Single(page.Items).Id);
        Assert.Same(query, _storage.LastQuery);
    }

    [Fact]
    public async Task GetAllFiles_EmptyPage_ReturnsEmptyItems()
    {
        _storage.PageResult = new PagedResult<StoredFile>([], 0, 1, FilesQueryParameters.DefaultPageSize);
        var controller = CreateController();

        var result = await controller.GetAllFiles(new FilesQueryParameters(), CancellationToken.None);

        var page = Assert.IsType<PagedResult<StoredFileDto>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
        Assert.Equal(0, page.TotalPages);
    }

    [Fact]
    public async Task GetAllFiles_InvalidModelState_Returns400WithoutServiceCall()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError(
            nameof(FilesQueryParameters.PageSize),
            "недопустимый размер страницы");

        var result = await controller.GetAllFiles(
            new FilesQueryParameters { PageSize = 101 },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Null(_storage.LastUserId);
    }

    [Fact]
    public async Task GetAllFiles_WithoutUserIdClaim_ThrowsMissingUserIdClaimException()
    {
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity()));

        await Assert.ThrowsAsync<MissingUserIdClaimException>(
            () => controller.GetAllFiles(new FilesQueryParameters(), CancellationToken.None));
        Assert.Null(_storage.LastUserId);
    }
}