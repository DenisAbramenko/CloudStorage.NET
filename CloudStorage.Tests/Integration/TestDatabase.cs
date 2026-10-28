using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CloudStorage.Tests.Integration;

internal static partial class TestDatabase
{
    private const string DefaultDatabaseName = "cloudstorage_tests";

    private static readonly Lazy<(string ConnectionString, string DatabaseName)> Settings = new(Build);

    public static string ConnectionString => Settings.Value.ConnectionString;

    public static string DatabaseName => Settings.Value.DatabaseName;

    private static (string ConnectionString, string DatabaseName) Build()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("CLOUDSTORAGE_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            var builder = new NpgsqlConnectionStringBuilder(fromEnvironment);
            var name = string.IsNullOrWhiteSpace(builder.Database) ? DefaultDatabaseName : builder.Database;
            return Validated(new NpgsqlConnectionStringBuilder(fromEnvironment) { Database = name }.ConnectionString, name);
        }

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new InvalidOperationException(
                "Не найдена строка подключения к PostgreSQL. Задайте переменную окружения " +
                "CLOUDSTORAGE_TEST_CONNECTION или user-secrets ConnectionStrings:DefaultConnection проекта CloudStorage.API.");
        }

        return Validated(new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = DefaultDatabaseName
        }.ConnectionString, DefaultDatabaseName);
    }

    private static (string ConnectionString, string DatabaseName) Validated(string connectionString, string databaseName)
    {
        if (!DatabaseNamePattern().IsMatch(databaseName))
        {
            throw new InvalidOperationException(
                $"Недопустимое имя тестовой базы '{databaseName}': разрешены латинские буквы, цифры и подчёркивание.");
        }

        if (!databaseName.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Имя тестовой базы '{databaseName}' должно содержать 'test': тесты удаляют данные. " +
                "Проверьте переменную окружения CLOUDSTORAGE_TEST_CONNECTION.");
        }

        return (connectionString, databaseName);
    }

    public static void EnsureDatabaseExists()
    {
        var adminConnectionString = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "postgres"
        }.ConnectionString;

        using var connection = new NpgsqlConnection(adminConnectionString);
        connection.Open();

        using (var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection))
        {
            exists.Parameters.AddWithValue("name", DatabaseName);
            if (exists.ExecuteScalar() is not null) return;
        }

        try
        {
            using var create = new NpgsqlCommand($"CREATE DATABASE \"{DatabaseName}\"", connection);
            create.ExecuteNonQuery();
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            throw new InvalidOperationException(
                $"Нет прав на создание базы '{DatabaseName}'. Создайте базу вручную или укажите " +
                "CLOUDSTORAGE_TEST_CONNECTION на существующую тестовую базу.", exception);
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex DatabaseNamePattern();
}
