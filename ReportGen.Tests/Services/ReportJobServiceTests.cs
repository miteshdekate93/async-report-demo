using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReportGen.Api.Data;
using ReportGen.Api.Hubs;
using ReportGen.Api.Models;
using ReportGen.Api.Services;

namespace ReportGen.Tests.Services;

public class ReportJobServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static ReportJobService CreateService(
        AppDbContext db,
        IHubContext<ReportHub, IReportClient>? hubContext = null,
        IReportStorageService? storageService = null)
    {
        hubContext ??= new Mock<IHubContext<ReportHub, IReportClient>>().Object;
        storageService ??= new Mock<IReportStorageService>().Object;
        return new ReportJobService(db, hubContext, storageService, NullLogger<ReportJobService>.Instance);
    }

    // ── CreateJobAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateJobAsync_ValidRequest_SavesJobToDbAndReturnsJobId()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var service = CreateService(db);

        // Act
        var jobId = await service.CreateJobAsync(
            userId: 42,
            reportType: "Monthly",
            signalRConnectionId: "conn-abc");

        // Assert — returned ID must exist in DB
        var savedJob = await db.ReportJobs.FindAsync(jobId);
        Assert.NotNull(savedJob);
        Assert.Equal(42, savedJob.UserId);
        Assert.Equal("Monthly", savedJob.ReportType);
        Assert.Equal("conn-abc", savedJob.SignalRConnectionId);
        Assert.Equal(ReportStatus.Pending, savedJob.Status);
        Assert.NotEqual(Guid.Empty, savedJob.JobId);
    }

    // ── ExecuteJobAsync — status transitions ─────────────────────────────────

    [Fact]
    public async Task ExecuteJobAsync_DuringExecution_StatusChangesToProcessing()
    {
        // Arrange
        await using var db = CreateInMemoryDb();

        var job = new ReportJob
        {
            JobId = Guid.NewGuid(),
            UserId = 1,
            ReportType = "Monthly",
            SignalRConnectionId = "conn-abc",
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.ReportJobs.Add(job);
        await db.SaveChangesAsync();

        bool processingStatusObserved = false;

        // Spy: check DB state the moment the storage service is called —
        // the job should already be in Processing by then.
        var storageMock = new Mock<IReportStorageService>();
        storageMock
            .Setup(s => s.SaveReportAsync(job.JobId))
            .Returns(async () =>
            {
                var snapshot = await db.ReportJobs
                    .AsNoTracking()
                    .FirstAsync(j => j.JobId == job.JobId);
                processingStatusObserved = snapshot.Status == ReportStatus.Processing;
                return $"reports/{job.JobId}.txt";
            });

        var hubMock = new Mock<IHubContext<ReportHub, IReportClient>>();
        var clientMock = new Mock<IReportClient>();
        var clientsMock = new Mock<IHubClients<IReportClient>>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(clientMock.Object);
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var service = CreateService(db, hubMock.Object, storageMock.Object);

        // Act
        await service.ExecuteJobAsync(job.JobId);

        // Assert — intermediate Processing state was observed
        Assert.True(processingStatusObserved,
            "Status should be Processing while the report is being generated.");
    }

    [Fact]
    public async Task ExecuteJobAsync_WhenCompleted_SetsStatusToCompletedWithBlobPath()
    {
        // Arrange
        await using var db = CreateInMemoryDb();

        var job = new ReportJob
        {
            JobId = Guid.NewGuid(),
            UserId = 1,
            ReportType = "Monthly",
            SignalRConnectionId = "conn-abc",
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.ReportJobs.Add(job);
        await db.SaveChangesAsync();

        var expectedPath = $"reports/{job.JobId}.txt";
        var storageMock = new Mock<IReportStorageService>();
        storageMock
            .Setup(s => s.SaveReportAsync(job.JobId))
            .ReturnsAsync(expectedPath);

        var hubMock = new Mock<IHubContext<ReportHub, IReportClient>>();
        var clientMock = new Mock<IReportClient>();
        var clientsMock = new Mock<IHubClients<IReportClient>>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(clientMock.Object);
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var service = CreateService(db, hubMock.Object, storageMock.Object);

        // Act
        await service.ExecuteJobAsync(job.JobId);

        // Assert
        var updatedJob = await db.ReportJobs.AsNoTracking().FirstAsync(j => j.JobId == job.JobId);
        Assert.Equal(ReportStatus.Completed, updatedJob.Status);
        Assert.Equal(expectedPath, updatedJob.BlobPath);
    }

    [Fact]
    public async Task ExecuteJobAsync_WhenStorageThrows_SetsStatusToFailed()
    {
        // Arrange
        await using var db = CreateInMemoryDb();

        var job = new ReportJob
        {
            JobId = Guid.NewGuid(),
            UserId = 1,
            ReportType = "Monthly",
            SignalRConnectionId = "conn-abc",
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.ReportJobs.Add(job);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IReportStorageService>();
        storageMock
            .Setup(s => s.SaveReportAsync(job.JobId))
            .ThrowsAsync(new IOException("Disk full"));

        var service = CreateService(db, storageService: storageMock.Object);

        // Act — should NOT throw; service must catch and mark Failed
        await service.ExecuteJobAsync(job.JobId);

        // Assert
        var updatedJob = await db.ReportJobs.AsNoTracking().FirstAsync(j => j.JobId == job.JobId);
        Assert.Equal(ReportStatus.Failed, updatedJob.Status);
    }
}
