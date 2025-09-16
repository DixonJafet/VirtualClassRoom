using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using ProfessorAPI.Connexions;
using ProfessorAPI.Models;
using System.Diagnostics.Eventing.Reader;
using System.IO;



namespace ProfessorAPI.Controllers
{
    public class FileController
    {


        private readonly MySqlConnection dbconnection;


        public FileController()
        {

         

        }

        protected MySqlConnection getdbConnection()
        {
            return dbconnection;
        }

        public string CreateFileLink(IFormFile file, string Code)
        {
            var filePath = "";
            if (file != null)
            {
                var storagePath = Path.Combine("D:\\Programers\\Diseño_web\\SPA\\SchoolFiles", Code.ToString());
                Directory.CreateDirectory(storagePath);
                var uniqueFileName = $"{Guid.NewGuid()}00_{file?.FileName}";
                filePath = Path.Combine(storagePath, uniqueFileName);
            }
            return filePath;
        }

        // Refactored: Change async void to async Task
        public async Task SaveFile(IFormFile file, string filePath)
        {
            if (file != null)
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                     await file.CopyToAsync(stream);
                }
            }
        }

        public void DeleteFile(string Path)
        {


            string filePath = @Path;

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Console.WriteLine("File deleted successfully.");
            }
            else
            {
                Console.WriteLine("File not found.");
            }

        }

        public FileStreamResult GetFileStreamResult(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            string contentType;
            if (!provider.TryGetContentType(filePath, out contentType))
                contentType = "application/octet-stream";

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileName = Path.GetFileName(filePath).Split("00_")[1]; 

            return new FileStreamResult(stream, contentType)
            {
                FileDownloadName = fileName
            };
        }

        public static FileStreamResult getFile(string filePath)
        {
            var fileController = new FileController();
            return fileController.GetFileStreamResult(filePath);
        }


        public static string getFileName(string filePath)
        {
            try
            {
                return filePath?.Split("00_")[1];
            }
            catch {
                return "";
            }
        }












    }
}
