import { Injectable, OnDestroy, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { filter, take } from 'rxjs/operators';
import { ReportReadyPayload } from '../models/report-job.model';
import { environment } from '../../environments/environment';

// Manages the persistent WebSocket connection to the server's SignalR hub.
// Created once for the whole app — Angular's "providedIn: root" ensures a single instance.
@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {

  // The ID that SignalR assigned to this browser tab's connection.
  // We send this to the API so the server knows exactly where to push the result.
  readonly connectionId = signal<string | null>(null);

  // Internal stream that receives every "ReportReady" and "ReportFailed" event from the server.
  // Components subscribe to the slice they care about using waitForReport(jobId).
  private readonly allReportEvents$ = new Subject<ReportReadyPayload>();

  // The active WebSocket connection object — created once, kept alive with auto-reconnect.
  private readonly connection: signalR.HubConnection;

  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .build();

    // When the server calls "ReportReady", forward the payload into the shared stream.
    this.connection.on('ReportReady', (payload: ReportReadyPayload) => {
      this.allReportEvents$.next(payload);
    });

    // When the server calls "ReportFailed", emit a failed payload so the component can react.
    // We use an empty downloadUrl as the signal that something went wrong.
    this.connection.on('ReportFailed', (jobId: string) => {
      this.allReportEvents$.next({ jobId, downloadUrl: '' });
    });

    this.startConnection();
  }

  // Returns an Observable that emits exactly once when the server sends a result for this job.
  waitForReport(jobId: string) {
    return this.allReportEvents$.pipe(
      filter(payload => payload.jobId === jobId),
      take(1)
    );
  }

  // Open the WebSocket connection and capture the connection ID once it is established.
  // Retries with exponential backoff up to 5 attempts before giving up.
  private async startConnection(attempt = 1): Promise<void> {
    const maxRetries = 5;
    try {
      await this.connection.start();
      this.connectionId.set(this.connection.connectionId);
    } catch (err) {
      if (attempt < maxRetries) {
        // Double the wait time on each retry, capped at 16 seconds
        const delayMs = Math.min(1000 * Math.pow(2, attempt - 1), 16_000);
        setTimeout(() => this.startConnection(attempt + 1), delayMs);
      } else {
        console.error('[SignalR] Could not connect after', maxRetries, 'attempts:', err);
      }
    }
  }

  // Close the WebSocket when Angular tears down this service (e.g. app shutdown).
  ngOnDestroy(): void {
    this.connection.stop();
  }
}
