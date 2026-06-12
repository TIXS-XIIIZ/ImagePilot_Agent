using ImagePilot.Api.Models;

namespace ImagePilot.Api.Services;

public sealed class BatchQueueService(
    AppDataStore store,
    PromptTemplateService promptTemplateService,
    BrowserAutomationService browserAutomation,
    ILogger<BatchQueueService> logger) : BackgroundService
{
    public BatchRunRecord Create(PromptGenerateRequest request)
    {
        var prompts = promptTemplateService.Generate(request.BasePrompt, request.VariablesJson, request.Count);
        return store.Write(data =>
        {
            RequireProject(data, request.ProjectId);
            RequireProvider(data, request.ProviderId);
            var run = new BatchRunRecord
            {
                Id = data.NextBatchRunId++,
                ProjectId = request.ProjectId,
                ProviderId = request.ProviderId,
                Name = string.IsNullOrWhiteSpace(request.RunName) ? $"Batch {DateTime.Now:yyyy-MM-dd HH:mm}" : request.RunName,
                TotalJobs = prompts.Count
            };
            data.BatchRuns.Add(run);
            data.PromptJobs.AddRange(prompts.Select((prompt, index) => new PromptJobRecord
            {
                Id = data.NextPromptJobId++,
                BatchRunId = run.Id,
                ProjectId = request.ProjectId,
                ProviderId = request.ProviderId,
                JobNo = index + 1,
                Prompt = prompt,
                NegativePrompt = request.NegativePrompt,
                MaxRetry = Math.Clamp(request.MaxRetry, 0, 10)
            }));
            return run;
        });
    }

    public BatchRunRecord Start(int runId)
    {
        return store.Write(data =>
        {
            var run = RequireRun(data, runId);
            if (run.Status is RunStates.Completed or RunStates.Cancelled)
            {
                return run;
            }

            NormalizeManualWaits(data);
            var waitingJob = CurrentWaitingJob(data, run);
            if (waitingJob is not null)
            {
                run.Status = RunStates.WaitingForUser;
                run.CurrentJobId = waitingJob.Id;
                run.StartedAt ??= DateTime.UtcNow;
                return run;
            }

            run.Status = RunStates.Running;
            run.StartedAt ??= DateTime.UtcNow;
            return run;
        });
    }

    public BatchRunRecord Pause(int runId) => ChangeStatus(runId, RunStates.Paused);

    public BatchRunRecord Resume(int runId)
    {
        return store.Write(data =>
        {
            var run = RequireRun(data, runId);
            if (run.Status is RunStates.Completed or RunStates.Cancelled)
            {
                return run;
            }

            run.Status = RunStates.Running;
            NormalizeManualWaits(data);
            var waitingJob = CurrentWaitingJob(data, run);
            if (waitingJob is not null)
            {
                waitingJob.Status = JobStates.Pending;
                waitingJob.ErrorMessage = null;
                run.CurrentJobId = null;
            }

            return run;
        });
    }

    public BatchRunRecord Stop(int runId)
    {
        return store.Write(data =>
        {
            var run = RequireRun(data, runId);
            run.Status = RunStates.Cancelled;
            run.CompletedAt = DateTime.UtcNow;
            run.CurrentJobId = null;
            foreach (var job in data.PromptJobs.Where(job => job.BatchRunId == runId && job.Status is JobStates.Pending or JobStates.Retrying or JobStates.WaitingForUser))
            {
                job.Status = JobStates.Cancelled;
                job.CompletedAt = DateTime.UtcNow;
            }

            return run;
        });
    }

    public PromptJobRecord CompleteJob(int runId, int jobId, string? outputFilePath)
    {
        return store.Write(data =>
        {
            var run = RequireRun(data, runId);
            var job = data.PromptJobs.SingleOrDefault(item => item.Id == jobId && item.BatchRunId == runId)
                ?? throw new KeyNotFoundException("Job was not found.");
            MarkCompleted(data, job, outputFilePath);
            if (run.CurrentJobId == job.Id)
            {
                run.CurrentJobId = null;
            }

            RefreshRun(data, run);
            if (run.Status != RunStates.Completed)
            {
                run.Status = RunStates.Running;
            }

            return job;
        });
    }

    public PromptJobRecord RetryJob(int runId, int jobId)
    {
        return store.Write(data =>
        {
            var run = RequireRun(data, runId);
            var job = data.PromptJobs.SingleOrDefault(item => item.Id == jobId && item.BatchRunId == runId)
                ?? throw new KeyNotFoundException("Job was not found.");
            job.Status = JobStates.Retrying;
            job.ErrorMessage = null;
            run.Status = RunStates.Running;
            run.CompletedAt = null;
            RefreshRun(data, run);
            return job;
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RecoverInterruptedRuns();
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = store.Read(data =>
            {
                var run = data.BatchRuns.FirstOrDefault(item => item.Status == RunStates.Running && item.CurrentJobId is null);
                if (run is null)
                {
                    return null;
                }

                var job = data.PromptJobs.FirstOrDefault(item => item.BatchRunId == run.Id && item.Status is JobStates.Pending or JobStates.Retrying);
                var project = data.Projects.FirstOrDefault(item => item.Id == run.ProjectId);
                var provider = data.Providers.FirstOrDefault(item => item.Id == run.ProviderId);
                return job is null || project is null || provider is null ? null : new QueueItem(run, job, project, provider);
            });

            if (next is null)
            {
                FinalizeFinishedRuns();
                await Task.Delay(500, stoppingToken);
                continue;
            }

            await ProcessAsync(next, stoppingToken);
        }
    }

    private void RecoverInterruptedRuns()
    {
        store.Write(data =>
        {
            NormalizeManualWaits(data);
            foreach (var run in data.BatchRuns.Where(item => item.Status == RunStates.Running && item.CurrentJobId is not null))
            {
                var currentJobId = run.CurrentJobId.GetValueOrDefault();
                var job = data.PromptJobs.FirstOrDefault(item => item.Id == currentJobId);
                if (job is null || job.Status != JobStates.Running)
                {
                    run.CurrentJobId = null;
                    continue;
                }

                job.Status = JobStates.WaitingForUser;
                job.ErrorMessage = "Recovered from an interrupted browser automation run. Use your prepared browser tab, then mark this job complete.";
                run.Status = RunStates.WaitingForUser;
                run.CurrentJobId = job.Id;
            }

            NormalizeManualWaits(data);
            return true;
        });
    }

    private async Task ProcessAsync(QueueItem item, CancellationToken cancellationToken)
    {
        store.Write(data =>
        {
            var run = RequireRun(data, item.Run.Id);
            var job = data.PromptJobs.Single(record => record.Id == item.Job.Id);
            run.CurrentJobId = job.Id;
            job.Status = JobStates.Running;
            job.StartedAt ??= DateTime.UtcNow;
            return job;
        });

        var result = await browserAutomation.SubmitAsync(item.Project, item.Provider, item.Job);
        store.Write(data =>
        {
            var run = RequireRun(data, item.Run.Id);
            var job = data.PromptJobs.Single(record => record.Id == item.Job.Id);
            run.CurrentJobId = null;
            if (result.Status == JobStates.Completed)
            {
                MarkCompleted(data, job, result.OutputFilePath);
            }
            else if (result.Status == JobStates.WaitingForUser)
            {
                job.Status = JobStates.WaitingForUser;
                job.ErrorMessage = result.Message;
                run.Status = RunStates.WaitingForUser;
                run.CurrentJobId = job.Id;
            }
            else if (job.RetryCount < job.MaxRetry)
            {
                run.CurrentJobId = null;
                job.Status = JobStates.Retrying;
                job.RetryCount++;
                job.ErrorMessage = result.Message;
            }
            else
            {
                run.CurrentJobId = null;
                job.Status = JobStates.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = result.Message;
            }

            NormalizeManualWaits(data);
            RefreshRun(data, run);
            return job;
        });

        logger.LogInformation("Processed job {JobId} with status {Status}", item.Job.Id, result.Status);
        if (item.Provider.DelayBetweenJobsSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(item.Provider.DelayBetweenJobsSeconds), cancellationToken);
        }
    }

    private BatchRunRecord ChangeStatus(int runId, string status, bool setStartedAt = false)
    {
        return store.Write(data =>
        {
            var run = RequireRun(data, runId);
            if (run.Status is RunStates.Completed or RunStates.Cancelled)
            {
                return run;
            }

            run.Status = status;
            if (setStartedAt)
            {
                run.StartedAt ??= DateTime.UtcNow;
            }

            return run;
        });
    }

    private void FinalizeFinishedRuns()
    {
        store.Write(data =>
        {
            NormalizeManualWaits(data);
            foreach (var run in data.BatchRuns.Where(item => item.Status == RunStates.Running && item.CurrentJobId is null))
            {
                RefreshRun(data, run);
            }

            return true;
        });
    }

    private static void MarkCompleted(AppData data, PromptJobRecord job, string? outputFilePath)
    {
        job.Status = JobStates.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return;
        }

        job.OutputFilePath = outputFilePath;
        job.OutputFileName = Path.GetFileName(outputFilePath);
        var info = new FileInfo(outputFilePath);
        data.OutputFiles.RemoveAll(file => file.PromptJobId == job.Id);
        data.OutputFiles.Add(new OutputFileRecord
        {
            Id = data.NextOutputFileId++,
            PromptJobId = job.Id,
            FilePath = outputFilePath,
            FileName = Path.GetFileName(outputFilePath),
            FileSizeBytes = info.Exists ? info.Length : null
        });
    }

    private static void RefreshRun(AppData data, BatchRunRecord run)
    {
        var jobs = data.PromptJobs.Where(job => job.BatchRunId == run.Id).ToArray();
        run.CompletedJobs = jobs.Count(job => job.Status == JobStates.Completed);
        run.FailedJobs = jobs.Count(job => job.Status == JobStates.Failed);
        if (jobs.Length > 0 && jobs.All(job => job.Status is JobStates.Completed or JobStates.Failed or JobStates.Cancelled or JobStates.Skipped))
        {
            run.Status = RunStates.Completed;
            run.CompletedAt = DateTime.UtcNow;
            run.CurrentJobId = null;
        }
    }

    private static PromptJobRecord? CurrentWaitingJob(AppData data, BatchRunRecord run)
    {
        var waitingJobs = data.PromptJobs
            .Where(job => job.BatchRunId == run.Id && job.Status == JobStates.WaitingForUser)
            .OrderBy(job => job.JobNo)
            .ToArray();
        if (waitingJobs.Length == 0)
        {
            return null;
        }

        return waitingJobs.FirstOrDefault(job => job.Id == run.CurrentJobId) ?? waitingJobs[0];
    }

    private static void NormalizeManualWaits(AppData data)
    {
        foreach (var run in data.BatchRuns.Where(run => run.Status is not (RunStates.Completed or RunStates.Cancelled)))
        {
            if (run.CurrentJobId is not null)
            {
                var currentJob = data.PromptJobs.FirstOrDefault(job => job.Id == run.CurrentJobId);
                if (currentJob is null || currentJob.Status is not (JobStates.Running or JobStates.WaitingForUser))
                {
                    run.CurrentJobId = null;
                }
            }

            var waitingJobs = data.PromptJobs
                .Where(job => job.BatchRunId == run.Id && job.Status == JobStates.WaitingForUser)
                .OrderBy(job => job.JobNo)
                .ToArray();
            if (waitingJobs.Length == 0)
            {
                if (run.Status == RunStates.WaitingForUser)
                {
                    run.Status = RunStates.Running;
                }

                continue;
            }

            var activeJob = waitingJobs.FirstOrDefault(job => job.Id == run.CurrentJobId) ?? waitingJobs[0];
            foreach (var extraJob in waitingJobs.Where(job => job.Id != activeJob.Id))
            {
                extraJob.Status = JobStates.Pending;
                extraJob.ErrorMessage = null;
                extraJob.StartedAt = null;
            }

            run.Status = RunStates.WaitingForUser;
            run.CurrentJobId = activeJob.Id;
        }
    }

    private static BatchRunRecord RequireRun(AppData data, int runId) =>
        data.BatchRuns.SingleOrDefault(run => run.Id == runId) ?? throw new KeyNotFoundException("Batch run was not found.");

    private static ProjectRecord RequireProject(AppData data, int projectId) =>
        data.Projects.SingleOrDefault(project => project.Id == projectId) ?? throw new KeyNotFoundException("Project was not found.");

    private static ProviderRecord RequireProvider(AppData data, int providerId) =>
        data.Providers.SingleOrDefault(provider => provider.Id == providerId) ?? throw new KeyNotFoundException("Provider was not found.");

    private sealed record QueueItem(BatchRunRecord Run, PromptJobRecord Job, ProjectRecord Project, ProviderRecord Provider);
}
