using ProfessorAPI.Binding;
using System.Text.Json;

namespace ProfessorAPI.Models
{
    public class ClassRoom
    {
        public string? classroomCode { get; set; }
        public string? courseName { get; set; }
        public string? about { get; set; }
        public IFormFile? aboutFile { get; set; }

        [FromFormJson]
        public IEnumerable<Schedule>? schedule { get; set; }

    }
}
