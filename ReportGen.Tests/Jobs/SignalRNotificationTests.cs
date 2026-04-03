using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReportGen.Api.Data;
using ReportGen.Api.Hubs;
using ReportGen.Api.Models;
using ReportGen.Api.Services;

namespace ReportGen.Tests.Jobs;

public class SignalRNotificationTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ExecuteJobAsync_WhenComplete_SendsReportReadyToCorrectConnectionId()
    {
        // Arrange
        const string connectionId = "signalr-conn-xyz";

        await using var db = CreateInMemoryDb();

        var job = new ReportJob
        {
            JobId = Guid.NewGuid(),
            UserId = 1,
            ReportType = "Monthly",
            SignalRConnectionId = connectionId,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.ReportJobs.Add(job);
        await db.SaveChangesAsync();

        var clientMock = new Mock<IReportClient>();
        clientMock
            .Setup(c => c.ReportReady(It.IsAny<ReportReadyPayload>()))
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients<IReportClient>>();
        clientsMock
            .Setup(c => c.Client(connectionId))
            .Returns(clientMock.Object);

        var hubContextMock = new Mock<IHubContext<ReportHub, IReportClient>>();
        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var storageMock = new Mock<IReportStorageService>();
        storageMock
            .Setup(s => s.SaveReportAsync(job.JobId))
            .ReturnsAsync($"reports/{job.JobId}.txt");

        var service = new ReportJobService(
            db,
            hubContextMock.Object,
            storageMock.Object,
            NullLogger<ReportJobService>.Instance);

        // Act
        await service.ExecuteJobAsync(job.JobId);

        // Assert — hub must target the exact connection ID that was stored with the job
        clientsMock.Verify(c => c.Client(connectionId), Times.Once);

        // Assert — the correct payload is sent
        clientMock.Verify(
            c => c.ReportReady(It.Is<ReportReadyPayload>(p => p.JobId == job.JobId)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteJobAsync_WhenFailed_SendsReportFailedNotification()
    {
        // Arrange
        const string connectionId = "signalr-conn-xyz";

        await using var db = CreateInMemoryDb();

        var job = new ReportJob
        {
            JobId = Guid.NewGuid(),
            UserId = 1,
            ReportType = "Monthly",
            SignalRConnectionId = connectionId,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.ReportJobs.Add(job);
        await db.SaveChangesAsync();

        var clientMock = new Mock<IReportClient>();
        clientMock.Setup(c => c.ReportFailed(It.IsAny<Guid>())).Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients<IReportClient>>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(clientMock.Object);

        var hubContextMock = new Mock<IHubContext<ReportHub, IReportClient>>();
        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        // Force storage to throw so the job enters the failure path
        var storageMock = new Mock<IReportStorageService>();
        storageMock
            .Setup(s => s.SaveReportAsync(job.JobId))
            .ThrowsAsync(new IOException("Disk full"));

        var service = new ReportJobService(
            db,
            hubContextMock.Object,
            storageMock.Object,
            NullLogger<ReportJobService>.Instance);

        // Act
        await service.ExecuteJobAsync(job.JobId);

        // Assert — ReportReady must never be sent on failure
        clientMock.Verify(c => c.ReportReady(It.IsAny<ReportReadyPayload>()), Times.Never);

        // Assert — ReportFailed must be sent so the user knows the job did not complete
        clientMock.Verify(c => c.ReportFailed(job.JobId), Times.Once);
    }
}
