using Google.Protobuf.WellKnownTypes;
using ProfessorAPI.Models;

namespace ProfessorAPI.Interface

{
    public interface InterProfessors
    {
   
        Task<Professor> GetProfessor(string professor_id);

        Task<bool> InsertProfessor(Professor professor);

        Task<Professor> LogIn(Professor professor);

        Task<bool> UpdateProfessor(Professor professor);
        Task<bool> DeleteProfessor(Professor professor);

        Task<Professor> SingUP(Professor professor);
     
        Task<IEnumerable<ClassRoom>> GetAllClassRooms(Professor professor);
        Task<bool> CheckAccount(Professor professor);

        Task<IEnumerable<ClassRoom>> getAllProfessorClassRooms(Professor professor);

        Task<ClassRoom> getClassRoom(string professor_id, string classroomCode);

        Task<ClassRoom> editClassRoom(string professor_id, string classCode, ClassRoom classroom);

        Task<ClassRoom> deleteClassRoom(string professor_id,string classroomCode);

        Task<IEnumerable<object>> getClassRoomEvaluation(string professor_id, string classCode);

        Task<ClassRoom> createClassRoom(string professor_id,ClassRoom classroom);


        Task<Evaluation> DeleteEvaluation(string professor_id, string classCode, string evaluation);

        Task<bool> CreateEvaluation(string professor_id, string classroomCode, Evaluation evaluation );
    
        Task<Evaluation> EditEvaluation(string professor_id, string classroomCode, Evaluation evaluation, string prev_title);

        Task<Evaluation> GetEvaluation(string professor_id, string classCode, string evaluation_title);

        Task<Grade> GetGrade(string professor_id, string classCode, string student_name, string evaluation_title);

        Task<Grade> EditGrade(string professor_id, string classCode, string student_name, string evaluation_title, Grade grade);

        Task<ClassRoom> getAboutClassRoom(string professor_id, string classroomCode);

        Task<Evaluation> getStatementEvaluation(string professor_id, string classCode, string evaluation_title);

        Task<Evaluation> getRubricEvaluation(string professor_id, string classCode, string evaluation_title);

        Task<Grade> getFeedBackGrade(string professor_id, string classCode, string student_name, string evaluation_title);
    }
}
