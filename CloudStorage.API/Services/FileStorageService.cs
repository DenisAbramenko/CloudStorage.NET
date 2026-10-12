using CloudStorage.API.Models;
using CloudStorage.API.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CloudStorage.API.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IStoredFileRepository _storedFileRepository;

    public FileStorageService(IWebHostEnvironment environment, IStoredFileRepository storedFileRepository)
    {
        _environment = environment;
        _storedFileRepository = storedFileRepository;
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

            await _storedFileRepository.AddAsync(storedFile, cancellationToken);
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

    public async Task<List<StoredFile>> GetAllFiles(CancellationToken cancellationToken)
    {
        return await _storedFileRepository.GetAllAsync(cancellationToken);
    }
}