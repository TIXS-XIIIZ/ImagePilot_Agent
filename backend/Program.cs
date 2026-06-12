using ImagePilot.Api.Models;
using ImagePilot.Api.Services;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<AppPaths>();
builder.Services.AddSingleton<AppDataStore>();
builder.Services.AddSingleton<WindowsCredentialStore>();
builder.Services.AddSingleton<SqlServerService>();
builder.Services.AddSingleton<SqliteService>();
builder.Services.AddSingleton<PromptTemplateService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PromptAiService>();
builder.Services.AddSingleton<FolderPickerService>();
builder.Services.AddSingleton<BrowserAutomationService>();
builder.Services.AddSingleton<BatchQueueService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BatchQueueService>());
builder.Services.AddSingleton<SqlServerSyncService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SqlServerSyncService>());
builder.Services.AddSingleton<SqliteSyncService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SqliteSyncService>());

var app = builder.Build();
app.UseCors();
app.Use(async (context, next) =>
{
    if (context.Request.Method is not ("GET" or "OPTIONS") &&
        context.Request.Path.StartsWithSegments("/api") &&
        context.Request.Headers["X-ImagePilot-Token"] != "imagepilot-local-ui")
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Missing or invalid local UI token." });
        return;
    }

    await next();
});

app.MapGet("/", () => Results.Ok(new { name = "ImagePilot_Agent", status = "ready" }));
app.MapGet("/api/dashboard", (AppDataStore store) =>
{
    return store.Read(data => new DashboardSnapshot(data.Projects, data.Providers, data.ChromeProfiles, data.BatchRuns, data.PromptJobs, data.OutputFiles));
});

app.MapGet("/api/projects", (AppDataStore store) => store.Read(data => data.Projects));
app.MapPost("/api/projects", (ProjectRecord project, AppDataStore store, AppPaths paths) =>
{
    return Results.Ok(store.Write(data =>
    {
        project.Id = data.NextProjectId++;
        project.Code = string.IsNullOrWhiteSpace(project.Code) ? AppDataStore.Slugify(project.Name) : project.Code;
        project.OutputFolder = string.IsNullOrWhiteSpace(project.OutputFolder)
            ? Path.Combine(paths.ProjectsFolder, AppDataStore.Slugify(project.Code), "outputs")
            : project.OutputFolder;
        Directory.CreateDirectory(project.OutputFolder);
        project.CreatedAt = DateTime.UtcNow;
        data.Projects.Add(project);
        return project;
    }));
});
app.MapPut("/api/projects/{id:int}", (int id, ProjectRecord update, AppDataStore store) =>
{
    return Results.Ok(store.Write(data =>
    {
        var project = data.Projects.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("Project was not found.");
        project.Name = update.Name;
        project.Code = update.Code;
        project.Description = update.Description;
        project.OutputFolder = update.OutputFolder;
        project.DefaultProviderId = update.DefaultProviderId;
        project.FileNamingPattern = update.FileNamingPattern;
        project.UpdatedAt = DateTime.UtcNow;
        Directory.CreateDirectory(project.OutputFolder);
        return project;
    }));
});
app.MapDelete("/api/projects/{id:int}", (int id, AppDataStore store) =>
{
    store.Write(data =>
    {
        if (data.BatchRuns.Any(run => run.ProjectId == id))
        {
            throw new InvalidOperationException("Project has batch history and cannot be deleted.");
        }

        data.Projects.RemoveAll(project => project.Id == id);
        return true;
    });
    return Results.NoContent();
});

app.MapGet("/api/providers", (AppDataStore store) => store.Read(data => data.Providers));
app.MapPost("/api/providers", (ProviderRecord provider, AppDataStore store, AppPaths paths) =>
{
    return Results.Ok(store.Write(data =>
    {
        provider.Id = data.NextProviderId++;
        provider.BrowserProfilePath = string.IsNullOrWhiteSpace(provider.BrowserProfilePath)
            ? Path.Combine(paths.BrowserProfilesFolder, AppDataStore.Slugify(provider.Name))
            : provider.BrowserProfilePath;
        provider.CreatedAt = DateTime.UtcNow;
        data.Providers.Add(provider);
        return provider;
    }));
});
app.MapPut("/api/providers/{id:int}", (int id, ProviderRecord update, AppDataStore store) =>
{
    return Results.Ok(store.Write(data =>
    {
        var provider = data.Providers.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("Provider was not found.");
        update.Id = id;
        update.CreatedAt = provider.CreatedAt;
        update.UpdatedAt = DateTime.UtcNow;
        data.Providers[data.Providers.IndexOf(provider)] = update;
        return update;
    }));
});
app.MapPost("/api/providers/{id:int}/open", async (int id, AppDataStore store, BrowserAutomationService browser) =>
{
    var provider = store.Read(data => data.Providers.SingleOrDefault(item => item.Id == id))
        ?? throw new KeyNotFoundException("Provider was not found.");
    await browser.OpenProviderAsync(provider);
    return Results.Ok(new { message = "Browser profile opened. Log in manually if the provider asks you to." });
});
app.MapPost("/api/providers/{id:int}/open-system", (int id, AppDataStore store) =>
{
    var provider = store.Read(data => data.Providers.SingleOrDefault(item => item.Id == id))
        ?? throw new KeyNotFoundException("Provider was not found.");
    if (!Uri.TryCreate(provider.StartUrl, UriKind.Absolute, out var url) ||
        url.Scheme is not ("http" or "https"))
    {
        throw new InvalidOperationException("Provider Start URL must be a valid http or https URL.");
    }

    Process.Start(new ProcessStartInfo
    {
        FileName = url.ToString(),
        UseShellExecute = true
    });
    return Results.Ok(new { message = "Opened in your normal browser. Use your logged-in Chrome tab, then return here and mark the job complete." });
});

app.MapGet("/api/chrome-profiles", (AppDataStore store) => store.Read(data => data.ChromeProfiles));
app.MapPost("/api/chrome-profiles", (ChromeProfileRecord profile, AppDataStore store, AppPaths paths) =>
{
    return Results.Ok(store.Write(data =>
    {
        profile.Id = data.NextChromeProfileId++;
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? $"Chrome Profile {profile.Id}" : profile.Name.Trim();
        profile.AccountLabel = profile.AccountLabel.Trim();
        profile.StartUrl = string.IsNullOrWhiteSpace(profile.StartUrl) ? "https://gemini.google.com/app" : profile.StartUrl.Trim();
        profile.BrowserChannel = string.IsNullOrWhiteSpace(profile.BrowserChannel) ? "chrome" : profile.BrowserChannel.Trim();
        profile.BrowserProfilePath = string.IsNullOrWhiteSpace(profile.BrowserProfilePath)
            ? Path.Combine(paths.BrowserProfilesFolder, AppDataStore.Slugify(profile.Name))
            : profile.BrowserProfilePath.Trim();
        profile.Status = string.IsNullOrWhiteSpace(profile.Status) ? "Ready" : profile.Status.Trim();
        profile.CreatedAt = DateTime.UtcNow;
        Directory.CreateDirectory(profile.BrowserProfilePath);
        data.ChromeProfiles.Add(profile);
        return profile;
    }));
});
app.MapPut("/api/chrome-profiles/{id:int}", (int id, ChromeProfileRecord update, AppDataStore store, AppPaths paths) =>
{
    return Results.Ok(store.Write(data =>
    {
        var profile = data.ChromeProfiles.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("Chrome profile was not found.");
        update.Id = id;
        update.CreatedAt = profile.CreatedAt;
        update.UpdatedAt = DateTime.UtcNow;
        update.Name = string.IsNullOrWhiteSpace(update.Name) ? profile.Name : update.Name.Trim();
        update.StartUrl = string.IsNullOrWhiteSpace(update.StartUrl) ? "https://gemini.google.com/app" : update.StartUrl.Trim();
        update.BrowserChannel = string.IsNullOrWhiteSpace(update.BrowserChannel) ? "chrome" : update.BrowserChannel.Trim();
        update.BrowserProfilePath = string.IsNullOrWhiteSpace(update.BrowserProfilePath)
            ? Path.Combine(paths.BrowserProfilesFolder, AppDataStore.Slugify(update.Name))
            : update.BrowserProfilePath.Trim();
        update.Status = string.IsNullOrWhiteSpace(update.Status) ? "Ready" : update.Status.Trim();
        Directory.CreateDirectory(update.BrowserProfilePath);
        data.ChromeProfiles[data.ChromeProfiles.IndexOf(profile)] = update;
        return update;
    }));
});
app.MapPost("/api/chrome-profiles/{id:int}/open", async (int id, AppDataStore store, BrowserAutomationService browser) =>
{
    var profile = store.Read(data => data.ChromeProfiles.SingleOrDefault(item => item.Id == id))
        ?? throw new KeyNotFoundException("Chrome profile was not found.");
    await browser.OpenChromeProfileAsync(profile);
    return Results.Ok(new { message = $"Opened {profile.Name}. Log in once, then return to ImagePilot." });
});
app.MapPost("/api/chrome-profiles/{id:int}/assign", (int id, ChromeProfileAssignRequest request, AppDataStore store) =>
{
    return Results.Ok(store.Write(data =>
    {
        var profile = data.ChromeProfiles.SingleOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException("Chrome profile was not found.");
        var provider = data.Providers.SingleOrDefault(item => item.Id == request.ProviderId) ?? throw new KeyNotFoundException("Provider was not found.");
        provider.BrowserProfilePath = profile.BrowserProfilePath;
        provider.BrowserChannel = string.IsNullOrWhiteSpace(profile.BrowserChannel) ? "chrome" : profile.BrowserChannel;
        provider.StartUrl = profile.StartUrl;
        provider.AutomationEnabled = true;
        provider.UpdatedAt = DateTime.UtcNow;
        profile.Status = "Assigned";
        profile.UpdatedAt = DateTime.UtcNow;
        return new { profile, provider };
    }));
});

app.MapPost("/api/batch-runs", (PromptGenerateRequest request, BatchQueueService queue) => Results.Ok(queue.Create(request)));
app.MapPost("/api/batch-runs/{id:int}/start", (int id, BatchQueueService queue) => Results.Ok(queue.Start(id)));
app.MapPost("/api/batch-runs/{id:int}/pause", (int id, BatchQueueService queue) => Results.Ok(queue.Pause(id)));
app.MapPost("/api/batch-runs/{id:int}/resume", (int id, BatchQueueService queue) => Results.Ok(queue.Resume(id)));
app.MapPost("/api/batch-runs/{id:int}/stop", (int id, BatchQueueService queue) => Results.Ok(queue.Stop(id)));
app.MapGet("/api/batch-runs/{id:int}/status", (int id, AppDataStore store) =>
    store.Read(data => data.BatchRuns.SingleOrDefault(run => run.Id == id)));
app.MapGet("/api/batch-runs/{id:int}/jobs", (int id, AppDataStore store) =>
    store.Read(data => data.PromptJobs.Where(job => job.BatchRunId == id).OrderBy(job => job.JobNo).ToArray()));
app.MapPost("/api/batch-runs/{runId:int}/jobs/{jobId:int}/complete", (int runId, int jobId, CompleteJobRequest request, BatchQueueService queue) =>
    Results.Ok(queue.CompleteJob(runId, jobId, request.OutputFilePath)));
app.MapPost("/api/batch-runs/{runId:int}/jobs/{jobId:int}/retry", (int runId, int jobId, BatchQueueService queue) =>
    Results.Ok(queue.RetryJob(runId, jobId)));

app.MapGet("/api/settings/sql-server", (AppDataStore store) => store.Read(data => data.SqlServer));
app.MapGet("/api/settings/persistence", (AppDataStore store, SqliteService sqlite) =>
{
    return store.Read(data => new PersistenceSettingsResult(
        string.IsNullOrWhiteSpace(data.PersistenceMode) ? "Json" : data.PersistenceMode,
        sqlite.Normalize(data.Sqlite),
        data.SqlServer));
});
app.MapPost("/api/settings/persistence/mode", (PersistenceModeSaveRequest request, SqliteService sqlite) =>
    Results.Ok(sqlite.SaveMode(request)));
app.MapGet("/api/settings/sqlite", (AppDataStore store, SqliteService sqlite) => store.Read(data => sqlite.Normalize(data.Sqlite)));
app.MapPost("/api/settings/sqlite/save", (SqliteSettingsSaveRequest request, SqliteService sqlite) =>
    Results.Ok(sqlite.Save(request)));
app.MapPost("/api/settings/sqlite/test", async (SqliteSettingsSaveRequest request, SqliteService sqlite, CancellationToken cancellationToken) =>
    Results.Ok(await sqlite.TestAsync(request.Settings, cancellationToken)));
app.MapPost("/api/settings/sqlite/initialize", async (SqliteService sqlite, CancellationToken cancellationToken) =>
    Results.Ok(await sqlite.InitializeSchemaAsync(cancellationToken)));
app.MapPost("/api/settings/sql-server/save", (SqlSettingsSaveRequest request, SqlServerService sql) =>
{
    sql.Save(request);
    return Results.Ok(new { message = "Saved. The password is stored in Windows Credential Manager." });
});
app.MapPost("/api/settings/sql-server/test", async (SqlSettingsSaveRequest request, SqlServerService sql, CancellationToken cancellationToken) =>
    Results.Ok(await sql.TestAsync(request.Settings, request.Password, cancellationToken)));
app.MapPost("/api/settings/sql-server/initialize", async (SqlServerService sql, CancellationToken cancellationToken) =>
    Results.Ok(await sql.InitializeSchemaAsync(cancellationToken)));

app.MapGet("/api/settings/prompt-ai", (AppDataStore store) => store.Read(data => data.PromptAi));
app.MapPost("/api/settings/prompt-ai/save", (PromptAiSettingsSaveRequest request, PromptAiService promptAi) =>
{
    promptAi.Save(request);
    return Results.Ok(new { message = "Saved. The API key is stored in Windows Credential Manager." });
});
app.MapPost("/api/settings/prompt-ai/test", async (PromptAiSettingsSaveRequest request, PromptAiService promptAi, CancellationToken cancellationToken) =>
    Results.Ok(await promptAi.TestAsync(request, cancellationToken)));
app.MapPost("/api/prompts/enhance", async (PromptEnhanceRequest request, PromptAiService promptAi, CancellationToken cancellationToken) =>
    Results.Ok(await promptAi.EnhanceAsync(request, cancellationToken)));
app.MapPost("/api/local-folders/list", (FolderListRequest request, FolderPickerService folderPicker) =>
    Results.Ok(folderPicker.List(request.CurrentPath)));
app.MapPost("/api/local-folders/create", (FolderCreateRequest request, FolderPickerService folderPicker) =>
    Results.Ok(folderPicker.CreateSubfolder(request.CurrentPath, request.FolderName)));

app.MapPost("/api/agent-tools/image-batch/create", (AgentCreateRequest request, AppDataStore store, AppPaths paths, BatchQueueService queue) =>
{
    var provider = store.Read(data => data.Providers.FirstOrDefault(item => item.Name.Equals(request.Provider, StringComparison.OrdinalIgnoreCase)))
        ?? throw new KeyNotFoundException("Provider was not found.");
    var project = store.Write(data =>
    {
        var existing = data.Projects.FirstOrDefault(item => item.Name.Equals(request.ProjectName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var created = new ProjectRecord
        {
            Id = data.NextProjectId++,
            Name = request.ProjectName,
            Code = AppDataStore.Slugify(request.ProjectName),
            OutputFolder = string.IsNullOrWhiteSpace(request.OutputFolder)
                ? Path.Combine(paths.ProjectsFolder, AppDataStore.Slugify(request.ProjectName), "outputs")
                : request.OutputFolder,
            DefaultProviderId = provider.Id
        };
        Directory.CreateDirectory(created.OutputFolder);
        data.Projects.Add(created);
        return created;
    });
    return Results.Ok(queue.Create(new PromptGenerateRequest(project.Id, provider.Id, request.BasePrompt, "", "{}", request.Count)));
});
app.MapPost("/api/agent-tools/image-batch/start/{id:int}", (int id, BatchQueueService queue) => Results.Ok(queue.Start(id)));
app.MapGet("/api/agent-tools/image-batch/status/{id:int}", (int id, AppDataStore store) =>
    store.Read(data => data.BatchRuns.SingleOrDefault(run => run.Id == id)));
app.MapGet("/api/agent-tools/image-batch/results/{id:int}", (int id, AppDataStore store) =>
    store.Read(data => data.PromptJobs.Where(job => job.BatchRunId == id).OrderBy(job => job.JobNo).ToArray()));

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = exception is KeyNotFoundException ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
    await context.Response.WriteAsJsonAsync(new { message = exception?.Message ?? "Unexpected error." });
}));

app.Run();
