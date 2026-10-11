using CloudStorage.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CloudStorage.API.Controllers;

[ApiController]
[Route("api/files")]
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
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Файл не выбран или он пуст." });
        }

        var storedFile  = await _fileStorageService.UploadAsync(file, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new
        {
            storedFile.Id,
            storedFile.FileName,
            storedFile.ContentType,
            storedFile.Size,
            storedFile.CreatedAtUtc
        });
    }
}