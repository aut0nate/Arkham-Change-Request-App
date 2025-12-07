using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ArkhamChangeRequest.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
        {
            _blobServiceClient = blobServiceClient;
            _logger = logger;
        }

        public async Task<string> UploadFileAsync(IFormFile file, string containerName = "attachments")
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var blobName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var blobClient = containerClient.GetBlobClient(blobName);

                var blobHttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                };

                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders
                });

                _logger.LogInformation("File uploaded successfully: {BlobName}", blobName);
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string blobUrl)
        {
            try
            {
                var blobClient = GetBlobClient(blobUrl);
                var response = await blobClient.DeleteIfExistsAsync();

                _logger.LogInformation("File deletion result for {BlobUrl}: {Result}", blobUrl, response.Value);
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {BlobUrl}", blobUrl);
                return false;
            }
        }

        public async Task<Stream?> DownloadFileAsync(string blobUrl)
        {
            try
            {
                var blobClient = GetBlobClient(blobUrl);
                var response = await blobClient.DownloadStreamingAsync();
                return response.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {BlobUrl}", blobUrl);
                return null;
            }
        }

        public string GetBlobNameFromUrl(string blobUrl)
        {
            var uri = new Uri(blobUrl);
            return Path.GetFileName(uri.LocalPath);
        }

        private BlobClient GetBlobClient(string blobUrl)
        {
            var builder = new BlobUriBuilder(new Uri(blobUrl));
            var containerClient = _blobServiceClient.GetBlobContainerClient(builder.BlobContainerName);
            return containerClient.GetBlobClient(builder.BlobName);
        }
    }
}
