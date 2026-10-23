using CloudStorage.API.Models;

namespace CloudStorage.API.Repositories;

public interface IStoredFileRepository
{
    Task AddAsync(StoredFile file, CancellationToken cancellationToken);

    Task<List<StoredFile>> GetAllAsync(string userId, CancellationToken cancellationToken);
    Task<StoredFile?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken);
    Task Delete(StoredFile file, CancellationToken cancellationToken);
}