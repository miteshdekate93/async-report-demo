// The body sent to POST /api/reports when the user requests a new report
export interface GenerateReportRequest {
  reportType: string;
  // The user's SignalR connection ID so the server knows exactly where to push the result
  signalRConnectionId: string;
}

// The body returned by POST /api/reports — the job ID is used to match the SignalR push
export interface GenerateReportResponse {
  jobId: string;
}

// The SignalR push message the server sends when a report finishes (success or failure)
export interface ReportReadyPayload {
  jobId: string;
  // A relative URL like /api/reports/{id}/download — empty string means the job failed
  downloadUrl: string;
}
