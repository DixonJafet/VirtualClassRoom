using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ProfessorAPI.Connexions
{
    public class BlobService
    {
        private readonly string _storageFolder;

        public BlobService(IConfiguration config, IWebHostEnvironment env)
        {
            var configuredPath = config["FileStorage:Path"] ?? config["FileDB:Path"];
            if (!string.IsNullOrEmpty(configuredPath))
            {
                if (Path.IsPathRooted(configuredPath) && Directory.Exists(configuredPath))
                {
                    _storageFolder = configuredPath;
                }
                else
                {
                    _storageFolder = Path.GetFullPath(Path.Combine(env.ContentRootPath, configuredPath));
                }
            }
            else
            {
                var candidates = new[]
                {
                    Path.Combine(env.ContentRootPath, "ClassRoomFiles"),
                    Path.Combine(AppContext.BaseDirectory, "ClassRoomFiles"),
                    Path.Combine(env.ContentRootPath, "..", "ClassRoomFiles"),
                    Path.Combine(Directory.GetCurrentDirectory(), "ClassRoomFiles"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "ClassRoomFiles")
                };

                var found = candidates.FirstOrDefault(Directory.Exists);
                _storageFolder = found != null
                    ? Path.GetFullPath(found)
                    : Path.Combine(env.ContentRootPath, "ClassRoomFiles");
            }

            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }
        }

        /// <summary>
        /// Uploads a file to the hoster file storage (ClassRoomFiles) and returns its unique name.
        /// </summary>
        public async Task<string> UploadFileAsync(IFormFile file)
        {
            if (!Directory.Exists(_storageFolder))
            {
                Directory.CreateDirectory(_storageFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}00_PAPI_{file.FileName}";
            var filePath = Path.Combine(_storageFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream);
            }

            return uniqueFileName;
        }

        /// <summary>
        /// Retrieves a file stream from the hoster file storage (ClassRoomFiles).
        /// </summary>
        public Task<Stream?> GetFileAsync(string blobName)
        {
            var filePath = Path.Combine(_storageFolder, blobName);

            if (!File.Exists(filePath))
            {
                var candidatePaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "ClassRoomFiles", blobName),
                    Path.Combine(Directory.GetCurrentDirectory(), "ClassRoomFiles", blobName),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "ClassRoomFiles", blobName)
                };

                var alt = candidatePaths.FirstOrDefault(File.Exists);
                if (alt != null) filePath = alt;
            }

            if (!File.Exists(filePath))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(stream);
        }

        /// <summary>
        /// Deletes a file from the hoster file storage (ClassRoomFiles).
        /// </summary>
        public Task DeleteFileAsync(string blobName)
        {
            var filePath = Path.Combine(_storageFolder, blobName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }
    }
}
