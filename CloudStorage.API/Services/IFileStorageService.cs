using CloudStorage.API.DTOs;
using CloudStorage.API.Models;

namespace CloudStorage.API.Services;

public interface IFileStorageService
{
    public Task<StoredFile> UploadAsync(IFormFile? file, string userId, CancellationToken cancellationToken);
    public Task<PagedResult<StoredFile>> GetFilesPageAsync(string userId, FilesQueryParameters query, CancellationToken cancellationToken);
    public Task<FileDownloadResult?> DownloadAsync(string userId, Guid id, CancellationToken cancellationToken);
    public Task<bool> DeleteById(string userId, Guid id, CancellationToken cancellationToken);
}