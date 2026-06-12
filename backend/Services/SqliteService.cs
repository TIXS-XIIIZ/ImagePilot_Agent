using ImagePilot.Api.Models;
using Microsoft.Data.Sqlite;

namespace ImagePilot.Api.Services;

public sealed class SqliteService(AppDataStore store, AppPaths paths)
{
    public SqlConnectionResult Save(SqliteSettingsSaveRequest request)
    {
        var settings = Normalize(request.Settings);
        store.Write(data =>
        {
            var schemaInitialized = HasSameTarget(data.Sqlite, settings) && data.Sqlite.SchemaInitialized;
            data.Sqlite = settings;
            data.Sqlite.SchemaInitialized = schemaInitialized;
            return data.Sqlite;
        });
        return new SqlConnectionResult(true, "SQLite settings saved.");
    }

    public SqlConnectionResult SaveMode(PersistenceModeSaveRequest request)
    {
        var mode = NormalizeMode(request.Mode);
        store.Write(data =>
        {
            data.PersistenceMode = mode;
            return data.PersistenceMode;
        });
        return new SqlConnectionResult(true, $"Persistence mode set to {mode}.");
    }

    public async Task<SqlConnectionResult> TestAsync(SqliteSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var normalized = Normalize(settings);
            Directory.CreateDirectory(Path.GetDirectoryName(normalized.DatabasePath)!);
            await using var connection = CreateConnection(normalized);
            await connection.OpenAsync(cancellationToken);
            return new SqlConnectionResult(true, $"SQLite file is ready: {normalized.DatabasePath}");
        }
        catch (Exception exception)
        {
            return new SqlConnectionResult(false, exception.Message);
        }
    }

    public async Task<SqlConnectionResult> TestSavedAsync(CancellationToken cancellationToken)
    {
        var settings = store.Read(data => data.Sqlite);
        return await TestAsync(settings, cancellationToken);
    }

    public async Task<SqlConnectionResult> InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = Normalize(store.Read(data => data.Sqlite));
            Directory.CreateDirectory(Path.GetDirectoryName(settings.DatabasePath)!);
            var script = await File.ReadAllTextAsync(paths.SqliteSchemaFile, cancellationToken);
            await using var connection = CreateConnection(settings);
            await connection.OpenAsync(cancellationToken);

            foreach (var commandText in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = commandText;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            store.Write(data =>
            {
                data.Sqlite = settings;
                data.Sqlite.SchemaInitialized = true;
                data.PersistenceMode = "Sqlite";
                return data.Sqlite;
            });
            return new SqlConnectionResult(true, $"SQLite schema is ready: {settings.DatabasePath}");
        }
        catch (Exception exception)
        {
            return new SqlConnectionResult(false, exception.Message);
        }
    }

    public async Task<SqliteConnection?> TryOpenSavedConnectionAsync(CancellationToken cancellationToken)
    {
        var state = store.Read(data => new { data.PersistenceMode, data.Sqlite });
        if (!state.PersistenceMode.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
            !state.Sqlite.SchemaInitialized)
        {
            return null;
        }

        var connection = CreateConnection(Normalize(state.Sqlite));
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public SqliteSettings Normalize(SqliteSettings settings)
    {
        return new SqliteSettings
        {
            DatabasePath = string.IsNullOrWhiteSpace(settings.DatabasePath)
                ? paths.DefaultSqliteDatabaseFile
                : Path.GetFullPath(settings.DatabasePath.Trim()),
            SchemaInitialized = settings.SchemaInitialized,
            LastSyncedAt = settings.LastSyncedAt
        };
    }

    private static SqliteConnection CreateConnection(SqliteSettings settings)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        return new SqliteConnection(builder.ConnectionString);
    }

    private static string NormalizeMode(string mode) =>
        mode.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            ? "SqlServer"
            : mode.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
                ? "Sqlite"
                : "Json";

    private bool HasSameTarget(SqliteSettings current, SqliteSettings update) =>
        Normalize(current).DatabasePath.Equals(Normalize(update).DatabasePath, StringComparison.OrdinalIgnoreCase);
}
