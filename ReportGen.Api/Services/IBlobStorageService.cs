namespace ReportGen.Api.Services;

// Common contract for all file storage backends (local disk, Azure, S3, etc.)
// Swap implementations in Program.cs without changing any other code
public interface IBlobStorageService
{
    // Save a report file for the given job and return the path or URL where it was saved
    Task<string> SaveAsync(Guid jobId);
}
