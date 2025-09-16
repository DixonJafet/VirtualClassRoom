namespace ProfessorAPI.Models
{
    public class Professor
    {
        public  string? professor_id { get; set; }
        public  string? name { get; set; }
        public int? phone { get; set; }

        public string? password {  get; set; }

        public string? email { get; set; }

        public string? area_of_expertise { get; set; }

    }
}
