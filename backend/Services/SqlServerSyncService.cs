using ImagePilot.Api.Models;
using Microsoft.Data.SqlClient;

namespace ImagePilot.Api.Services;

public sealed class SqlServerSyncService(
    AppDataStore store,
    SqlServerService sqlServer,
    ILogger<SqlServerSyncService> logger) : BackgroundService
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
                logger.LogDebug(exception, "SQL Server mirror is not available yet.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var snapshot = store.Read(data => data);
        await using var connection = await sqlServer.TryOpenSavedConnectionAsync(cancellationToken);
        if (connection is null)
        {
            return;
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        foreach (var project in snapshot.Projects)
        {
            await UpsertProjectAsync(connection, transaction, project, cancellationToken);
        }

        foreach (var provider in snapshot.Providers)
        {
            await UpsertProviderAsync(connection, transaction, provider, cancellationToken);
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
    }

    private static Task UpsertProjectAsync(SqlConnection connection, SqlTransaction transaction, ProjectRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            SET IDENTITY_INSERT dbo.Projects ON;
            MERGE dbo.Projects AS target
            USING (SELECT @Id AS ProjectId) AS source ON target.ProjectId = source.ProjectId
            WHEN MATCHED THEN UPDATE SET ProjectName=@Name, ProjectCode=@Code, Description=@Description, OutputFolder=@OutputFolder, DefaultProviderId=@DefaultProviderId, FileNamingPattern=@Pattern, UpdatedAt=@UpdatedAt
            WHEN NOT MATCHED THEN INSERT (ProjectId, ProjectName, ProjectCode, Description, OutputFolder, DefaultProviderId, FileNamingPattern, CreatedAt, UpdatedAt) VALUES (@Id, @Name, @Code, @Description, @OutputFolder, @DefaultProviderId, @Pattern, @CreatedAt, @UpdatedAt);
            SET IDENTITY_INSERT dbo.Projects OFF;
            """, cancellationToken,
            ("@Id", item.Id), ("@Name", item.Name), ("@Code", item.Code), ("@Description", item.Description), ("@OutputFolder", item.OutputFolder),
            ("@DefaultProviderId", item.DefaultProviderId), ("@Pattern", item.FileNamingPattern), ("@CreatedAt", item.CreatedAt), ("@UpdatedAt", item.UpdatedAt));

    private static Task UpsertProviderAsync(SqlConnection connection, SqlTransaction transaction, ProviderRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            SET IDENTITY_INSERT dbo.Providers ON;
            MERGE dbo.Providers AS target
            USING (SELECT @Id AS ProviderId) AS source ON target.ProviderId = source.ProviderId
            WHEN MATCHED THEN UPDATE SET ProviderName=@Name, ProviderType=@Type, StartUrl=@Url, PromptInputSelector=@Prompt, SubmitButtonSelector=@Submit, ResultContainerSelector=@Result, DownloadButtonSelector=@Download, DefaultTimeoutSeconds=@Timeout, DelayBetweenJobsSeconds=@Delay, ManualModeFallback=@Manual, BrowserProfilePath=@Profile, UpdatedAt=@UpdatedAt
            WHEN NOT MATCHED THEN INSERT (ProviderId, ProviderName, ProviderType, StartUrl, PromptInputSelector, SubmitButtonSelector, ResultContainerSelector, DownloadButtonSelector, DefaultTimeoutSeconds, DelayBetweenJobsSeconds, ManualModeFallback, BrowserProfilePath, CreatedAt, UpdatedAt) VALUES (@Id, @Name, @Type, @Url, @Prompt, @Submit, @Result, @Download, @Timeout, @Delay, @Manual, @Profile, @CreatedAt, @UpdatedAt);
            SET IDENTITY_INSERT dbo.Providers OFF;
            """, cancellationToken,
            ("@Id", item.Id), ("@Name", item.Name), ("@Type", item.Type), ("@Url", item.StartUrl), ("@Prompt", item.PromptInputSelector),
            ("@Submit", item.SubmitButtonSelector), ("@Result", item.ResultContainerSelector), ("@Download", item.DownloadButtonSelector),
            ("@Timeout", item.DefaultTimeoutSeconds), ("@Delay", item.DelayBetweenJobsSeconds), ("@Manual", item.ManualModeFallback),
            ("@Profile", item.BrowserProfilePath), ("@CreatedAt", item.CreatedAt), ("@UpdatedAt", item.UpdatedAt));

    private static Task UpsertRunAsync(SqlConnection connection, SqlTransaction transaction, BatchRunRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            SET IDENTITY_INSERT dbo.BatchRuns ON;
            MERGE dbo.BatchRuns AS target
            USING (SELECT @Id AS BatchRunId) AS source ON target.BatchRunId = source.BatchRunId
            WHEN MATCHED THEN UPDATE SET ProjectId=@ProjectId, ProviderId=@ProviderId, RunName=@Name, Status=@Status, TotalJobs=@Total, CompletedJobs=@Completed, FailedJobs=@Failed, StartedAt=@StartedAt, CompletedAt=@CompletedAt, ErrorMessage=@Error
            WHEN NOT MATCHED THEN INSERT (BatchRunId, ProjectId, ProviderId, RunName, Status, TotalJobs, CompletedJobs, FailedJobs, StartedAt, CompletedAt, CreatedAt, ErrorMessage) VALUES (@Id, @ProjectId, @ProviderId, @Name, @Status, @Total, @Completed, @Failed, @StartedAt, @CompletedAt, @CreatedAt, @Error);
            SET IDENTITY_INSERT dbo.BatchRuns OFF;
            """, cancellationToken,
            ("@Id", item.Id), ("@ProjectId", item.ProjectId), ("@ProviderId", item.ProviderId), ("@Name", item.Name), ("@Status", item.Status),
            ("@Total", item.TotalJobs), ("@Completed", item.CompletedJobs), ("@Failed", item.FailedJobs), ("@StartedAt", item.StartedAt),
            ("@CompletedAt", item.CompletedAt), ("@CreatedAt", item.CreatedAt), ("@Error", item.ErrorMessage));

    private static Task UpsertJobAsync(SqlConnection connection, SqlTransaction transaction, PromptJobRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            SET IDENTITY_INSERT dbo.PromptJobs ON;
            MERGE dbo.PromptJobs AS target
            USING (SELECT @Id AS PromptJobId) AS source ON target.PromptJobId = source.PromptJobId
            WHEN MATCHED THEN UPDATE SET BatchRunId=@BatchId, ProjectId=@ProjectId, ProviderId=@ProviderId, JobNo=@JobNo, Prompt=@Prompt, NegativePrompt=@Negative, Status=@Status, RetryCount=@Retry, MaxRetry=@MaxRetry, OutputFilePath=@OutputPath, OutputFileName=@OutputName, ErrorMessage=@Error, StartedAt=@StartedAt, CompletedAt=@CompletedAt
            WHEN NOT MATCHED THEN INSERT (PromptJobId, BatchRunId, ProjectId, ProviderId, JobNo, Prompt, NegativePrompt, Status, RetryCount, MaxRetry, OutputFilePath, OutputFileName, ErrorMessage, StartedAt, CompletedAt, CreatedAt) VALUES (@Id, @BatchId, @ProjectId, @ProviderId, @JobNo, @Prompt, @Negative, @Status, @Retry, @MaxRetry, @OutputPath, @OutputName, @Error, @StartedAt, @CompletedAt, @CreatedAt);
            SET IDENTITY_INSERT dbo.PromptJobs OFF;
            """, cancellationToken,
            ("@Id", item.Id), ("@BatchId", item.BatchRunId), ("@ProjectId", item.ProjectId), ("@ProviderId", item.ProviderId), ("@JobNo", item.JobNo),
            ("@Prompt", item.Prompt), ("@Negative", item.NegativePrompt), ("@Status", item.Status), ("@Retry", item.RetryCount),
            ("@MaxRetry", item.MaxRetry), ("@OutputPath", item.OutputFilePath), ("@OutputName", item.OutputFileName), ("@Error", item.ErrorMessage),
            ("@StartedAt", item.StartedAt), ("@CompletedAt", item.CompletedAt), ("@CreatedAt", item.CreatedAt));

    private static Task UpsertOutputFileAsync(SqlConnection connection, SqlTransaction transaction, OutputFileRecord item, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            SET IDENTITY_INSERT dbo.OutputFiles ON;
            MERGE dbo.OutputFiles AS target
            USING (SELECT @Id AS OutputFileId) AS source ON target.OutputFileId = source.OutputFileId
            WHEN MATCHED THEN UPDATE SET PromptJobId=@JobId, FilePath=@Path, FileName=@Name, FileSizeBytes=@Size
            WHEN NOT MATCHED THEN INSERT (OutputFileId, PromptJobId, FilePath, FileName, FileSizeBytes, CreatedAt) VALUES (@Id, @JobId, @Path, @Name, @Size, @CreatedAt);
            SET IDENTITY_INSERT dbo.OutputFiles OFF;
            """, cancellationToken,
            ("@Id", item.Id), ("@JobId", item.PromptJobId), ("@Path", item.FilePath), ("@Name", item.FileName), ("@Size", item.FileSizeBytes), ("@CreatedAt", item.CreatedAt));

    private static async Task ExecuteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = new SqlCommand(commandText, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
