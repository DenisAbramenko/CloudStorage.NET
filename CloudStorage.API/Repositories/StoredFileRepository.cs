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

    public async Task<List<StoredFile>> GetAllAsync(string userId, CancellationToken cancellationToken)
    {
        return await _dbContext.StoredFiles
            .AsNoTracking()
            .Where(file => file.UserId == userId)
            .OrderByDescending(file => file.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<StoredFile?> GetByIdAsync(string userId, Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.StoredFiles
            .AsNoTracking()
            .Where(file => file.UserId == userId)
            .FirstOrDefaultAsync(file => file.Id == id, cancellationToken);
    }

    public async Task Delete(StoredFile file, CancellationToken cancellationToken)
    {
        _dbContext.Remove(file);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}