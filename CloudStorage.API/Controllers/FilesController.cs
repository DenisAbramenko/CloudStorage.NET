using System.Security.Claims;
using CloudStorage.API.DTOs;
using CloudStorage.API.Errors;
using CloudStorage.API.Services;
using CloudStorage.API.Services.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudStorage.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;
    private readonly IStoredFileMapper _storedFileMapper;

    public FilesController(IFileStorageService fileStorageService, IStoredFileMapper storedFileMapper)
    {
        _fileStorageService = fileStorageService;
        _storedFileMapper = storedFileMapper;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "Файл не выбран или он пуст.");
            return ValidationProblem(ModelState);
        }

        var userId = GetCurrentUserId();
        var storedFile = await _fileStorageService.UploadAsync(file, userId, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, _storedFileMapper.ToDto(storedFile));
    }

    [HttpGet]
    public async Task<IActionResult> GetAllFiles([FromQuery] FilesQueryParameters query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = GetCurrentUserId();
        var files = await _fileStorageService.GetFilesPageAsync(userId, query, cancellationToken);
        return Ok(_storedFileMapper.ToDtoPage(files));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var file = await _fileStorageService.DownloadAsync(userId, id, cancellationToken);
        if (file == null) return NotFound();
        return new FileStreamResult(
            file.Content,
            file.ContentType)
        {
            FileDownloadName = file.FileName
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _fileStorageService.DeleteById(userId, id, cancellationToken);
        if (!result) return NotFound();
        return NoContent();
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new MissingUserIdClaimException();
    }
}