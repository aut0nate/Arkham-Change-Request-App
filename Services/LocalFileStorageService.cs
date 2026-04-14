namespace ArkhamChangeRequest.Services
{
    public class LocalFileStorageService : IBlobStorageService
    {
        private const string LocalSchemePrefix = "local://";

        private readonly string _rootPath;
        private readonly ILogger<LocalFileStorageService> _logger;

        public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
        {
            _logger = logger;

            var configuredPath = configuration["Storage:LocalPath"];
            _rootPath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads")
                : ResolveAbsolutePath(configuredPath);

            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> UploadFileAsync(IFormFile file, string containerName = "attachments")
        {
            var containerPath = Path.Combine(_rootPath, containerName);
            Directory.CreateDirectory(containerPath);

            var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var destinationPath = Path.Combine(containerPath, safeFileName);

            await using var fileStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(fileStream);

            _logger.LogInformation("Saved local attachment to {Path}", destinationPath);
            return $"{LocalSchemePrefix}{containerName}/{safeFileName}";
        }

        public Task<bool> DeleteFileAsync(string blobUrl)
        {
            try
            {
                var filePath = ResolveStoragePath(blobUrl);
                var exists = File.Exists(filePath);
                if (exists)
                {
                    File.Delete(filePath);
                }

                return Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting local attachment {BlobUrl}", blobUrl);
                return Task.FromResult(false);
            }
        }

        public Task<Stream?> DownloadFileAsync(string blobUrl)
        {
            try
            {
                var filePath = ResolveStoragePath(blobUrl);
                if (!File.Exists(filePath))
                {
                    return Task.FromResult<Stream?>(null);
                }

                Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return Task.FromResult<Stream?>(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening local attachment {BlobUrl}", blobUrl);
                return Task.FromResult<Stream?>(null);
            }
        }

        public string GetBlobNameFromUrl(string blobUrl)
        {
            return Path.GetFileName(ResolveStoragePath(blobUrl));
        }

        private string ResolveStoragePath(string blobUrl)
        {
            if (!blobUrl.StartsWith(LocalSchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported local file URL format: {blobUrl}");
            }

            var relativePath = blobUrl[LocalSchemePrefix.Length..]
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(_rootPath, relativePath);
        }

        private static string ResolveAbsolutePath(string path)
        {
            return Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path);
        }
    }
}
