using CloudStorage.API.DTOs;
using CloudStorage.API.Models;
using CloudStorage.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CloudStorage.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _fileStorageService;

    public FilesController(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Файл не выбран или он пуст." });

        var storedFile = await _fileStorageService.UploadAsync(file, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ToDto(storedFile));
    }

    [HttpGet]
    public async Task<IActionResult> GetAllFiles(CancellationToken cancellationToken)
    {
        var files = await _fileStorageService.GetAllFiles(cancellationToken);
        var result = files.Select(ToDto);
        return Ok(result);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken cancellationToken)
    {
        var file = await _fileStorageService.DownloadAsync(id, cancellationToken);
        if (file == null) return NotFound();
        return new FileStreamResult(
            file.Content,
            file.ContentType)
        {
            FileDownloadName = file.FileName
        };
    }

    private StoredFileDto ToDto(StoredFile file)
    {
        return new StoredFileDto
        {
            Id = file.Id,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Size = file.Size,
            CreatedAtUtc = file.CreatedAtUtc
        };
    }
}