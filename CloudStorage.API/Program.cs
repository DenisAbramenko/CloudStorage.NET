using System.Text;
using CloudStorage.API;
using CloudStorage.API.Errors;
using CloudStorage.API.Models;
using CloudStorage.API.Repositories;
using CloudStorage.API.Services;
using CloudStorage.API.Services.Auth;
using CloudStorage.API.Services.FileSystem;
using CloudStorage.API.Services.Mapping;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var secretKey = string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Secret"])
    ? throw new InvalidOperationException("JWT SecretKey is not configured.")
    : builder.Configuration["Jwt:Secret"];

builder.Services.AddControllers();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidIssuer = builder.Configuration["Jwt:Issuer"];
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = builder.Configuration["Jwt:Audience"];
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey ?? string.Empty));
        options.TokenValidationParameters.ValidateLifetime = true;
    });
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IStoredFileRepository, StoredFileRepository>();
builder.Services.AddScoped<ILocalFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IStoredFileMapper, StoredFileMapper>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/health/db", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { database = "connected" })
        : Results.Problem("Database is unavailable");
});
app.MapControllers();
app.Run();

public partial class Program { }