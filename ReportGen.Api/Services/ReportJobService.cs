using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ReportGen.Api.Data;
using ReportGen.Api.Hubs;
using ReportGen.Api.Models;

namespace ReportGen.Api.Services;

// Orchestrates the full lifecycle of a report job: creation, execution, and user notification
public class ReportJobService(
    AppDbContext db,
    IHubContext<ReportHub, IReportClient> hubContext,
    IReportStorageService storageService,
    ILogger<ReportJobService> logger) : IReportJobService
{
    // Create a brand-new job record in the database and return its ID to the caller
    public async Task<Guid> CreateJobAsync(int userId, string reportType, string signalRConnectionId)
    {
        var newJob = new ReportJob
        {
            JobId = Guid.NewGuid(),
            UserId = userId,
            ReportType = reportType,
            SignalRConnectionId = signalRConnectionId,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.ReportJobs.Add(newJob);
        await db.SaveChangesAsync();

        return newJob.JobId;
    }

    // Run the full report pipeline: mark as Processing → save file → mark Complete → notify user
    public async Task ExecuteJobAsync(Guid jobId)
    {
        // Load a fresh read-only snapshot so we have the user's SignalR connection ID
        var job = await FindJobOrThrowAsync(jobId);

        try
        {
            await SetStatusAsync(jobId, ReportStatus.Processing);
            var blobPath = await storageService.SaveReportAsync(jobId);
            await SetCompletedAsync(jobId, blobPath);

            // Best-effort: a SignalR hiccup does not undo the completed job status
            try { await NotifySuccessAsync(job, blobPath); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SignalR success notification failed for job {JobId}", jobId);
            }
        }
        catch (Exception ex)
        {
            // Record the failure so the database stays consistent regardless of notification outcome
            logger.LogError(ex, "Report generation failed for job {JobId}", jobId);
            await SetStatusAsync(jobId, ReportStatus.Failed);

            // Best-effort: notify the user so they are not stuck on the spinner forever
            try { await NotifyFailureAsync(job); }
            catch (Exception notifyEx)
            {
                logger.LogWarning(notifyEx, "SignalR failure notification failed for job {JobId}", jobId);
            }
        }
    }

    // Look up a job by ID and throw a clear error if it does not exist
    private async Task<ReportJob> FindJobOrThrowAsync(Guid jobId)
    {
        var job = await db.ReportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        return job ?? throw new InvalidOperationException($"Job {jobId} not found in the database.");
    }

    // Update only the Status field — uses change tracking so it works with both InMemory and SQL Server
    private async Task SetStatusAsync(Guid jobId, ReportStatus newStatus)
    {
        var job = await db.ReportJobs.FindAsync(jobId);
        if (job is null) return;

        job.Status = newStatus;
        await db.SaveChangesAsync();
    }

    // Record that the job has finished and store where the report file was saved
    private async Task SetCompletedAsync(Guid jobId, string blobPath)
    {
        var job = await db.ReportJobs.FindAsync(jobId);
        if (job is null) return;

        job.Status = ReportStatus.Completed;
        job.BlobPath = blobPath;
        await db.SaveChangesAsync();
    }

    // Send a SignalR message to the user's browser with the link to download their report
    private async Task NotifySuccessAsync(ReportJob job, string blobPath)
    {
        var downloadUrl = $"/api/reports/{job.JobId}/download";
        var payload = new ReportReadyPayload(job.JobId, downloadUrl);
        await hubContext.Clients.Client(job.SignalRConnectionId).ReportReady(payload);
    }

    // Send a SignalR failure message so the user knows the job did not complete
    private async Task NotifyFailureAsync(ReportJob job)
    {
        await hubContext.Clients.Client(job.SignalRConnectionId).ReportFailed(job.JobId);
    }
}
