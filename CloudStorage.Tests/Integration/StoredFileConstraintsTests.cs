using CloudStorage.API;
using CloudStorage.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CloudStorage.Tests.Integration;

[Collection(ApiCollection.Name)]
public class StoredFileConstraintsTests
{
    private const string ForeignKeyViolation = "23503";

    private readonly ApiFixture _fixture;

    public StoredFileConstraintsTests(ApiFixture fixture)
    {
        _fixture = fixture;
    }

    // Сценарий: файл нельзя привязать к пользователю, которого нет в базе.
    [Fact]
    public async Task AddStoredFile_WithUnknownUser_ViolatesForeignKey()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.StoredFiles.Add(NewStoredFile(Guid.NewGuid().ToString()));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        Assert.Equal(ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    // Сценарий: при Restrict пользователя с файлами удалить нельзя — иначе файлы на диске остались бы без владельца.
    [Fact]
    public async Task DeleteUser_WithStoredFiles_IsRestricted()
    {
        using var scope = _fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = NewEmail();

        var user = new ApplicationUser { UserName = email, Email = email };
        dbContext.Users.Add(user);
        dbContext.StoredFiles.Add(NewStoredFile(user.Id));
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"AspNetUsers\" WHERE \"Id\" = {0}",
            user.Id));

        Assert.Equal(ForeignKeyViolation, exception.SqlState);
    }

    private static StoredFile NewStoredFile(string userId)
    {
        return new StoredFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = "constraint-test.txt",
            StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = "text/plain",
            Size = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static string NewEmail()
    {
        return $"constraints-{Guid.NewGuid():N}@example.com";
    }
}
