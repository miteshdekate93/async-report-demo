using ReportGen.Api.Models;

namespace ReportGen.Api.Hubs;

// Messages the server can push to a specific browser client over SignalR
public interface IReportClient
{
    // Tell the user their report finished and give them the download link
    Task ReportReady(ReportReadyPayload payload);

    // Tell the user something went wrong so they are not left waiting forever
    Task ReportFailed(Guid jobId);
}
