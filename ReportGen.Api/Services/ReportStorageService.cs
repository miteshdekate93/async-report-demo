namespace ReportGen.Api.Services;

// Connects the report job pipeline to whichever blob storage backend is configured
// This thin wrapper lets you swap local vs. Azure storage without touching ReportJobService
public class ReportStorageService(IBlobStorageService blobStorage) : IReportStorageService
{
    // Forward the save request to whichever blob storage implementation is currently wired up
    public Task<string> SaveReportAsync(Guid jobId) => blobStorage.SaveAsync(jobId);
}
