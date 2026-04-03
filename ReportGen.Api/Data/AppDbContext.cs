using Microsoft.EntityFrameworkCore;
using ReportGen.Api.Models;

namespace ReportGen.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportJob>(entity =>
        {
            entity.HasKey(j => j.JobId);
            entity.Property(j => j.ReportType).IsRequired().HasMaxLength(100);
            entity.Property(j => j.SignalRConnectionId).IsRequired().HasMaxLength(256);
        });
    }
}
