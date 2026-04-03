# Async Report Generation Demo

A full-stack demo showing how to handle long-running tasks without blocking the user.

The user clicks **Generate Report**, the API responds immediately with a job ID, and a real-time push notification arrives ~10 seconds later with a download link — no polling required.

---

## Architecture

```
Angular (localhost:4200)        ASP.NET Core API (localhost:5000)       Hangfire Worker
        |                                    |                                |
        |── 1. Connect SignalR ─────────────>|                                |
        |<─ connectionId ──────────────────---|                                |
        |                                    |                                |
        |── 2. POST /api/reports ───────────>|── 3. Insert ReportJob ──> DB   |
        |   { reportType,                    |── 4. Enqueue job ──────────────>|
        |     signalRConnectionId }          |                                |
        |<─ { jobId } 202 Accepted ────────---|                         5. Execute
        |                                    |                         6. Wait 10s
        |  [spinner shows]                   |                         7. Write file
        |                                    |                         8. Update DB
        |                                    |<── 9. SignalR push ─────────────|
        |<─ ReportReady({ jobId,             |
        |     downloadUrl }) ─────────────---|
        |  [download link shows]             |
```

**Key design decision:** Angular connects to SignalR and captures the `connectionId` *before* clicking Generate. That ID is stored on the job record so Hangfire can push to the right browser tab even if the connection changes.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Angular 19 (standalone components, Signals) |
| API | ASP.NET Core 9 Minimal APIs |
| Background Jobs | Hangfire (InMemory for dev, SQL Server for prod) |
| Real-time Push | ASP.NET Core SignalR (typed hub) |
| Database | EF Core 9 (InMemory for dev, SQL Server for prod) |
| File Storage | Local filesystem for dev, Azure Blob stub for prod |
| Testing | xUnit + Moq + WebApplicationFactory |

---

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- [Node.js 20+](https://nodejs.org/)
- [Angular CLI](https://angular.io/cli): `npm install -g @angular/cli`

### Run the API

```bash
cd ReportGen.Api
dotnet run
# API available at http://localhost:5000
# Hangfire dashboard at http://localhost:5000/hangfire
# Swagger UI at http://localhost:5000/swagger
```

### Run the Angular App

```bash
cd report-gen-ui
npm install
ng serve
# App available at http://localhost:4200
```

### Try It

1. Open `http://localhost:4200` in your browser
2. Wait for **"Connecting to server…"** to disappear (SignalR ready)
3. Select a report type and click **Generate Report**
4. Watch the spinner — the API returns immediately
5. After ~10 seconds the spinner is replaced by a **Download Report** link
6. Check `http://localhost:5000/hangfire` to see job history

---

## Project Structure

```
async-report-demo/
├── ReportGen.Api/                     # ASP.NET Core API
│   ├── Auth/                          # DevAuthHandler (dev), JWT (prod)
│   ├── Data/                          # EF Core DbContext
│   ├── Endpoints/                     # Minimal API route handlers
│   ├── Hubs/                          # SignalR typed hub
│   ├── Jobs/                          # Hangfire background job
│   ├── Models/                        # ReportJob entity, enums, payloads
│   └── Services/                      # ReportJobService, BlobStorageService
├── ReportGen.Tests/                   # xUnit integration + unit tests
│   ├── Endpoints/                     # WebApplicationFactory endpoint tests
│   ├── Helpers/                       # TestAuthHandler
│   ├── Jobs/                          # SignalR notification tests
│   └── Services/                      # ReportJobService unit tests
└── report-gen-ui/                     # Angular frontend
    └── src/app/
        ├── components/report-button/  # 4-state UI: idle → loading → ready/failed
        ├── models/                    # TypeScript interfaces
        └── services/                  # SignalRService, ReportService
```

---

## Running Tests

```bash
dotnet test
# 8 tests — all pass
```

Test coverage includes:

- `CreateJobAsync` saves to DB and returns a job ID
- Status transitions: Pending → Processing → Completed / Failed
- SignalR hub is called with the correct connection ID on success
- SignalR `ReportFailed` is sent on job failure
- `POST /api/reports` returns 202 with a job ID
- `POST /api/reports` enqueues a Hangfire job

---

## Configuration

| Key | Default | Notes |
|---|---|---|
| `Cors:AllowedOrigin` | `http://localhost:4200` | Angular dev server origin |
| `BlobStorage:LocalPath` | `/tmp/reports` | Where report `.txt` files are saved |
| `BlobStorage:SimulationDelayMs` | `10000` | Simulated generation time (ms) |
| `JwtSettings:Secret` | *(placeholder)* | Required in production only |

In development, authentication is bypassed by `DevAuthHandler` (always authenticates as `userId=1`). No login flow is needed to run the demo.

---

## License

MIT
