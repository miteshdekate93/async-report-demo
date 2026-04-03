import { Component } from '@angular/core';
import { ReportButton } from './components/report-button/report-button';

// Root component — renders the page shell and mounts the report generator card.
// SignalR connects automatically when the app starts because SignalRService
// is injected into ReportButton and Angular creates it on first use.
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ReportButton],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
