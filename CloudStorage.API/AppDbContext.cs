using CloudStorage.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CloudStorage.API;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
}