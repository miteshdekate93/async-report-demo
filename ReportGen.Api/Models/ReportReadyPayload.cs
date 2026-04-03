namespace ReportGen.Api.Models;

public record ReportReadyPayload(Guid JobId, string DownloadUrl);
