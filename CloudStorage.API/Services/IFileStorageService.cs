using CloudStorage.API.DTOs;
using CloudStorage.API.Models;

namespace CloudStorage.API.Services;

public interface IFileStorageService
{
    public Task<StoredFile> UploadAsync(IFormFile? file, CancellationToken cancellationToken);
    public Task<List<StoredFile>> GetAllFiles(CancellationToken cancellationToken);
    public Task<FileDownloadResult?> DownloadAsync(Guid id, CancellationToken cancellationToken);
    public Task DeleteById(Guid id, CancellationToken cancellationToken);
}