using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReportGen.Api.Auth;
using ReportGen.Api.Data;
using ReportGen.Api.Endpoints;
using ReportGen.Api.Hubs;
using ReportGen.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────

// Use an in-memory database for local development so no SQL Server setup is needed
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseInMemoryDatabase("ReportGenDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection connection string is required.");

    builder.Services.AddDbContext<AppDbContext>(opts =>
        opts.UseSqlServer(connectionString));
}

// ── Hangfire (background job queue) ─────────────────────────────────────────

// Use an in-memory job store for development — jobs vanish on restart but no SQL needed
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHangfire(config =>
        config.UseInMemoryStorage());
}
else
{
    var hangfireConnection = builder.Configuration.GetConnectionString("HangfireConnection")
        ?? throw new InvalidOperationException("HangfireConnection connection string is required.");

    builder.Services.AddHangfire(config =>
        config.UseSqlServerStorage(hangfireConnection));
}

// Start the Hangfire worker that picks up queued jobs and runs them in the background
builder.Services.AddHangfireServer();

// ── SignalR (real-time push notifications) ───────────────────────────────────

builder.Services.AddSignalR();

// ── CORS ─────────────────────────────────────────────────────────────────────

// AllowCredentials() is required for SignalR — cannot be combined with AllowAnyOrigin()
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy
            .WithOrigins(
                builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

// ── Authentication & Authorization ───────────────────────────────────────────

if (builder.Environment.IsDevelopment())
{
    // Dev bypass: always authenticates as userId=1 so the demo works without a login flow.
    // The download endpoint uses this to pass the ownership check in development.
    builder.Services.AddAuthentication(DevAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(
            DevAuthHandler.SchemeName, _ => { });
}
else
{
    // Production: validate JWT tokens signed with the configured secret
    var secret = builder.Configuration["JwtSettings:Secret"]
        ?? throw new InvalidOperationException("JwtSettings:Secret is required in production.");
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["JwtSettings:Audience"],
            };
        });
}

builder.Services.AddAuthorization();

// ── Application services ─────────────────────────────────────────────────────

builder.Services.AddScoped<IReportJobService, ReportJobService>();
builder.Services.AddScoped<IReportStorageService, ReportStorageService>();

if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IBlobStorageService, LocalFileBlobStorageService>();
else
    builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── Build the app ─────────────────────────────────────────────────────────────

var app = builder.Build();

// UseRouting must come before UseCors so CORS headers are applied to routed endpoints
// including the SignalR /negotiate handshake. Without this the negotiate request is rejected.
app.UseRouting();
app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

// Hangfire dashboard — restricted to localhost connections only via HangfireLocalDashboardFilter
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireLocalDashboardFilter()]
});

// Do NOT use RequireCors here — it doesn't handle OPTIONS preflight requests which SignalR needs.
// The global app.UseCors() above (positioned after UseRouting) covers the negotiate handshake.
app.MapHub<ReportHub>("/hubs/report");
app.MapReportEndpoints();

app.Run();

// Required so WebApplicationFactory can find the entry point in integration tests
public partial class Program { }
