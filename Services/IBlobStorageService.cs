namespace ArkhamChangeRequest.Services
{
    public interface IBlobStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string containerName = "attachments");
        Task<bool> DeleteFileAsync(string blobUrl);
        Task<Stream?> DownloadFileAsync(string blobUrl);
        string GetBlobNameFromUrl(string blobUrl);
    }
}
