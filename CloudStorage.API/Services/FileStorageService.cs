using CloudStorage.API.DTOs;
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
        if (file == null) throw new ArgumentNullException(nameof(file));
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

    public async Task<FileDownloadResult?> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var storedFile = await _storedFileRepository.GetByIdAsync(id, cancellationToken);
        if (storedFile == null) return null;
        if (!_localFileStorage.Exists(storedFile.StorageKey))
            throw new FileNotFoundException("Файл отсутствует на диске.");
        var fileStream = await _localFileStorage.OpenRead(storedFile.StorageKey);
        if (fileStream == null) return null;
        return new FileDownloadResult
        {
            Content = fileStream,
            FileName = storedFile.FileName,
            ContentType = storedFile.ContentType
        };
    }

    public async Task DeleteFile(Guid id, CancellationToken cancellationToken)
    {
        var storedFile = await _storedFileRepository.GetByIdAsync(id, cancellationToken);
        if (storedFile == null) throw new FileNotFoundException("Файл отсутствует на диске.");
        _localFileStorage.Delete(storedFile.StorageKey);
        _storedFileRepository.Delete(storedFile);
    }
}