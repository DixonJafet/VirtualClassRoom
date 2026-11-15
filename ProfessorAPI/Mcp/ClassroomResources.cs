using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using MySql.Data.MySqlClient;

namespace ProfessorAPI.Mcp;

[McpServerResourceType]
public sealed class ClassroomResources
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _connectionString;

    public ClassroomResources(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _connectionString = configuration.GetConnectionString("MySQLConnnection")
            ?? configuration.GetConnectionString("MySQLConnection")
            ?? configuration["MYSQL_ADDON_URI"]
            ?? throw new InvalidOperationException("The MySQL connection string is not configured.");
    }

    [McpServerResource(UriTemplate = "classrooms://mine", Name = "My classrooms", MimeType = "application/json")]
    [System.ComponentModel.Description("Aulas del profesor autenticado y URI de recursos de evaluaciones.")]
    public async Task<string> GetClassroomsAsync()
    {
        using var connection = new MySqlConnection(_connectionString);
        var classrooms = await connection.QueryAsync(
            """
            SELECT classroomCode, courseName
            FROM classrooms
            WHERE professor_id = @professorId
            ORDER BY courseName
            """,
            new { professorId = GetProfessorId() });

        return JsonSerializer.Serialize(new
        {
            classrooms,
            evaluationsResourceTemplate = "classroom://{classroomCode}/evaluations"
        });
    }

    [McpServerResource(
        UriTemplate = "classroom://{classroomCode}/evaluations",
        Name = "Classroom evaluations",
        MimeType = "application/json")]
    [System.ComponentModel.Description("Evaluaciones del aula solicitada con valor máximo y fecha de entrega.")]
    public async Task<string> GetClassroomEvaluationsAsync(string classroomCode)
    {
        if (string.IsNullOrWhiteSpace(classroomCode) || classroomCode.Length > 100)
        {
            throw new ArgumentException("A valid classroom code is required.", nameof(classroomCode));
        }

        var professorId = GetProfessorId();
        using var connection = new MySqlConnection(_connectionString);
        var ownsClassroom = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM classrooms
            WHERE classroomCode = @classroomCode AND professor_id = @professorId
            """,
            new { classroomCode, professorId });

        if (ownsClassroom == 0)
        {
            throw new KeyNotFoundException("The classroom was not found for the authenticated professor.");
        }

        var evaluations = await connection.QueryAsync(
            """
            SELECT c.classroomCode, c.courseName, e.title, e.value AS weight, e.dueDate
            FROM evaluations e
            INNER JOIN classrooms c ON c.classroom_id = e.classroom_id
            WHERE c.classroomCode = @classroomCode AND c.professor_id = @professorId
            ORDER BY e.dueDate, e.title
            """,
            new { classroomCode, professorId });

        return JsonSerializer.Serialize(new
        {
            classroomCode,
            evaluations
        });
    }

    private string GetProfessorId()
    {
        var professorId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrEmpty(professorId)
            ? throw new UnauthorizedAccessException("The MCP request has no authenticated professor.")
            : professorId;
    }
}
