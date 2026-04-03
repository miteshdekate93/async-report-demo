import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { GenerateReportRequest, GenerateReportResponse } from '../models/report-job.model';
import { environment } from '../../environments/environment';

// Handles all HTTP calls to the report generation API.
// Injectable at root level so the same instance is shared across all components.
@Injectable({ providedIn: 'root' })
export class ReportService {

  // The base URL of the backend API — configured per environment in src/environments/.
  private readonly apiBase = environment.apiUrl;

  constructor(private readonly http: HttpClient) {}

  // Send a POST request to queue a new report and return the job ID immediately.
  // The API responds with 202 Accepted — the actual report arrives later via SignalR.
  async generateReport(request: GenerateReportRequest): Promise<GenerateReportResponse> {
    return firstValueFrom(
      this.http.post<GenerateReportResponse>(`${this.apiBase}/api/reports`, request)
    );
  }
}
