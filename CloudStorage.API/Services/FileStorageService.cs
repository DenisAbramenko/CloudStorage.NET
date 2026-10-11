using CloudStorage.API.Models;

namespace CloudStorage.API.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _dbContext;

    public FileStorageService(IWebHostEnvironment environment, AppDbContext dbContext)
    {
        _environment = environment;
        _dbContext = dbContext;
    }

    public async Task<StoredFile> UploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
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
            return storedFile;
        }
        catch
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            throw;
        }
    }
}