using System.Text.Json.Serialization;

namespace ImagePilot.Api.Models;

public static class JobStates
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string WaitingForUser = "WaitingForUser";
    public const string WaitingForResult = "WaitingForResult";
    public const string Downloading = "Downloading";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Retrying = "Retrying";
    public const string Skipped = "Skipped";
    public const string Cancelled = "Cancelled";
}

public static class RunStates
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string WaitingForUser = "WaitingForUser";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed class ProjectRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public int? DefaultProviderId { get; set; }
    public string FileNamingPattern { get; set; } = "{projectCode}_{provider}_{yyyyMMdd}_{jobNo}.{ext}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ProviderRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Web";
    public string StartUrl { get; set; } = "";
    public string PromptInputSelector { get; set; } = "";
    public string SubmitButtonSelector { get; set; } = "";
    public string ResultContainerSelector { get; set; } = "";
    public string DownloadButtonSelector { get; set; } = "";
    public string VerificationSelector { get; set; } = "";
    public int DefaultTimeoutSeconds { get; set; } = 180;
    public int DelayBetweenJobsSeconds { get; set; } = 5;
    public bool ManualModeFallback { get; set; } = true;
    public bool AutomationEnabled { get; set; }
    public string BrowserProfilePath { get; set; } = "";
    public string BrowserChannel { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ChromeProfileRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string AccountLabel { get; set; } = "";
    public string StartUrl { get; set; } = "https://gemini.google.com/app";
    public string BrowserProfilePath { get; set; } = "";
    public string BrowserChannel { get; set; } = "chrome";
    public int DailyQuota { get; set; } = 0;
    public int UsedToday { get; set; } = 0;
    public string Status { get; set; } = "Ready";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class BatchRunRecord
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ProviderId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = RunStates.Pending;
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int? CurrentJobId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

public sealed class PromptJobRecord
{
    public int Id { get; set; }
    public int BatchRunId { get; set; }
    public int ProjectId { get; set; }
    public int ProviderId { get; set; }
    public int JobNo { get; set; }
    public string Prompt { get; set; } = "";
    public string NegativePrompt { get; set; } = "";
    public string Status { get; set; } = JobStates.Pending;
    public int RetryCount { get; set; }
    public int MaxRetry { get; set; } = 2;
    public string? OutputFilePath { get; set; }
    public string? OutputFileName { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class OutputFileRecord
{
    public int Id { get; set; }
    public int PromptJobId { get; set; }
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public long? FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SqlServerSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1433;
    public string InstanceName { get; set; } = "";
    public string DatabaseName { get; set; } = "LocalAiImageRunner";
    public string AuthenticationMode { get; set; } = "Windows";
    public string Username { get; set; } = "";
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; } = true;
    public bool PasswordSaved { get; set; }
    public bool SchemaInitialized { get; set; }
}

public sealed class SqliteSettings
{
    public string DatabasePath { get; set; } = "";
    public bool SchemaInitialized { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

public sealed class PromptAiSettings
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAiCompatible";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public bool ApiKeySaved { get; set; }
}

public sealed class AppData
{
    public int NextProjectId { get; set; } = 1;
    public int NextProviderId { get; set; } = 1;
    public int NextChromeProfileId { get; set; } = 1;
    public int NextBatchRunId { get; set; } = 1;
    public int NextPromptJobId { get; set; } = 1;
    public int NextOutputFileId { get; set; } = 1;
    public List<ProjectRecord> Projects { get; set; } = [];
    public List<ProviderRecord> Providers { get; set; } = [];
    public List<ChromeProfileRecord> ChromeProfiles { get; set; } = [];
    public List<BatchRunRecord> BatchRuns { get; set; } = [];
    public List<PromptJobRecord> PromptJobs { get; set; } = [];
    public List<OutputFileRecord> OutputFiles { get; set; } = [];
    public string PersistenceMode { get; set; } = "Json";
    public SqliteSettings Sqlite { get; set; } = new();
    public SqlServerSettings SqlServer { get; set; } = new();
    public PromptAiSettings PromptAi { get; set; } = new();
}

public sealed record PromptGenerateRequest(
    int ProjectId,
    int ProviderId,
    string BasePrompt,
    string NegativePrompt,
    string VariablesJson,
    int Count,
    int MaxRetry = 2,
    string? RunName = null);

public sealed record SqlSettingsSaveRequest(SqlServerSettings Settings, string? Password);
public sealed record SqlConnectionResult(bool Success, string Message);
public sealed record PersistenceSettingsResult(string Mode, SqliteSettings Sqlite, SqlServerSettings SqlServer);
public sealed record PersistenceModeSaveRequest(string Mode);
public sealed record SqliteSettingsSaveRequest(SqliteSettings Settings);
public sealed record PromptAiSettingsSaveRequest(PromptAiSettings Settings, string? ApiKey);
public sealed record PromptAiResult(bool Success, string Message, string? Prompt = null);
public sealed record PromptEnhanceRequest(string Prompt, string? Category, string? ExtraInstructions);
public sealed record FolderListRequest(string? CurrentPath);
public sealed record FolderCreateRequest(string CurrentPath, string FolderName);
public sealed record FolderEntry(string Name, string Path);
public sealed record FolderListResult(string CurrentPath, string? ParentPath, IReadOnlyList<FolderEntry> Drives, IReadOnlyList<FolderEntry> Folders);
public sealed record CompleteJobRequest(string? OutputFilePath);
public sealed record ChromeProfileAssignRequest(int ProviderId);
public sealed record AgentCreateRequest(string ProjectName, string Provider, string BasePrompt, int Count, string OutputFolder);

public sealed record DashboardSnapshot(
    IReadOnlyList<ProjectRecord> Projects,
    IReadOnlyList<ProviderRecord> Providers,
    IReadOnlyList<ChromeProfileRecord> ChromeProfiles,
    IReadOnlyList<BatchRunRecord> Runs,
    IReadOnlyList<PromptJobRecord> Jobs,
    IReadOnlyList<OutputFileRecord> OutputFiles);
