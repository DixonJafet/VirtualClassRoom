using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using ProfessorAPI.Connexions;
using ProfessorAPI.Models;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using static Org.BouncyCastle.Math.EC.ECCurve;



namespace ProfessorAPI.Controllers
{
    public class FileController
    {

        private readonly IConfiguration? config;
        private readonly MySqlConnection? dbconnection;
        private readonly BlobService _blobService;

        // Constructor modified to use dependency injection
        public FileController(IConfiguration? configuration, BlobService blobService)
        {
            config = configuration;
            _blobService = blobService;
        }

        protected MySqlConnection? getdbConnection()
        {
            return dbconnection;
        }

        // Adapted to upload to Blob Storage and return the unique blob name
        public async Task<string> CreateFileLink(IFormFile file, string Code)
        {
            if (file != null)
            {
                // The storage path is no longer a physical path, but a container.
                // The new unique name will serve as the file link.
                var uniqueBlobName = await _blobService.UploadFileAsync(file);
                return uniqueBlobName;
            }
            return "";
        }

        // Adapted to delete from Blob Storage
        public async Task DeleteFile(string blobName)
        {
            await _blobService.DeleteFileAsync(blobName);
            Console.WriteLine("File deleted successfully.");
        }

        // Adapted to get the file stream from Blob Storage
        public async Task<FileStreamResult> GetFileStreamResult(string blobName)
        {
            var stream = await _blobService.GetFileAsync(blobName);

            if (stream == null)
                throw new FileNotFoundException("File not found.", blobName);

            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            string contentType;
            if (!provider.TryGetContentType(getFileName(blobName), out contentType))
                contentType = "application/octet-stream";

            var fileName = getFileName(blobName);

            return new FileStreamResult(stream, contentType)
            {
                FileDownloadName = fileName
            };
        }

        // Updated to be an async method
        public static async Task<FileStreamResult> getFile(FileController fileController, string blobName)
        {
            return await fileController.GetFileStreamResult(blobName);
        }

        // This method remains the same, as it only parses the string
        public static string getFileName(string blobName)
        {
            try
            {
                // Assuming your old naming convention of `GUID00_FileName.ext` is still used
                return blobName?.Split("00_PAPI_")[1];
            }
            catch
            {
                return "";
            }
        }


    }
}
