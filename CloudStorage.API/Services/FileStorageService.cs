using CloudStorage.API.Models;
using CloudStorage.API.Repositories;
using CloudStorage.API.Services.FileSystem;

namespace CloudStorage.API.Services;

public class FileStorageService : IFileStorageService
{
    private readonly ILocalFileStorage _localFileStorage;
    private readonly IStoredFileRepository _storedFileRepository;

    public FileStorageService(IStoredFileRepository storedFileRepository, ILocalFileStorage localFileStorage)
    {
        _storedFileRepository = storedFileRepository;
        _localFileStorage = localFileStorage;
    }

    public async Task<StoredFile> UploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        var storageKey = Guid.NewGuid().ToString("N");
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
            await _localFileStorage.Save(storageKey, file.OpenReadStream(), cancellationToken);
            await _storedFileRepository.AddAsync(storedFile, cancellationToken);
            return storedFile;
        }
        catch
        {
            if (_localFileStorage.Exists(storageKey)) _localFileStorage.Delete(storageKey);

            throw;
        }
    }

    public async Task<List<StoredFile>> GetAllFiles(CancellationToken cancellationToken)
    {
        return await _storedFileRepository.GetAllAsync(cancellationToken);
    }
}