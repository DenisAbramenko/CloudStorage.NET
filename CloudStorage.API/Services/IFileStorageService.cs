using CloudStorage.API.DTOs;
using CloudStorage.API.Models;

namespace CloudStorage.API.Services;

public interface IFileStorageService
{
    public Task<StoredFile> UploadAsync(IFormFile? file, CancellationToken cancellationToken);
    public Task<List<StoredFile>> GetAllFiles(CancellationToken cancellationToken);
}