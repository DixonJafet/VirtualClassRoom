namespace ProfessorAPI.Models
{
    public class Evaluation
    {
        public string? evaluation_id { get; set; }
        public string? classroomCode { get; set; }
        public string? title { get; set; }
        public float value { get; set; }
        private DateOnly _DueDate;
        public DateTime dueDate { get; set; } 
        public string? subject { get; set; }
        public string? statement { get; set; }
        public string? rubric { get; set; }

        public IFormFile? statementFile { get; set; }
        public IFormFile? rubricFile { get; set; }
    }
}