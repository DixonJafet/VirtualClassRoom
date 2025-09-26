

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;


namespace ProfessorAPI.Connexions
{
    public class BlobService
    {
        private readonly IConfiguration? config;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        public BlobService(BlobServiceClient blobServiceClient)
        {
            _containerName = config["BlobStorage:ContainerName"] ?? "files";
            _blobServiceClient = blobServiceClient;
        }

        /// <summary>
        /// Uploads a file to Azure Blob Storage and returns its unique name.
        /// </summary>
        /// <param name="file">The file to upload.</param>
        /// <returns>The unique name of the uploaded blob.</returns>
        public async Task<string> UploadFileAsync(IFormFile file)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // Generate a unique name for the blob to prevent overwrites
            var uniqueFileName = $"{Guid.NewGuid()}00_PAPI_{file.FileName}";
            var blobClient = containerClient.GetBlobClient(uniqueFileName);

            using (var stream = file.OpenReadStream())
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
            }

            return uniqueFileName;
        }

        /// <summary>
        /// Retrieves a file as a stream from Azure Blob Storage.
        /// </summary>
        /// <param name="blobName">The unique name of the blob to retrieve.</param>
        /// <returns>A Stream of the file content or null if not found.</returns>
        public async Task<Stream> GetFileAsync(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
            {
                return null; // File not found
            }

            return await blobClient.OpenReadAsync();
        }

        /// <summary>
        /// Deletes a file from Azure Blob Storage.
        /// </summary>
        /// <param name="blobName">The unique name of the blob to delete.</param>
        public async Task DeleteFileAsync(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }


    }
}
