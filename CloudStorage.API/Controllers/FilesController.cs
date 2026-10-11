using CloudStorage.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace CloudStorage.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public FilesController(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
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

        var uploadDirectory = Path.Combine(_environment.ContentRootPath, "App_Data", "Uploads");

        Directory.CreateDirectory(uploadDirectory);

        var storageKey = Guid.NewGuid().ToString("N");
        var filePath = Path.Combine(uploadDirectory, storageKey);

        var storedFile = new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(file.FileName),
            StorageKey = storageKey,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            Size = file.Length,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await using (var stream = new FileStream(
                             filePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _dbContext.StoredFiles.Add(storedFile);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            throw;
        }

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