import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';

export const appConfig: ApplicationConfig = {
  providers: [
    // Catches unhandled errors globally and logs them to the console
    provideBrowserGlobalErrorListeners(),

    // Makes HttpClient available for injection across the whole app
    // ReportService uses this to POST to /api/reports
    provideHttpClient(),
  ]
};
