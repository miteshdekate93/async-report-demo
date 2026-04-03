using ReportGen.Api.Services;

namespace ReportGen.Api.Jobs;

// A Hangfire background job that runs the full report generation pipeline
// Hangfire deserializes this class and calls ExecuteAsync when the job is dequeued
public class ReportGenerationJob(IReportJobService reportJobService)
{
    // Entry point called by Hangfire — hands off to the service for all the real work
    public Task ExecuteAsync(Guid jobId)
    {
        // The service handles: Processing status → save file → Completed/Failed → SignalR notify
        return reportJobService.ExecuteJobAsync(jobId);
    }
}
