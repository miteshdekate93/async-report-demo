namespace ReportGen.Api.Services;

public interface IReportStorageService
{
    /// <summary>Simulates report generation and returns the saved file path.</summary>
    Task<string> SaveReportAsync(Guid jobId);
}
