using CloudStorage.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CloudStorage.API;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StoredFile>(entity =>
        {
            // Покрывает фильтр по владельцу, пагинацию по умолчанию и тай-брейк по Id.
            entity.HasIndex(file => new { file.UserId, file.CreatedAtUtc, file.Id });

            // Каскад удалил бы записи, оставив файлы на диске, поэтому удаление запрещено.
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(file => file.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}