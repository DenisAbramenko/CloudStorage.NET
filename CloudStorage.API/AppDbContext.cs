using Microsoft.EntityFrameworkCore;

namespace CloudStorage.API;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);