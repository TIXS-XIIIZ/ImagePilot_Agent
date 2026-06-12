using ImagePilot.Api.Models;
using Microsoft.Data.Sqlite;

namespace ImagePilot.Api.Services;

public sealed class SqliteSyncService(
    AppDataStore store,
    SqliteService sqlite,
    ILogger<SqliteSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "SQLite mirror is not available yet.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var snapshot = store.Read(data => data);
        await using var connection = await sqlite.TryOpenSavedConnectionAsync(cancellationToken);
        if (connection is null)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var project in snapshot.Projects)
        {
            await UpsertProjectAsync(connection, transaction, project, cancellationToken);
        }

        foreach (var provider in snapshot.Providers)
        {
            await UpsertProviderAsync(connection, transaction, provider, cancellationToken);
        }

        foreach (var profile in snapshot.ChromeProfiles)
        {
            await UpsertChromeProfileAsync(connection, transaction, profile, cancellationToken);
        }

        foreach (var run in snapshot.BatchRuns)
        {
            await UpsertRunAsync(connection, transaction, run, cancellationToken);
        }

        foreach (var job in snapshot.PromptJobs)
        {
            await UpsertJobAsync(connection, transaction, job, cancellationToken);
        }

        foreach (var file in snapshot.OutputFiles)
        {
            await UpsertOutputFileAsync(connection, transaction, file, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        store.Write(data =>
        {
            data.Sqlite.LastSyncedAt = DateTime.UtcNow;
            return data.Sqlite;
        });
    }

    private static Task UpsertProjectAsync(SqliteConnection connection, SqliteTransaction transaction, ProjectRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT INTO Projects (ProjectId, ProjectName, ProjectCode, Description, OutputFolder, DefaultProviderId, FileNamingPattern, CreatedAt, UpdatedAt)
            VALUES ($Id, $Name, $Code, $Description, $OutputFolder, $DefaultProviderId, $Pattern, $CreatedAt, $UpdatedAt)
            ON CONFLICT(ProjectId) DO UPDATE SET ProjectName=$Name, ProjectCode=$Code, Description=$Description, OutputFolder=$OutputFolder, DefaultProviderId=$DefaultProviderId, FileNamingPattern=$Pattern, UpdatedAt=$UpdatedAt
            """, cancellationToken,
            ("$Id", item.Id), ("$Name", item.Name), ("$Code", item.Code), ("$Description", item.Description), ("$OutputFolder", item.OutputFolder),
            ("$DefaultProviderId", item.DefaultProviderId), ("$Pattern", item.FileNamingPattern), ("$CreatedAt", item.CreatedAt), ("$UpdatedAt", item.UpdatedAt));

    private static Task UpsertProviderAsync(SqliteConnection connection, SqliteTransaction transaction, ProviderRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT INTO Providers (ProviderId, ProviderName, ProviderType, StartUrl, PromptInputSelector, SubmitButtonSelector, ResultContainerSelector, DownloadButtonSelector, DefaultTimeoutSeconds, DelayBetweenJobsSeconds, ManualModeFallback, AutomationEnabled, BrowserProfilePath, BrowserChannel, CreatedAt, UpdatedAt)
            VALUES ($Id, $Name, $Type, $Url, $Prompt, $Submit, $Result, $Download, $Timeout, $Delay, $Manual, $Automation, $Profile, $Channel, $CreatedAt, $UpdatedAt)
            ON CONFLICT(ProviderId) DO UPDATE SET ProviderName=$Name, ProviderType=$Type, StartUrl=$Url, PromptInputSelector=$Prompt, SubmitButtonSelector=$Submit, ResultContainerSelector=$Result, DownloadButtonSelector=$Download, DefaultTimeoutSeconds=$Timeout, DelayBetweenJobsSeconds=$Delay, ManualModeFallback=$Manual, AutomationEnabled=$Automation, BrowserProfilePath=$Profile, BrowserChannel=$Channel, UpdatedAt=$UpdatedAt
            """, cancellationToken,
            ("$Id", item.Id), ("$Name", item.Name), ("$Type", item.Type), ("$Url", item.StartUrl), ("$Prompt", item.PromptInputSelector),
            ("$Submit", item.SubmitButtonSelector), ("$Result", item.ResultContainerSelector), ("$Download", item.DownloadButtonSelector),
            ("$Timeout", item.DefaultTimeoutSeconds), ("$Delay", item.DelayBetweenJobsSeconds), ("$Manual", item.ManualModeFallback),
            ("$Automation", item.AutomationEnabled), ("$Profile", item.BrowserProfilePath), ("$Channel", item.BrowserChannel),
            ("$CreatedAt", item.CreatedAt), ("$UpdatedAt", item.UpdatedAt));

    private static Task UpsertChromeProfileAsync(SqliteConnection connection, SqliteTransaction transaction, ChromeProfileRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT INTO ChromeProfiles (ChromeProfileId, ProfileName, AccountLabel, StartUrl, BrowserProfilePath, BrowserChannel, DailyQuota, UsedToday, Status, Notes, CreatedAt, UpdatedAt)
            VALUES ($Id, $Name, $Account, $Url, $Profile, $Channel, $DailyQuota, $UsedToday, $Status, $Notes, $CreatedAt, $UpdatedAt)
            ON CONFLICT(ChromeProfileId) DO UPDATE SET ProfileName=$Name, AccountLabel=$Account, StartUrl=$Url, BrowserProfilePath=$Profile, BrowserChannel=$Channel, DailyQuota=$DailyQuota, UsedToday=$UsedToday, Status=$Status, Notes=$Notes, UpdatedAt=$UpdatedAt
            """, cancellationToken,
            ("$Id", item.Id), ("$Name", item.Name), ("$Account", item.AccountLabel), ("$Url", item.StartUrl),
            ("$Profile", item.BrowserProfilePath), ("$Channel", item.BrowserChannel), ("$DailyQuota", item.DailyQuota),
            ("$UsedToday", item.UsedToday), ("$Status", item.Status), ("$Notes", item.Notes), ("$CreatedAt", item.CreatedAt), ("$UpdatedAt", item.UpdatedAt));

    private static Task UpsertRunAsync(SqliteConnection connection, SqliteTransaction transaction, BatchRunRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT INTO BatchRuns (BatchRunId, ProjectId, ProviderId, RunName, Status, TotalJobs, CompletedJobs, FailedJobs, StartedAt, CompletedAt, CreatedAt, ErrorMessage)
            VALUES ($Id, $ProjectId, $ProviderId, $Name, $Status, $Total, $Completed, $Failed, $StartedAt, $CompletedAt, $CreatedAt, $Error)
            ON CONFLICT(BatchRunId) DO UPDATE SET ProjectId=$ProjectId, ProviderId=$ProviderId, RunName=$Name, Status=$Status, TotalJobs=$Total, CompletedJobs=$Completed, FailedJobs=$Failed, StartedAt=$StartedAt, CompletedAt=$CompletedAt, ErrorMessage=$Error
            """, cancellationToken,
            ("$Id", item.Id), ("$ProjectId", item.ProjectId), ("$ProviderId", item.ProviderId), ("$Name", item.Name), ("$Status", item.Status),
            ("$Total", item.TotalJobs), ("$Completed", item.CompletedJobs), ("$Failed", item.FailedJobs), ("$StartedAt", item.StartedAt),
            ("$CompletedAt", item.CompletedAt), ("$CreatedAt", item.CreatedAt), ("$Error", item.ErrorMessage));

    private static Task UpsertJobAsync(SqliteConnection connection, SqliteTransaction transaction, PromptJobRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT INTO PromptJobs (PromptJobId, BatchRunId, ProjectId, ProviderId, JobNo, Prompt, NegativePrompt, Status, RetryCount, MaxRetry, OutputFilePath, OutputFileName, ErrorMessage, StartedAt, CompletedAt, CreatedAt)
            VALUES ($Id, $BatchId, $ProjectId, $ProviderId, $JobNo, $Prompt, $Negative, $Status, $Retry, $MaxRetry, $OutputPath, $OutputName, $Error, $StartedAt, $CompletedAt, $CreatedAt)
            ON CONFLICT(PromptJobId) DO UPDATE SET BatchRunId=$BatchId, ProjectId=$ProjectId, ProviderId=$ProviderId, JobNo=$JobNo, Prompt=$Prompt, NegativePrompt=$Negative, Status=$Status, RetryCount=$Retry, MaxRetry=$MaxRetry, OutputFilePath=$OutputPath, OutputFileName=$OutputName, ErrorMessage=$Error, StartedAt=$StartedAt, CompletedAt=$CompletedAt
            """, cancellationToken,
            ("$Id", item.Id), ("$BatchId", item.BatchRunId), ("$ProjectId", item.ProjectId), ("$ProviderId", item.ProviderId), ("$JobNo", item.JobNo),
            ("$Prompt", item.Prompt), ("$Negative", item.NegativePrompt), ("$Status", item.Status), ("$Retry", item.RetryCount),
            ("$MaxRetry", item.MaxRetry), ("$OutputPath", item.OutputFilePath), ("$OutputName", item.OutputFileName), ("$Error", item.ErrorMessage),
            ("$StartedAt", item.StartedAt), ("$CompletedAt", item.CompletedAt), ("$CreatedAt", item.CreatedAt));

    private static Task UpsertOutputFileAsync(SqliteConnection connection, SqliteTransaction transaction, OutputFileRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT INTO OutputFiles (OutputFileId, PromptJobId, FilePath, FileName, FileSizeBytes, CreatedAt)
            VALUES ($Id, $JobId, $Path, $Name, $Size, $CreatedAt)
            ON CONFLICT(OutputFileId) DO UPDATE SET PromptJobId=$JobId, FilePath=$Path, FileName=$Name, FileSizeBytes=$Size
            """, cancellationToken,
            ("$Id", item.Id), ("$JobId", item.PromptJobId), ("$Path", item.FilePath), ("$Name", item.FileName), ("$Size", item.FileSizeBytes), ("$CreatedAt", item.CreatedAt));

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
