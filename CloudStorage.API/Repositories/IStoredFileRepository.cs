using CloudStorage.API.DTOs;
using CloudStorage.API.Models;

namespace CloudStorage.API.Repositories;

public interface IStoredFileRepository
{
    Task AddAsync(StoredFile file, CancellationToken cancellationToken);

    Task<(List<StoredFile> Items, int Total)> GetPageAsync(
        string userId,
        FilesQueryParameters query,
        CancellationToken cancellationToken);

    // Временно, до перевода сервиса на GetPageAsync (шаг 3).
    Task<List<StoredFile>> GetAllAsync(string userId, CancellationToken cancellationToken);

    Task<StoredFile?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken);
    Task Delete(StoredFile file, CancellationToken cancellationToken);
}
