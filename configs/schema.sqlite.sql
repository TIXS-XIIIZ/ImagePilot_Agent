CREATE TABLE IF NOT EXISTS Projects (
    ProjectId INTEGER PRIMARY KEY,
    ProjectName TEXT NOT NULL,
    ProjectCode TEXT NULL,
    Description TEXT NULL,
    OutputFolder TEXT NOT NULL,
    DefaultProviderId INTEGER NULL,
    FileNamingPattern TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Providers (
    ProviderId INTEGER PRIMARY KEY,
    ProviderName TEXT NOT NULL,
    ProviderType TEXT NOT NULL,
    StartUrl TEXT NOT NULL,
    PromptInputSelector TEXT NULL,
    SubmitButtonSelector TEXT NULL,
    ResultContainerSelector TEXT NULL,
    DownloadButtonSelector TEXT NULL,
    DefaultTimeoutSeconds INTEGER NOT NULL DEFAULT 180,
    DelayBetweenJobsSeconds INTEGER NOT NULL DEFAULT 60,
    ManualModeFallback INTEGER NOT NULL DEFAULT 1,
    AutomationEnabled INTEGER NOT NULL DEFAULT 0,
    BrowserProfilePath TEXT NULL,
    BrowserChannel TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS ChromeProfiles (
    ChromeProfileId INTEGER PRIMARY KEY,
    ProfileName TEXT NOT NULL,
    AccountLabel TEXT NULL,
    StartUrl TEXT NOT NULL,
    BrowserProfilePath TEXT NULL,
    BrowserChannel TEXT NULL,
    DailyQuota INTEGER NOT NULL DEFAULT 0,
    UsedToday INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL,
    Notes TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NULL
);

CREATE TABLE IF NOT EXISTS BatchRuns (
    BatchRunId INTEGER PRIMARY KEY,
    ProjectId INTEGER NOT NULL,
    ProviderId INTEGER NOT NULL,
    RunName TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    TotalJobs INTEGER NOT NULL DEFAULT 0,
    CompletedJobs INTEGER NOT NULL DEFAULT 0,
    FailedJobs INTEGER NOT NULL DEFAULT 0,
    StartedAt TEXT NULL,
    CompletedAt TEXT NULL,
    CreatedAt TEXT NOT NULL,
    ErrorMessage TEXT NULL
);

CREATE TABLE IF NOT EXISTS PromptJobs (
    PromptJobId INTEGER PRIMARY KEY,
    BatchRunId INTEGER NOT NULL,
    ProjectId INTEGER NOT NULL,
    ProviderId INTEGER NOT NULL,
    JobNo INTEGER NOT NULL,
    Prompt TEXT NOT NULL,
    NegativePrompt TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    RetryCount INTEGER NOT NULL DEFAULT 0,
    MaxRetry INTEGER NOT NULL DEFAULT 2,
    OutputFilePath TEXT NULL,
    OutputFileName TEXT NULL,
    ErrorMessage TEXT NULL,
    StartedAt TEXT NULL,
    CompletedAt TEXT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS OutputFiles (
    OutputFileId INTEGER PRIMARY KEY,
    PromptJobId INTEGER NOT NULL,
    FilePath TEXT NOT NULL,
    FileName TEXT NOT NULL,
    FileSizeBytes INTEGER NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS AgentToolCalls (
    AgentToolCallId INTEGER PRIMARY KEY,
    ToolName TEXT NOT NULL,
    RequestJson TEXT NULL,
    ResponseJson TEXT NULL,
    Status TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    CompletedAt TEXT NULL,
    ErrorMessage TEXT NULL
);
