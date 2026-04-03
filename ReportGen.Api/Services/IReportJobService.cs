namespace ReportGen.Api.Services;

public interface IReportJobService
{
    Task<Guid> CreateJobAsync(int userId, string reportType, string signalRConnectionId);
    Task ExecuteJobAsync(Guid jobId);
}
