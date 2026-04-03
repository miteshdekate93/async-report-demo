namespace ReportGen.Api.Services;

// Stores report files in Azure Blob Storage — use this in production deployments
// To activate: add Azure.Storage.Blobs NuGet package and set the AzureStorage connection string
public class AzureBlobStorageService(IConfiguration config) : IBlobStorageService
{
    // The name of the Azure container where report files will be uploaded (from appsettings.json)
    private readonly string _containerName =
        config["BlobStorage:AzureContainerName"] ?? "reports";

    // The full Azure Storage connection string — read from config, NEVER hardcoded in source
    private readonly string _connectionString =
        config.GetConnectionString("AzureStorage")
        ?? throw new InvalidOperationException(
            "AzureStorage connection string is required. " +
            "Add it to appsettings.json under ConnectionStrings:AzureStorage.");

    // Upload the report to Azure Blob Storage and return the public URL of the uploaded blob
    public Task<string> SaveAsync(Guid jobId)
    {
        // TODO: Implement this when deploying to Azure
        // Step 1: Install package — dotnet add package Azure.Storage.Blobs
        // Step 2: var containerClient = new BlobContainerClient(_connectionString, _containerName);
        // Step 3: await containerClient.CreateIfNotExistsAsync();
        // Step 4: var blobClient = containerClient.GetBlobClient($"{jobId}.txt");
        // Step 5: await blobClient.UploadAsync(BinaryData.FromString(content));
        // Step 6: return blobClient.Uri.ToString();
        throw new NotImplementedException(
            "Azure Blob Storage is not yet wired up. " +
            "Install Azure.Storage.Blobs and implement this method.");
    }
}
