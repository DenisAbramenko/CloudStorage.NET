using CloudStorage.API.DTOs;
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

    public async Task<(List<StoredFile> Items, int Total)> GetPageAsync(
        string userId,
        FilesQueryParameters query,
        CancellationToken cancellationToken)
    {
        var files = _dbContext.StoredFiles
            .AsNoTracking()
            .Where(file => file.UserId == userId);

        var total = await files.CountAsync(cancellationToken);
        var items = await ApplySorting(files, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    // Временно, до перевода сервиса на GetPageAsync (шаг 3).
    public async Task<List<StoredFile>> GetAllAsync(string userId, CancellationToken cancellationToken)
    {
        var (items, _) = await GetPageAsync(
            userId,
            new FilesQueryParameters { Page = 1, PageSize = int.MaxValue },
            cancellationToken);
        return items;
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

    // Сортировка только по разрешённым полям: динамические выражения не собираются.
    private static IOrderedQueryable<StoredFile> ApplySorting(IQueryable<StoredFile> files, FilesQueryParameters query)
    {
        var descending = (query.SortDirection ?? FilesQueryParameters.DefaultSortDirection) == FileSortDirection.Desc;

        var ordered = (query.SortBy ?? FilesQueryParameters.DefaultSortBy) switch
        {
            FileSortField.Name => descending
                ? files.OrderByDescending(file => file.FileName)
                : files.OrderBy(file => file.FileName),
            FileSortField.Size => descending
                ? files.OrderByDescending(file => file.Size)
                : files.OrderBy(file => file.Size),
            _ => descending
                ? files.OrderByDescending(file => file.CreatedAtUtc)
                : files.OrderBy(file => file.CreatedAtUtc)
        };

        // Тай-брейк по Id делает страницы детерминированными при равных ключах сортировки.
        return ordered.ThenBy(file => file.Id);
    }
}
