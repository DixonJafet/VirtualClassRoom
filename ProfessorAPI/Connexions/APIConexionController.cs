using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MySqlX.XDevAPI.Common;
using ProfessorAPI.Controllers;
using ProfessorAPI.Interface;
using ProfessorAPI.Models;
using System.Drawing.Printing;
using System.Security.Claims;
using System.Security.Permissions;

namespace ProfessorAPI.Connexions //Access-Control-Allow-Origin
{
    [EnableCors("Policy")]
    [Route("api/profes")] // [controller]
    [ApiController]
    public class APIConexionController : ControllerBase
    {
        private readonly InterProfessors interProfessors;
        private readonly MyJWT JWT;



        public APIConexionController(InterProfessors interProfessors, MyJWT myJWT)
        {
            this.interProfessors = interProfessors;
            JWT = myJWT;

        }



        [HttpGet("Profile")]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            Console.WriteLine(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null) return BadRequest();
            Console.WriteLine(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var result = await interProfessors.GetProfessor(professor_id);
            return Ok(result);
        }




        [HttpDelete("delete/{classCode}")]
        [Authorize]

        public async Task<IActionResult> deleteClassRoom(string classCode)
        {
         //   Console.WriteLine(classCode);
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!ModelState.IsValid) return BadRequest();
            if (classCode == null) return BadRequest();
            if (professor_id == null) return BadRequest();
            var response = await interProfessors.deleteClassRoom(professor_id, classCode);
            if (response == null) return BadRequest();
            return Ok(response);
        }


        [HttpPut("edit/{classCode}")]
        [Authorize]
        public async Task<IActionResult> editClassRoomInfo( [FromForm] ClassRoom classroom, string classCode)
        {
           // Console.WriteLine(classCode);
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!ModelState.IsValid) return BadRequest();
            if (classCode == null) return BadRequest();
            if (professor_id == null) return BadRequest();
       
            var response = await interProfessors.editClassRoom(professor_id, classCode, classroom);
            if (response == null) return BadRequest();
            return Ok(response);
        }

        [HttpGet("edit/{classCode}")]
        [Authorize]
        public async Task<IActionResult>getClassRoomInfo(string classCode)
        {
            // Console.WriteLine(classCode);
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!ModelState.IsValid) return BadRequest();
            if (classCode == null) return BadRequest();
            if (professor_id == null) return BadRequest();

            var response = await interProfessors.getClassRoom(professor_id, classCode);
            if (response == null) return BadRequest();
            return Ok(response);
        }

        [HttpPost("SingUp")]
        [Authorize]
        public async Task<IActionResult> SingUp([FromBody] Professor professor)
        {
            if (professor == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            Professor singed_professor = await interProfessors.SingUP(professor);
            if (singed_professor.professor_id == "0")
            {
                return BadRequest(
                    new
                    {
                        error = "User not Created",
                        message = "Professor with same email already exist"
                    });
            }
            string myJWT = JWT.userJWTSession(professor.name, singed_professor.professor_id);
            Response.Cookies.Append("SchoolWebToken506", myJWT, new CookieOptions { Secure = true, HttpOnly = true });

            return Ok(new Professor { name = singed_professor.name });
        }

        [HttpPost("LogIn")]

        public async Task<IActionResult> LogIn([FromBody] Professor professor)
        {

            Console.WriteLine(professor);

            if (professor == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            Professor logged_professor = await interProfessors.LogIn(professor);
            Console.WriteLine(logged_professor);


            if (logged_professor.professor_id == "0")
            {
                return BadRequest(
                    new
                    {
                        error = "User not Loged",
                        message = "Email or Password are  incorrect"
                    });
            }
            string myJWT = JWT.userJWTSession(logged_professor.name, logged_professor.professor_id);
            Response.Cookies.Append("SchoolWebToken506", myJWT, new CookieOptions { Secure = true, HttpOnly = true });

            return Ok(new Professor { name = logged_professor.name });

        }




        [HttpGet("ClassRooms")]
        [Authorize]
        public async Task<IActionResult> GetClassRooms()
        {
            Console.WriteLine(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null) return BadRequest();

            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"{professor_id}");
            var response = await interProfessors.getAllProfessorClassRooms(new Professor { professor_id = professor_id });

            return Ok(new
            {
                ClassRooms = response
            });
        }

        [HttpPut("UpdateProfileInfo")]
        public async Task<IActionResult> UpdateProfessor([FromBody] Professor professor)
        {
            if (professor == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            Console.WriteLine(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null) return BadRequest();

            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            professor.professor_id = professor_id;
            var updated = await interProfessors.UpdateProfessor(professor);

            return Ok(professor);

        }

        [HttpPost("LogOut")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            // Clear the JWT cookie by expiring it
            Response.Cookies.Append("SchoolWebToken506", "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,      // true if using HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(-1), // Expire immediately
                Path = "/"          // Match cookie path
            });

            return Ok(new { message = "Logged out" });
        }





        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProfessor(string id)
        {
            var deleted = await interProfessors.DeleteProfessor(new Professor { professor_id = id });

            return NoContent();

        }

        [HttpGet("checkCredentials")]
        [Authorize]
        public async Task<IActionResult> CheckAccount()
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            Console.WriteLine(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            bool chek = await interProfessors.CheckAccount(new Professor { professor_id = professor_id });
            Console.WriteLine(chek);
            if (!chek) return BadRequest();

            return Ok(new { message = "credentials checked" });


        }


        [HttpGet("ClassRoomEvaluation/{classCode}")]
        [Authorize]
        public async Task<IActionResult> GetClassRoomEvaluation(string classCode)
        {


           // Console.WriteLine(classCode);
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!ModelState.IsValid) return BadRequest();
            if (classCode == null) return BadRequest();
            if (professor_id == null) return BadRequest();
            var response = await interProfessors.getClassRoomEvaluation(professor_id, classCode);
            if (response == null) return BadRequest();
            return Ok(response);

        }

        [HttpPost("newClassRoom")]
        [Authorize]
        public async Task<IActionResult> CreateClassRoom([FromForm] ClassRoom classRoom)
        {
            if (classRoom == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();

            Console.Write(classRoom.courseName);
            var response = await interProfessors.createClassRoom(professor_id, classRoom);
            Console.Write(response.classroomCode);
            if (response == null) return BadRequest();
            return Ok(response);


        }


        [HttpDelete("deleteEvaluation/{classCode}/{evaluation}")]
        [Authorize]
        public async Task<IActionResult> DeleteEvaluation(string classCode, string evaluation)
        {


           // Console.WriteLine(classCode);
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!ModelState.IsValid) return BadRequest();
            if (classCode == null) return BadRequest();
            if (professor_id == null) return BadRequest();
            var response = await interProfessors.DeleteEvaluation(professor_id, classCode, evaluation);

            if (response == null) return BadRequest();
            return Ok(response);

        }

        [HttpPost("createEvaluation/{classCode}")]
        [Authorize]
        public async Task<IActionResult> CreateEvaluation([FromForm] Evaluation evaluation,string classCode)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (evaluation == null) return BadRequest();

            var result = await interProfessors.CreateEvaluation( professor_id, classCode, evaluation );

            if (!result) return BadRequest(new { error = "Evaluation not created" });
            return Ok(new { message = "Evaluation created successfully" });
        }

        [HttpPut("editEvaluation/{classCode}/{prev_title}")]
        [Authorize]
        public async Task<IActionResult> EditEvaluation([FromForm] Evaluation evaluation,string classCode,string prev_title)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (evaluation == null || evaluation == null ) return BadRequest();
            Console.WriteLine(prev_title);
            Console.WriteLine(classCode);
            var result = await interProfessors.EditEvaluation(
                professor_id,
                classCode,
                evaluation,
                prev_title

            );

            if (result == null) return BadRequest(new { error = "Evaluation not updated" });
            return Ok(new { message = "Evaluation updated successfully" });
        }


        [HttpGet("getEvaluation/{classCode}/{evaluation_title}")]
        [Authorize]
        public async Task<IActionResult> GetEvaluation(string classCode, string evaluation_title)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(evaluation_title)) return BadRequest();
            Console.WriteLine(classCode);
            // You need to implement this method in InterProfessors and ProfessorsController
            var evaluation = await interProfessors.GetEvaluation(professor_id, classCode, evaluation_title);
            if (evaluation == null) return NotFound(new { error = "Evaluation not found" });
           
            return Ok(evaluation);
        }


        [HttpGet("grade/{classCode}/{student_name}/{evaluation_title}")]
        [Authorize]
        public async Task<IActionResult>GetGrade(string classCode,string student_name,string evaluation_title)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            var response = await interProfessors.GetGrade(professor_id,classCode, student_name, evaluation_title);
            return Ok(response);
        }


        [HttpPut("editGrade/{classCode}/{student_name}/{evaluation_title}")]
        [Authorize]
        public async Task<IActionResult> EditGrade([FromForm] Grade gradeInfo,string classCode, string student_name, string evaluation_title)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            Console.WriteLine(gradeInfo.feedback);
            var response = await interProfessors.EditGrade(professor_id,classCode, student_name, evaluation_title, gradeInfo);
            return Ok(response);
           // return Ok(new { message = "Grade updated successfully" });
        }



        [HttpGet("aboutFile/{classroomCode}")]
        public IActionResult downloadFileAbout(string classroomCode)
        {

            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();

            var filePath = interProfessors
                .getAboutClassRoom(professor_id, classroomCode)
                .Result
                .about;

            return FileController.getFile(filePath);
        }


        [HttpGet("statementFile/{classCode}/{evaluation_title}")]
        [Authorize]
        public async Task<IActionResult> downloadFileStatement(string classCode, string evaluation_title)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(evaluation_title)) return BadRequest();
            Console.WriteLine(classCode);
            // You need to implement this method in InterProfessors and ProfessorsController
            var evaluation = await interProfessors.getStatementEvaluation(professor_id, classCode, evaluation_title);

            if (evaluation == null) return NotFound(new { error = "Evaluation not found" });

            var filePath = evaluation.statement;

            return FileController.getFile(filePath);
        }

        [HttpGet("rubricFile/{classCode}/{evaluation_title}")]
        [Authorize]
        public async Task<IActionResult> downloadRubricFile(string classCode, string evaluation_title)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(evaluation_title)) return BadRequest();
            Console.WriteLine(classCode);
            // You need to implement this method in InterProfessors and ProfessorsController
            var evaluation = await interProfessors.getRubricEvaluation(professor_id, classCode, evaluation_title);
            if (evaluation == null) return NotFound(new { error = "Evaluation not found" });

            var filePath = evaluation.rubric;
            return FileController.getFile(filePath);
        }



        [HttpGet("feedBackFile/{classCode}/{student_name}/{evaluation_title}")]
        [Authorize]
        public async Task<IActionResult> downloadFileRubric(string classCode, string student_name, string evaluation_title)
        {
            var professor_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (professor_id == null) return BadRequest();
            var response = await interProfessors.getFeedBackGrade(professor_id, classCode, student_name, evaluation_title);
            
            var filePath = response.feedback;
            Console.WriteLine(filePath);
            return FileController.getFile(filePath);
        }





    }
}
