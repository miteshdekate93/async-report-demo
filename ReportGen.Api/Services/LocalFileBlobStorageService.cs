namespace ReportGen.Api.Services;

// Saves report files to the local filesystem — perfect for demos and local development
// Switch to AzureBlobStorageService in production by changing the registration in Program.cs
public class LocalFileBlobStorageService(IConfiguration config) : IBlobStorageService
{
    // The local folder where all report files will be written (comes from appsettings.json)
    private readonly string _storagePath =
        config["BlobStorage:LocalPath"] ?? "/tmp/reports";

    // How many milliseconds to wait before saving — simulates a slow report generation process
    private readonly int _simulationDelayMs =
        config.GetValue<int>("BlobStorage:SimulationDelayMs", 10_000);

    // Write a placeholder report file to disk and return the full path to that file
    public async Task<string> SaveAsync(Guid jobId)
    {
        // Create the storage directory if it does not already exist
        Directory.CreateDirectory(_storagePath);

        // Pause here to simulate the time a real report engine (e.g. PDF generator) would take
        await Task.Delay(_simulationDelayMs);

        // Each report gets its own file named after the job ID so there are no collisions
        var filePath = Path.Combine(_storagePath, $"{jobId}.txt");

        // Write a simple text file — replace this content with real report bytes in production
        var content = $"Report for job {jobId}\nGenerated at: {DateTime.UtcNow:O}\n";
        await File.WriteAllTextAsync(filePath, content);

        // Return the full path so the job service can record it in the database
        return filePath;
    }
}
