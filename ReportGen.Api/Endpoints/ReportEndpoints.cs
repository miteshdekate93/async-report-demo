using System.Security.Claims;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ReportGen.Api.Data;
using ReportGen.Api.Jobs;
using ReportGen.Api.Services;

namespace ReportGen.Api.Endpoints;

// All HTTP endpoints related to requesting and downloading reports
public static class ReportEndpoints
{
    // Register both report endpoints on the application's route table
    public static void MapReportEndpoints(this WebApplication app)
    {
        // Any authenticated user can request a new report
        app.MapPost("/api/reports", HandleGenerateReport)
           .RequireAuthorization();

        // Download requires auth so the ownership check in the handler can verify the requester
        app.MapGet("/api/reports/{jobId:guid}/download", HandleDownloadReport)
           .RequireAuthorization();
    }

    // Validate the request, create a job record, queue it in Hangfire, and return 202 immediately
    private static async Task<IResult> HandleGenerateReport(
        HttpContext ctx,
        GenerateReportRequest request,
        IReportJobService reportService,
        IBackgroundJobClient jobClient)
    {
        // Validate inputs before touching the database
        if (string.IsNullOrWhiteSpace(request.ReportType) || request.ReportType.Length > 100)
            return Results.BadRequest("ReportType must be between 1 and 100 characters.");

        if (string.IsNullOrWhiteSpace(request.SignalRConnectionId) || request.SignalRConnectionId.Length > 256)
            return Results.BadRequest("SignalRConnectionId must be between 1 and 256 characters.");

        var userId = GetUserIdFromClaims(ctx.User);
        if (userId is null)
            return Results.Unauthorized();

        var jobId = await reportService.CreateJobAsync(
            userId.Value,
            request.ReportType,
            request.SignalRConnectionId);

        jobClient.Enqueue<ReportGenerationJob>(j => j.ExecuteAsync(jobId));

        return Results.Accepted($"/api/reports/{jobId}", new { JobId = jobId });
    }

    // Verify ownership, prevent path traversal, then stream the finished report file to the browser
    private static async Task<IResult> HandleDownloadReport(
        Guid jobId,
        HttpContext ctx,
        AppDbContext db,
        IConfiguration config)
    {
        var userId = GetUserIdFromClaims(ctx.User);
        if (userId is null)
            return Results.Unauthorized();

        var job = await db.ReportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        if (job?.BlobPath is null)
            return Results.NotFound("Report not ready or the file could not be found.");

        // Ownership check — only the user who created the job can download its report
        if (job.UserId != userId.Value)
            return Results.Forbid();

        // Path traversal prevention — resolve both paths and confirm the file is inside the storage root
        var storageRoot = Path.GetFullPath(config["BlobStorage:LocalPath"] ?? "/tmp/reports");
        var resolvedPath = Path.GetFullPath(job.BlobPath);
        if (!resolvedPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("Invalid file path.");

        if (!File.Exists(resolvedPath))
            return Results.NotFound("Report file not found on disk.");

        var fileBytes = await File.ReadAllBytesAsync(resolvedPath);
        return Results.File(fileBytes, "text/plain", $"report-{jobId}.txt");
    }

    // Pull the logged-in user's numeric ID out of their authentication claims
    private static int? GetUserIdFromClaims(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}

// The data the client must send when requesting a new report
public record GenerateReportRequest(string ReportType, string SignalRConnectionId);
