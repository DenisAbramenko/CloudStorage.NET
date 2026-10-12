using CloudStorage.API.Models;

namespace CloudStorage.API.Repositories;

public interface IStoredFileRepository
{
    Task AddAsync(StoredFile file, CancellationToken cancellationToken);

    Task<List<StoredFile>> GetAllAsync(CancellationToken cancellationToken);
}