using System.Net;
using Hangfire.Dashboard;

namespace ReportGen.Api.Auth;

// Restricts the Hangfire dashboard to loopback connections (localhost) only.
// Replace with a role-based check before deploying to a shared or production environment.
public class HangfireLocalDashboardFilter : IDashboardAuthorizationFilter
{
    // Returns true only when the request originates from the local machine.
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        return remoteIp is not null && IPAddress.IsLoopback(remoteIp);
    }
}
