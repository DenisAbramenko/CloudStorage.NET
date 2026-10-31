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

    public async Task<StoredFile> UploadAsync(IFormFile? file, string userId, CancellationToken cancellationToken)
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
            CreatedAtUtc = DateTime.UtcNow,
            UserId = userId
        };
        await using var content = file.OpenReadStream();

        try
        {
            await _localFileStorage.Save(storageKey, content, cancellationToken);
            await _storedFileRepository.AddAsync(storedFile, cancellationToken);
            return storedFile;
        }
        catch
        {
            if (_localFileStorage.Exists(storageKey)) _localFileStorage.Delete(storageKey);

            throw;
        }
    }

    public async Task<PagedResult<StoredFile>> GetFilesPageAsync(
        string userId,
        FilesQueryParameters query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalized = Normalize(query);
        var (items, total) = await _storedFileRepository.GetPageAsync(userId, normalized, cancellationToken);

        return new PagedResult<StoredFile>(items, total, normalized.Page, normalized.PageSize);
    }

    public async Task<FileDownloadResult?> DownloadAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        var storedFile = await _storedFileRepository.GetByIdAsync(userId, id, cancellationToken);
        if (storedFile == null) return null;
        if (!_localFileStorage.Exists(storedFile.StorageKey))
            throw new FileNotFoundException("Файл отсутствует на диске.");
        var fileStream = _localFileStorage.OpenRead(storedFile.StorageKey);
        if (fileStream == null) return null;
        return new FileDownloadResult
        {
            Content = fileStream,
            FileName = storedFile.FileName,
            ContentType = storedFile.ContentType
        };
    }

    public async Task<bool> DeleteById(string userId, Guid id, CancellationToken cancellationToken)
    {
        var storedFile = await _storedFileRepository.GetByIdAsync(userId, id, cancellationToken);
        if (storedFile == null) return false;
        _localFileStorage.Delete(storedFile.StorageKey);
        await _storedFileRepository.Delete(storedFile, cancellationToken);
        return true;
    }

    // Нормализация на случай прямого вызова сервиса в обход валидации контроллера.
    private static FilesQueryParameters Normalize(FilesQueryParameters query)
    {
        return new FilesQueryParameters
        {
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize is >= 1 and <= FilesQueryParameters.MaxPageSize
                ? query.PageSize
                : FilesQueryParameters.DefaultPageSize,
            SortBy = query.SortBy ?? FilesQueryParameters.DefaultSortBy,
            SortDirection = query.SortDirection ?? FilesQueryParameters.DefaultSortDirection
        };
    }
}