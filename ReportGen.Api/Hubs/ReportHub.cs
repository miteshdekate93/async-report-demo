using Microsoft.AspNetCore.SignalR;

namespace ReportGen.Api.Hubs;

public class ReportHub : Hub<IReportClient>
{
}
