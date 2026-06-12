IF OBJECT_ID('dbo.Projects', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Projects (
        ProjectId INT IDENTITY(1,1) PRIMARY KEY,
        ProjectName NVARCHAR(200) NOT NULL,
        ProjectCode NVARCHAR(100) NULL,
        Description NVARCHAR(MAX) NULL,
        OutputFolder NVARCHAR(1000) NOT NULL,
        DefaultProviderId INT NULL,
        FileNamingPattern NVARCHAR(300) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO
IF OBJECT_ID('dbo.Providers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Providers (
        ProviderId INT IDENTITY(1,1) PRIMARY KEY,
        ProviderName NVARCHAR(200) NOT NULL,
        ProviderType NVARCHAR(100) NOT NULL,
        StartUrl NVARCHAR(1000) NOT NULL,
        PromptInputSelector NVARCHAR(500) NULL,
        SubmitButtonSelector NVARCHAR(500) NULL,
        ResultContainerSelector NVARCHAR(500) NULL,
        DownloadButtonSelector NVARCHAR(500) NULL,
        DefaultTimeoutSeconds INT NOT NULL DEFAULT 180,
        DelayBetweenJobsSeconds INT NOT NULL DEFAULT 60,
        ManualModeFallback BIT NOT NULL DEFAULT 1,
        BrowserProfilePath NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        IsActive BIT NOT NULL DEFAULT 1
    );
END
GO
IF OBJECT_ID('dbo.BatchRuns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BatchRuns (
        BatchRunId INT IDENTITY(1,1) PRIMARY KEY,
        ProjectId INT NOT NULL,
        ProviderId INT NOT NULL,
        RunName NVARCHAR(200) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        TotalJobs INT NOT NULL DEFAULT 0,
        CompletedJobs INT NOT NULL DEFAULT 0,
        FailedJobs INT NOT NULL DEFAULT 0,
        StartedAt DATETIME2 NULL,
        CompletedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ErrorMessage NVARCHAR(MAX) NULL
    );
END
GO
IF OBJECT_ID('dbo.PromptJobs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PromptJobs (
        PromptJobId INT IDENTITY(1,1) PRIMARY KEY,
        BatchRunId INT NOT NULL,
        ProjectId INT NOT NULL,
        ProviderId INT NOT NULL,
        JobNo INT NOT NULL,
        Prompt NVARCHAR(MAX) NOT NULL,
        NegativePrompt NVARCHAR(MAX) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        RetryCount INT NOT NULL DEFAULT 0,
        MaxRetry INT NOT NULL DEFAULT 2,
        OutputFilePath NVARCHAR(1000) NULL,
        OutputFileName NVARCHAR(300) NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        StartedAt DATETIME2 NULL,
        CompletedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO
IF OBJECT_ID('dbo.OutputFiles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutputFiles (
        OutputFileId INT IDENTITY(1,1) PRIMARY KEY,
        PromptJobId INT NOT NULL,
        FilePath NVARCHAR(1000) NOT NULL,
        FileName NVARCHAR(300) NOT NULL,
        FileSizeBytes BIGINT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO
IF OBJECT_ID('dbo.AgentToolCalls', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgentToolCalls (
        AgentToolCallId INT IDENTITY(1,1) PRIMARY KEY,
        ToolName NVARCHAR(200) NOT NULL,
        RequestJson NVARCHAR(MAX) NULL,
        ResponseJson NVARCHAR(MAX) NULL,
        Status NVARCHAR(50) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CompletedAt DATETIME2 NULL,
        ErrorMessage NVARCHAR(MAX) NULL
    );
END
