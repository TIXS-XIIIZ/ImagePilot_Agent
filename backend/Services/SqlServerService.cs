using ImagePilot.Api.Models;
using Microsoft.Data.SqlClient;

namespace ImagePilot.Api.Services;

public sealed class SqlServerService(AppDataStore store, WindowsCredentialStore credentialStore, AppPaths paths)
{
    public async Task<SqlConnectionResult> TestAsync(SqlServerSettings settings, string? password, CancellationToken cancellationToken)
    {
        try
        {
            var resolvedPassword = string.IsNullOrWhiteSpace(password) ? credentialStore.ReadSqlPassword() : password;
            await using var connection = new SqlConnection(BuildConnectionString(settings, resolvedPassword));
            await connection.OpenAsync(cancellationToken);
            return new SqlConnectionResult(true, $"Connected to {connection.DataSource} / {connection.Database}.");
        }
        catch (Exception exception)
        {
            return new SqlConnectionResult(false, exception.Message);
        }
    }

    public async Task<SqlConnectionResult> TestSavedAsync(CancellationToken cancellationToken)
    {
        var settings = store.Read(data => data.SqlServer);
        return await TestAsync(settings, credentialStore.ReadSqlPassword(), cancellationToken);
    }

    public async Task<SqlConnectionResult> InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = store.Read(data => data.SqlServer);
            var script = await File.ReadAllTextAsync(paths.SchemaFile, cancellationToken);
            var password = credentialStore.ReadSqlPassword();
            await EnsureDatabaseAsync(settings, password, cancellationToken);
            await using var connection = new SqlConnection(BuildConnectionString(settings, password));
            await connection.OpenAsync(cancellationToken);

            foreach (var commandText in script.Split("\nGO", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await using var command = new SqlCommand(commandText, connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            store.Write(data =>
            {
                data.SqlServer.SchemaInitialized = true;
                data.PersistenceMode = "SqlServer";
                return data.SqlServer;
            });
            return new SqlConnectionResult(true, "SQL Server schema is ready.");
        }
        catch (Exception exception)
        {
            return new SqlConnectionResult(false, exception.Message);
        }
    }

    public void Save(SqlSettingsSaveRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            credentialStore.SaveSqlPassword(request.Password);
        }

        var passwordSaved = credentialStore.ReadSqlPassword() is not null;
        store.Write(data =>
        {
            var schemaInitialized = HasSameTarget(data.SqlServer, request.Settings) && data.SqlServer.SchemaInitialized;
            data.SqlServer = request.Settings;
            data.SqlServer.PasswordSaved = passwordSaved;
            data.SqlServer.SchemaInitialized = schemaInitialized;
            return data.SqlServer;
        });
    }

    public async Task<SqlConnection?> TryOpenSavedConnectionAsync(CancellationToken cancellationToken)
    {
        var settings = store.Read(data => data.SqlServer);
        var persistenceMode = store.Read(data => data.PersistenceMode);
        if (!persistenceMode.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ||
            !settings.SchemaInitialized)
        {
            return null;
        }

        var connection = new SqlConnection(BuildConnectionString(settings, credentialStore.ReadSqlPassword()));
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

    private static async Task EnsureDatabaseAsync(SqlServerSettings settings, string? password, CancellationToken cancellationToken)
    {
        var masterSettings = new SqlServerSettings
        {
            Host = settings.Host,
            Port = settings.Port,
            InstanceName = settings.InstanceName,
            DatabaseName = "master",
            AuthenticationMode = settings.AuthenticationMode,
            Username = settings.Username,
            Encrypt = settings.Encrypt,
            TrustServerCertificate = settings.TrustServerCertificate
        };
        await using var connection = new SqlConnection(BuildConnectionString(masterSettings, password));
        await connection.OpenAsync(cancellationToken);
        var databaseName = settings.DatabaseName.Replace("]", "]]", StringComparison.Ordinal);
        await using var command = new SqlCommand($"IF DB_ID(@databaseName) IS NULL CREATE DATABASE [{databaseName}]", connection);
        command.Parameters.AddWithValue("@databaseName", settings.DatabaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildConnectionString(SqlServerSettings settings, string? password)
    {
        var host = string.IsNullOrWhiteSpace(settings.InstanceName)
            ? $"{settings.Host},{settings.Port}"
            : $@"{settings.Host}\{settings.InstanceName}";

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = host,
            InitialCatalog = settings.DatabaseName,
            Encrypt = settings.Encrypt,
            TrustServerCertificate = settings.TrustServerCertificate,
            ConnectTimeout = 5
        };

        if (settings.AuthenticationMode.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = false;
            builder.UserID = settings.Username;
            builder.Password = password ?? "";
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }

    private static bool HasSameTarget(SqlServerSettings current, SqlServerSettings update) =>
        current.Host.Equals(update.Host, StringComparison.OrdinalIgnoreCase) &&
        current.Port == update.Port &&
        current.InstanceName.Equals(update.InstanceName, StringComparison.OrdinalIgnoreCase) &&
        current.DatabaseName.Equals(update.DatabaseName, StringComparison.OrdinalIgnoreCase);
}
