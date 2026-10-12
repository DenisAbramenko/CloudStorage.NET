using CloudStorage.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CloudStorage.API.Repositories;

public class StoredFileRepository : IStoredFileRepository
{
    private readonly AppDbContext _dbContext;

    public StoredFileRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(StoredFile file, CancellationToken cancellationToken)
    {
        _dbContext.StoredFiles.Add(file);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<StoredFile>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.StoredFiles
            .AsNoTracking()
            .OrderByDescending(file => file.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}