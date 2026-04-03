import { Component, OnDestroy, computed, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { SignalRService } from '../../services/signalr.service';
import { ReportService } from '../../services/report.service';
import { environment } from '../../../environments/environment';

// The four possible states the button can be in.
// The template switches between completely different UIs based on this value.
type ReportState = 'idle' | 'loading' | 'ready' | 'failed';

// The "Generate Report" button with built-in loading spinner and download link.
// It manages the full async flow: click → API call → wait for SignalR → show result.
@Component({
  selector: 'app-report-button',
  standalone: true,
  templateUrl: './report-button.html',
  styleUrl: './report-button.css'
})
export class ReportButton implements OnDestroy {

  protected readonly state = signal<ReportState>('idle');
  protected readonly downloadUrl = signal<string | null>(null);
  protected readonly isConnected = computed(() => this.signalR.connectionId() !== null);
  protected readonly reportTypes = ['Monthly', 'Quarterly', 'Annual'];
  protected readonly selectedType = signal<string>('Monthly');

  private reportSubscription: Subscription | null = null;

  constructor(
    private readonly signalR: SignalRService,
    private readonly reportService: ReportService
  ) {}

  // Update the selected report type when the user changes the dropdown.
  protected onTypeChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedType.set(select.value);
  }

  // Called when the user clicks the "Generate Report" button.
  async onGenerateClick(): Promise<void> {
    const connectionId = this.signalR.connectionId();
    if (!connectionId) return;

    this.state.set('loading');
    this.downloadUrl.set(null);

    try {
      const response = await this.reportService.generateReport({
        reportType: this.selectedType(),
        signalRConnectionId: connectionId
      });

      // Subscribe for the one SignalR push that belongs to this specific job.
      this.reportSubscription = this.signalR
        .waitForReport(response.jobId)
        .subscribe(payload => this.onReportResult(payload));

    } catch {
      this.state.set('failed');
    }
  }

  // Called once when the server pushes a result (success or failure) for our job.
  private onReportResult(payload: { jobId: string; downloadUrl: string }): void {
    if (!payload.downloadUrl) {
      this.state.set('failed');
    } else {
      // Prepend the API origin so the browser can fetch the file directly.
      this.downloadUrl.set(`${environment.apiUrl}${payload.downloadUrl}`);
      this.state.set('ready');
    }

    this.reportSubscription?.unsubscribe();
    this.reportSubscription = null;
  }

  // Reset to the initial state so the user can generate another report.
  protected onReset(): void {
    this.state.set('idle');
    this.downloadUrl.set(null);
  }

  ngOnDestroy(): void {
    this.reportSubscription?.unsubscribe();
  }
}
