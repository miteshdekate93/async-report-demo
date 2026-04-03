using System.Net;
using System.Net.Http.Json;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReportGen.Tests.Helpers;

namespace ReportGen.Tests.Endpoints;

public class ReportsEndpointTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> BuildFactory(
        Mock<IBackgroundJobClient>? jobClientMock = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace JWT with always-pass test scheme
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                            TestAuthHandler.SchemeName, _ => { });

                    // Inject mock Hangfire client if provided
                    if (jobClientMock is not null)
                        services.AddSingleton(jobClientMock.Object);
                });
            });
    }

    // ── POST /api/reports ────────────────────────────────────────────────────

    [Fact]
    public async Task PostReport_ValidRequest_Returns202WithJobId()
    {
        // Arrange
        await using var factory = BuildFactory();
        var client = factory.CreateClient();

        var request = new
        {
            ReportType = "Monthly",
            SignalRConnectionId = "conn-abc"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/reports", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GenerateReportResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.JobId);
    }

    [Fact]
    public async Task PostReport_ValidRequest_EnqueuesJobToHangfireQueue()
    {
        // Arrange
        var jobClientMock = new Mock<IBackgroundJobClient>();
        jobClientMock
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns(Guid.NewGuid().ToString());

        await using var factory = BuildFactory(jobClientMock);
        var client = factory.CreateClient();

        var request = new
        {
            ReportType = "Monthly",
            SignalRConnectionId = "conn-abc"
        };

        // Act
        await client.PostAsJsonAsync("/api/reports", request);

        // Assert — Hangfire's Enqueue extension calls Create() under the hood
        jobClientMock.Verify(
            c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()),
            Times.Once);
    }

    // ── response DTO (mirrors what the endpoint will return) ─────────────────

    private sealed record GenerateReportResponse(Guid JobId);
}
