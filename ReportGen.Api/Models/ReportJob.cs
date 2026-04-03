namespace ReportGen.Api.Models;

public class ReportJob
{
    public Guid JobId { get; set; }
    public int UserId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public ReportStatus Status { get; set; }
    public string? BlobPath { get; set; }
    public string SignalRConnectionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
