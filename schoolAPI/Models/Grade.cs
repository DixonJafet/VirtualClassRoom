namespace ProfessorAPI.Models
{
    public class Grade
    {

        public float? grade { get; set; }

        public IFormFile? feedbackFile
        { get; set; }


        public string? feedback { get; set; }

        public float? max { get; set; }

    }
}
