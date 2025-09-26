using Dapper;
using Google.Protobuf.WellKnownTypes;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509.Qualified;
using ProfessorAPI.Interface;
using ProfessorAPI.Models;
using ProfessorAPI.Connexions;
using System.Runtime.InteropServices;
using BC = BCrypt.Net.BCrypt;


namespace ProfessorAPI.Controllers
{
    
    public class ProfessorsController : InterProfessors
    {
        class Verify { public int Password_verification { get; set; } };
    
        private readonly MySqlConnection dbconnection;
        private FileController FileHandler;
     

        public ProfessorsController(MySqlConnection mySqlConnection)
        {

            dbconnection = mySqlConnection;
            FileHandler = new FileController();
            
        }

        protected MySqlConnection getdbConnection()
        {
            return dbconnection;
        } 
        public async Task<bool> DeleteProfessor(Professor professor )
        {
            var request = @"DELETE FROM professors WHERE id = @Id";

            var result = await getdbConnection().ExecuteAsync(request, new { Id = professor.professor_id });
            return result>0;
        }



        public  Task<Professor> GetProfessor(string professor_id)
        {   
            var request = @"CALL GETProfessorInfo(@professor_id); ";
            var result = getdbConnection().QueryFirstOrDefaultAsync<Professor>(request, new { professor_id });
            result.Result.password = "";
            return result;
        }


        public async Task<bool> UpdateProfessor(Professor professor)
        {
            var DB = getdbConnection();
            var request = @"CALL EDITProfessorInfo(@professor_id,
									@name,
									@phone,
									@area_of_expertise,
                                    @email,
                                    @password
                                    )";
            var result = await DB.ExecuteAsync(request, new {   professor.professor_id,
                                                                professor.name,
                                                                professor.phone,
                                                                professor.area_of_expertise,
                                                                professor.email,
                                                                password = BC.HashPassword(professor.password, 6)   

            });

            return result > 0;
        }

        public async Task<Professor> SingUP(Professor professor)
        {
            if (Exist(professor).Result) return new Professor { professor_id = "0" };
            var DB = getdbConnection();
            var request = @"INSERT INTO professors(full_name,password,phone,Area_of_expertise,email) 
                            values (@Name,@Password,@phone,@area_of_expertise,@email)";
            //var salt = BC.GenerateSalt(6);
            var new_professor = new Professor{   
                                        name = professor.name,
                                        password = BC.HashPassword(professor.password,6),
                                        phone = professor.phone,
                                        area_of_expertise = professor.area_of_expertise,
                                        email = professor.email
            };

            var singup_result = await DB.ExecuteAsync(request, new_professor);
            var request_id = @"SELECT professor_id FROM professors WHERE email = @email";
            var request_result = await DB.QueryFirstOrDefaultAsync<Professor>(request_id, new { professor.email });

            Professor result_professor = new Professor { 
                            professor_id = request_id
            };
            return result_professor;
        }

        public async Task<Professor> LogIn(Professor professor)
        {
            var DB = getdbConnection();
            var request = @"Select *, full_name as name from professors where email = @email";
            var result = await DB.QueryFirstOrDefaultAsync<Professor>(request, new { professor.email });
            Professor professor_logged= result;

            if(professor_logged == null)
            {
                return new Professor
                {
                    professor_id = "0"
                };
            }
            var password = professor_logged.password;
            var name = professor_logged.name;
            Console.WriteLine(result.email);

            
            var salt = BC.GenerateSalt(6);
            bool verify = BC.Verify(professor.password,password);

            Console.WriteLine(verify);
            if (verify) {
                Console.WriteLine(password);
                return new Professor {
                    name = professor_logged.name,
                    professor_id = professor_logged.professor_id
                };
            }
            return new Professor
            {
                professor_id = "0"
            };
        }

        public async Task<bool> InsertProfessor(Professor professor) { return true;}

        public async Task<bool> Exist(Professor professor)
        {
            var DB = getdbConnection();
            var query = @"SELECT COUNT(*) FROM professors WHERE email = @email";
            var result = await DB.QuerySingleAsync<int>(query, new { professor.email });
            int count = Convert.ToInt32(result);
            return count > 0;
        }
        public async Task<IEnumerable<ClassRoom>> GetAllClassRooms(Professor professor) {

            var DB = getdbConnection();
         //   var query = ""


            return null;
        
        
        }

        public async Task<bool> CheckAccount(Professor professor)
        {

            var DB = getdbConnection();
            var query  = @"SELECT COUNT(*) FROM professors WHERE professor_id = @professor_id";
            var result = await DB.QuerySingleAsync<int>(query, new { professor.professor_id });
            Console.WriteLine(result);
  
            return result > 0;
        }


        public async Task<IEnumerable<ClassRoom>> getAllProfessorClassRooms(Professor professor)
        {

            var DB = getdbConnection();
            var query = @"CALL GetProfessorClassrooms(@professor_id)";
            var classrooms = await DB.QueryAsync<ClassRoom>(query, new { professor.professor_id });
            foreach (var prev_classroom in classrooms)
            {
                try
                {
                    prev_classroom.about = prev_classroom.about.Split("00_")[1];
                }
                catch { }
            }
            //  lia newClassrooms = classrooms.ToList<ClassRoom>();
            var classroomsWithSchedule = classrooms.Select(
                    classroom =>
                    {
                        var query = @"CALL GetClassroomSchedules(@classroomCode);";
                        var result =  DB.QueryAsync<Schedule>(query, new { classroom.classroomCode }).Result;
                        return new ClassRoom
                        {
                            classroomCode = classroom.classroomCode,
                            courseName = classroom.courseName,
                            about = classroom.about,
                            schedule = result
                        };
                    }             
            );
            return classroomsWithSchedule;
        }


        public async Task<ClassRoom> getClassRoom(string professor_id, string classroomCode)
        {
            var DB = getdbConnection();
            var query = @"Select courseName,classroomCode,about from classrooms where professor_id = @professor_id and classroomCode = @classroomCode";
            var result = await DB.QueryFirstOrDefaultAsync<ClassRoom>(query, new{
                                                                                    professor_id,
                                                                                    classroomCode
                                                                                });

            if (result == null) return null;

            var schedule_query = @"CALL GetClassroomSchedules(@classroomCode);";
            var schedule_result =  await DB.QueryAsync<Schedule>(schedule_query, new { classroomCode });
           // Console.WriteLine(schedule_result);

            result.schedule = schedule_result;

            result.about = FileController.getFileName(result?.about);
            return result;

        }

        public async Task<ClassRoom> editClassRoom(string professor_id, string classroomCode, ClassRoom classroom)
        {
            var DB = getdbConnection();
            var query = @"call editclassroom(@professor_id,@classroomCode,@courseName,@about)";
            string Path = FileHandler.CreateFileLink(classroom.aboutFile, classroomCode);
            classroom.about = Path;
            var result = await DB.QueryFirstOrDefaultAsync<ClassRoom>(query, new
            {
                professor_id,
                classroomCode,
                classroom.courseName,
                classroom.about,

            });
            var reset_query = @"call deleteClassroomScd(@classroomCode)";
            var result_reset = await DB.ExecuteAsync(reset_query, new
            {
                classroomCode
            });
            IEnumerable<Schedule> schedules = classroom.schedule;
            var schedule_query = @"call InsertClassRoomScd(@classroomCode,@new_day,@new_from_time,@new_to_time)";
            if (classroom.schedule != null)
            {
                foreach (Schedule schedule in schedules)
                {
                    var time = schedule.time.Split("-");
                    var from_time = time[0];
                    var to_time = time[1];
                    var resultsch = await DB.ExecuteAsync(schedule_query, new
                    {
                        classroomCode,
                        new_day = schedule.day,
                        new_from_time = from_time,
                        new_to_time = to_time
                    });
                }
            }
            
            FileHandler.DeleteFile(result.about);
            return result;
        }

        public async Task<ClassRoom> deleteClassRoom(string professor_id, string classroomCode)
        {

            var DB = getdbConnection();
            var query = @"call DeleteClassroom(@classroomCode,@professor_id)";
            var result = await DB.QueryFirstOrDefaultAsync<ClassRoom>(query, new
            {
                professor_id,
                classroomCode
            });



            FileHandler.DeleteFile(result.about);

            return result;
        }


        public async Task<IEnumerable<object>> getClassRoomEvaluation(string professor_id, string classCode)
        {
            var DB = getdbConnection();

            var evaluation_query = @"call getEvaluationsClassRoom(@classCode,@professor_id);
";
            var evaluation_Results = await DB.QueryAsync<object>(evaluation_query, 
                                                                                    new { classCode,
                                                                                           professor_id

                                                                                    });
            // Console.WriteLine(schedule_result);

            if (evaluation_Results == null)
            {
            }

            return evaluation_Results; 

        
     
        }
        public async Task<ClassRoom> createClassRoom(string professor_id, ClassRoom classroom)
        {
            var DB = getdbConnection();
            // Insert classroom
            var insertQuery = @"CALL CreateClassroom(@professor_id, @courseName)";
            
            var result = await DB.QueryFirstOrDefaultAsync<ClassRoom>(insertQuery, new
            {
                professor_id,
                classroom.courseName,

            });
            Console.Write(result.classroomCode);

            string about = await FileHandler.CreateFileLink(classroom.aboutFile, result.classroomCode);
            var aboutQuery = @"CALL SetAboutFile(@classroomCode, @about)";

            var result_with_doc = await DB.QueryFirstOrDefaultAsync<ClassRoom>(aboutQuery, new
            {
                result.classroomCode,
                about

            });

            


            if (classroom.schedule != null )
            {
                var scheduleQuery = @"CALL InsertClassRoomScd(@classroomCode, @new_day, @new_from_time, @new_to_time)";
                foreach (var schedule in classroom.schedule)
                {
                    if (string.IsNullOrEmpty(schedule.time)) continue;
                    var timeParts = schedule.time.Split('-');
                    if (timeParts.Length != 2) continue;
                    var from_time = timeParts[0];
                    var to_time = timeParts[1];

                    await DB.ExecuteAsync(scheduleQuery, new
                    {
                        result.classroomCode,
                        new_day = schedule.day,
                        new_from_time = from_time,
                        new_to_time = to_time
                    });
                }
            }

            // Get schedules for the created classroom
            var getScheduleQuery = @"CALL GetClassroomSchedules(@classroomCode);";
            var schedules = await DB.QueryAsync<Schedule>(getScheduleQuery, new { result.classroomCode });
            result.schedule = schedules;
            result.about = classroom.about;
            
          //  Console.WriteLine(classroom.schedule);
            return result;

        }
      
        public async Task<Evaluation> DeleteEvaluation(string professor_id, string classCode, string evaluation)
        {
            var DB = getdbConnection();
            var query = @"call DeleteEvaluation(@professor_id,@classCode,@evaluation)";
            var result = await DB.QueryFirstOrDefaultAsync<Evaluation>(query, new
            {
                professor_id,
                classCode,
                evaluation
            });


            FileHandler?.DeleteFile(result.rubric);
            FileHandler?.DeleteFile(result.statement);

            return result;
        }

        public async Task<bool> CreateEvaluation(string professor_id,string classroomCode,Evaluation evaluation)
                    {

                string statement = await FileHandler.CreateFileLink(evaluation.statementFile, classroomCode);
                string rubric = await FileHandler.CreateFileLink(evaluation.rubricFile, classroomCode);

                var DB = getdbConnection();
                        var query = @"CALL CreateEvaluation(
                                    @professor_id,
                                    @classroomCode,
                                    @Title,
                                    @Value,
                                    @DueDate,
                                    @Subject,
                                    @Statement,
                                    @Rubric
                                )";

                        var result = await DB.ExecuteAsync(query, new
                        {
                            professor_id,
                            classroomCode,
                            evaluation.title,
                            evaluation.value,
                            evaluation.dueDate,
                            evaluation.subject,
                            statement,
                            rubric
                        });


            return result > 0;
        }


        public async Task<Evaluation> EditEvaluation(string professor_id, string classroomCode, Evaluation evaluation,string prev_title)
        {

            string statement = await FileHandler.CreateFileLink(evaluation.statementFile, classroomCode);
            string rubric = await FileHandler.CreateFileLink(evaluation.rubricFile, classroomCode);

            var DB = getdbConnection();
            var query = @"CALL EDITEvaluationInfo(
        
                            @professor_id,
                            @classroomCode,
                            @title,
                            @value,
                            @dueDate,
                            @subject,
                            @statement,
                            @rubric,
                            @prev_title
                        )";

            var result = await DB.QueryFirstOrDefaultAsync<Evaluation>(query, new
            {
                professor_id,
                classroomCode,
                evaluation.title,
                evaluation.value,
                evaluation.dueDate,
                evaluation.subject,
                statement,
                rubric,
                prev_title
            });

            FileHandler?.DeleteFile(result.rubric);
            FileHandler?.DeleteFile(result.statement);

            return result;
        }

        public async Task<Evaluation> GetEvaluation(string professor_id, string classCode, string evaluation_title)
        {
            var DB = getdbConnection();
            var query = @"CALL GetEvaluationInfo(@professor_id,@classCode,@evaluation_title)";

            var evaluation = await DB.QueryFirstOrDefaultAsync<Evaluation>(query, new
            {
                evaluation_title,
                classCode,
                professor_id
            });

            evaluation.rubric = FileController.getFileName(evaluation.rubric);
            evaluation.statement = FileController.getFileName(evaluation.statement);
            return evaluation;
        }

        public async Task<Grade> GetGrade(string professor_id, string classCode, string student_name, string evaluation_title)
        {
            var DB = getdbConnection();
            var query = @"CALL GetGradeInfo(@professor_id, @classCode, @student_name, @evaluation_title)";
            var result = await DB.QueryFirstOrDefaultAsync<Grade>(query, new
            {
                professor_id,
                classCode,
                student_name,
                evaluation_title
            });


            result.feedback = FileController.getFileName(result.feedback);

            return result;
        }

        public async Task<Grade> EditGrade(string professor_id, string classCode, string student_name, string evaluation_title, Grade grade)
        {

            string feedback = await FileHandler.CreateFileLink(grade.feedbackFile, classCode);
           
            var DB = getdbConnection();
            var query = @"CALL EditGradeInfo(@professor_id, @classCode, @student_name, @evaluation_title, @grade, @feedback)";
            var result = await DB.QueryFirstOrDefaultAsync<Grade>(query, new
            {
                professor_id,
                classCode,
                student_name,
                evaluation_title,
                grade.grade,
                feedback
            });
            FileHandler.DeleteFile(result.feedback);

         

            return result ;


        }



        public async Task<ClassRoom> getAboutClassRoom(string professor_id, string classroomCode)
        {
            var DB = getdbConnection();
            var query = @"Select courseName,classroomCode,about from classrooms where professor_id = @professor_id and classroomCode = @classroomCode";
            var result = await DB.QueryFirstOrDefaultAsync<ClassRoom>(query, new
            {
                professor_id,
                classroomCode
            });

            return result;
        }

        public async Task<Evaluation> getStatementEvaluation(string professor_id, string classCode, string evaluation_title)
        {
            var DB = getdbConnection();
            var query = @"CALL GetEvaluationInfo(@professor_id,@classCode,@evaluation_title)";

            var evaluation = await DB.QueryFirstOrDefaultAsync<Evaluation>(query, new
            {
                evaluation_title,
                classCode,
                professor_id
            });


            return evaluation;
        }

        public async Task<Evaluation> getRubricEvaluation(string professor_id, string classCode, string evaluation_title)
        {
            var DB = getdbConnection();
            var query = @"CALL GetEvaluationInfo(@professor_id,@classCode,@evaluation_title)";

            var evaluation = await DB.QueryFirstOrDefaultAsync<Evaluation>(query, new
            {
                evaluation_title,
                classCode,
                professor_id
            });


            return evaluation;
        }



        public async Task<Grade> getFeedBackGrade(string professor_id, string classCode, string student_name, string evaluation_title)
        {
            var DB = getdbConnection();
            var query = @"CALL GetGradeInfo(@professor_id, @classCode, @student_name, @evaluation_title)";
            var result = await DB.QueryFirstOrDefaultAsync<Grade>(query, new
            {
                professor_id,
                classCode,
                student_name,
                evaluation_title
            });

            return result;
        }



    }
}