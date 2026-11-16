using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using MySql.Data.MySqlClient;
using ProfessorAPI.Interface;
using ProfessorAPI.Models;
using ProfessorAPI.Connexions;

namespace ProfessorAPI.Mcp;

[McpServerToolType]
public sealed partial class AnalyticsTools
{
    private const string DataLimitation =
        "El sistema no guarda un estado revisado/pendiente por calificación; un cero puede ser una nota real o un placeholder.";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _connectionString;
    private readonly InterProfessors _professors;
    private readonly BlobService _files;

    public AnalyticsTools(
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        InterProfessors professors,
        BlobService files)
    {
        _httpContextAccessor = httpContextAccessor;
        _professors = professors;
        _files = files;
        _connectionString = configuration.GetConnectionString("MySQLConnnection")
            ?? configuration.GetConnectionString("MySQLConnection")
            ?? configuration["MYSQL_ADDON_URI"]
            ?? throw new InvalidOperationException("The MySQL connection string is not configured.");
    }

    [McpServerTool(Name = "list_classrooms", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Lista las aulas del profesor autenticado.")]
    public async Task<string> ListClassroomsAsync()
    {
        using var connection = CreateConnection();
        var classrooms = await connection.QueryAsync(
            """
            SELECT classroomCode, courseName
            FROM classrooms
            WHERE professor_id = @professorId
            ORDER BY courseName
            """,
            new { professorId = GetProfessorId() });

        return JsonSerializer.Serialize(classrooms);
    }

    [McpServerTool(Name = "get_evaluation_curve", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Devuelve la curva de una evaluación: notas, porcentajes del máximo, distancia al puntaje más alto y distribución.")]
    public async Task<string> GetEvaluationCurveAsync(
        [Description("Código del aula")] string classroomCode,
        [Description("Título exacto de la evaluación")] string evaluationTitle)
    {
        var context = await LoadClassroomDataAsync(classroomCode);
        var evaluation = GetUniqueEvaluation(context.Evaluations, evaluationTitle);
        var maxPoints = RequireMaxPoints(evaluation);
        var scores = context.Grades.Select(row =>
        {
            var score = GetScore(row, evaluationTitle);
            return new
            {
                student = GetStudentName(row),
                score,
                percent = score,
                relativeToEvaluationPercent = maxPoints == 0m ? (decimal?)null : score / maxPoints * 100m
            };
        }).ToArray();
        var highestScore = scores.Length == 0 ? 0m : scores.Max(score => score.score);

        var result = new
        {
            classroomCode,
            evaluation = evaluationTitle,
            maxPercent = maxPoints,
            highestScore,
            highestScorePercent = highestScore,
            averagePercent = scores.Length == 0 ? 0m : scores.Average(score => score.percent),
            students = scores
                .Select(score => new
                {
                    score.student,
                    score.score,
                    score.percent,
                    score.relativeToEvaluationPercent,
                    relativeToHighestPercent = highestScore == 0m ? (decimal?)null : score.score / highestScore * 100m,
                    gapFromHighestPoints = highestScore - score.score
                })
                .OrderByDescending(score => score.score)
                .ThenBy(score => score.student, StringComparer.OrdinalIgnoreCase),
            distribution = new
            {
                below50 = scores.Count(score => score.relativeToEvaluationPercent < 50m),
                from50ToBelow68 = scores.Count(score => score.relativeToEvaluationPercent >= 50m && score.relativeToEvaluationPercent < 68m),
                from68ToBelow80 = scores.Count(score => score.relativeToEvaluationPercent >= 68m && score.relativeToEvaluationPercent < 80m),
                from80ToBelow90 = scores.Count(score => score.relativeToEvaluationPercent >= 80m && score.relativeToEvaluationPercent < 90m),
                from90To100 = scores.Count(score => score.relativeToEvaluationPercent >= 90m && score.relativeToEvaluationPercent <= 100m),
                above100 = scores.Count(score => score.relativeToEvaluationPercent > 100m)
            },
            distributionBasis = "porcentaje relativo al valor de la evaluación",
            dataLimitation = DataLimitation
        };

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "get_top_students", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Atajo para obtener los mejores estudiantes por porcentaje normalizado.")]
    public async Task<string> GetTopStudentsAsync(
        [Description("Código del aula")] string classroomCode,
        [Description("Cantidad máxima de estudiantes (1-100)")] int limit = 10)
    {
        return await RankStudentsAsync(classroomCode, "top", limit);
    }

    [McpServerTool(Name = "rank_students", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Obtiene los mejores o peores estudiantes por contribución al curso de evaluaciones vencidas o una evaluación concreta.")]
    public async Task<string> RankStudentsAsync(
        [Description("Código del aula")] string classroomCode,
        [Description("Usa 'top' para los mejores o 'bottom' para los peores")] string direction = "bottom",
        [Description("Cantidad de estudiantes (1-100)")] int limit = 5,
        [Description("Título exacto de evaluación opcional; si se omite se consideran evaluaciones vencidas")] string? evaluationTitle = null)
    {
        ValidateLimit(limit);
        if (!direction.Equals("top", StringComparison.OrdinalIgnoreCase) &&
            !direction.Equals("bottom", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("direction must be 'top' or 'bottom'.", nameof(direction));
        }

        var context = await LoadClassroomDataAsync(classroomCode);
        var evaluations = evaluationTitle is null
            ? context.Evaluations.Where(evaluation => evaluation.DueDate?.Date <= DateTime.UtcNow.Date).ToArray()
            : new[] { GetUniqueEvaluation(context.Evaluations, evaluationTitle) };
        var maxPoints = evaluations.Sum(RequireMaxPoints);
        var availableWeight = maxPoints;

        var rankedStudents = context.Grades.Select(row =>
        {
            var earnedPoints = evaluations.Sum(evaluation => GetScore(row, evaluation.Title!));
            return new
            {
                student = GetStudentName(row),
                percent = earnedPoints,
                evaluationsIncluded = evaluations.Length
            };
        });
        var ranked = direction.Equals("top", StringComparison.OrdinalIgnoreCase)
            ? rankedStudents.OrderByDescending(student => student.percent)
            : rankedStudents.OrderBy(student => student.percent);
        var students = evaluations.Length == 0
            ? Array.Empty<object>()
            : ranked
                .ThenBy(student => student.student, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Cast<object>()
                .ToArray();

        return JsonSerializer.Serialize(new
        {
            classroomCode,
            ranking = direction.ToLowerInvariant(),
            evaluation = evaluationTitle,
            calculation = evaluationTitle is null
                ? "suma de porcentajes obtenidos en evaluaciones vencidas"
                : "porcentaje obtenido en la evaluación",
            availableWeight,
            evaluationsIncluded = evaluations.Select(evaluation => new
            {
                evaluation.Title,
                value = evaluation.MaxPoints,
                evaluation.DueDate
            }),
            students,
            dataLimitation = DataLimitation
        });
    }

    [McpServerTool(Name = "compare_evaluations", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Compara dos evaluaciones por estudiante usando porcentajes respecto al valor máximo de cada una.")]
    public async Task<string> CompareEvaluationsAsync(
        [Description("Código del aula")] string classroomCode,
        [Description("Título exacto de la primera evaluación")] string firstEvaluationTitle,
        [Description("Título exacto de la segunda evaluación")] string secondEvaluationTitle)
    {
        var context = await LoadClassroomDataAsync(classroomCode);
        var first = GetUniqueEvaluation(context.Evaluations, firstEvaluationTitle);
        var second = GetUniqueEvaluation(context.Evaluations, secondEvaluationTitle);
        var firstMax = RequireMaxPoints(first);
        var secondMax = RequireMaxPoints(second);

        var comparisons = context.Grades.Select(row =>
        {
            var firstScore = GetScore(row, firstEvaluationTitle);
            var secondScore = GetScore(row, secondEvaluationTitle);
            var firstPercent = firstMax == 0m ? 0m : firstScore / firstMax * 100m;
            var secondPercent = secondMax == 0m ? 0m : secondScore / secondMax * 100m;

            return new
            {
                student = GetStudentName(row),
                firstScore,
                firstPercent,
                secondScore,
                secondPercent,
                changePoints = secondScore - firstScore,
                changePercentagePoints = secondPercent - firstPercent
            };
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            classroomCode,
            firstEvaluation = firstEvaluationTitle,
            secondEvaluation = secondEvaluationTitle,
            improved = comparisons.Count(student => student.changePercentagePoints > 0m),
            unchanged = comparisons.Count(student => student.changePercentagePoints == 0m),
            declined = comparisons.Count(student => student.changePercentagePoints < 0m),
            averageChangePercentagePoints = comparisons.Length == 0
                ? 0m
                : comparisons.Average(student => student.changePercentagePoints),
            students = comparisons
                .OrderByDescending(student => student.changePercentagePoints)
                .ThenBy(student => student.student, StringComparer.OrdinalIgnoreCase),
            dataLimitation = DataLimitation
        });
    }

    [McpServerTool(Name = "get_upcoming_evaluations", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Lista evaluaciones que vencen en los próximos días, para un aula o todas las aulas propias.")]
    public async Task<string> GetUpcomingEvaluationsAsync(
        [Description("Código del aula; omítelo para consultar todas tus aulas")] string? classroomCode = null,
        [Description("Días a consultar desde hoy (1-30)")] int days = 7)
    {
        if (days is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "days must be between 1 and 30.");
        }

        using var connection = CreateConnection();
        var evaluations = await connection.QueryAsync(
            """
            SELECT c.classroomCode, c.courseName, e.title, e.value AS weight, e.dueDate
            FROM evaluations e
            INNER JOIN classrooms c ON c.classroom_id = e.classroom_id
            WHERE c.professor_id = @professorId
              AND (@classroomCode IS NULL OR c.classroomCode = @classroomCode)
              AND e.dueDate >= CURRENT_DATE()
              AND e.dueDate < DATE_ADD(CURRENT_DATE(), INTERVAL @days DAY)
            ORDER BY e.dueDate, c.courseName, e.title
            """,
            new { professorId = GetProfessorId(), classroomCode, days });

        return JsonSerializer.Serialize(new
        {
            days,
            evaluations,
            dataLimitation = DataLimitation
        });
    }

    [McpServerTool(Name = "find_students_by_grade_range", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Busca estudiantes por contribución porcentual al curso de una evaluación o de evaluaciones vencidas.")]
    public async Task<string> FindStudentsByGradeRangeAsync(
        [Description("Código del aula")] string classroomCode,
        [Description("Porcentaje mínimo (0-100)")] decimal minimumPercent,
        [Description("Porcentaje máximo (0-100)")] decimal maximumPercent,
        [Description("Título de evaluación exacto; omítelo para usar evaluaciones vencidas")] string? evaluationTitle = null,
        [Description("Cantidad máxima de estudiantes (1-100)")] int limit = 50)
    {
        if (minimumPercent is < 0m or > 100m ||
            maximumPercent is < 0m or > 100m ||
            minimumPercent > maximumPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumPercent), "The grade range must be between 0 and 100, with minimum <= maximum.");
        }

        ValidateLimit(limit);
        var context = await LoadClassroomDataAsync(classroomCode);
        var evaluations = evaluationTitle is null
            ? context.Evaluations.Where(evaluation => evaluation.DueDate?.Date <= DateTime.UtcNow.Date).ToArray()
            : new[] { GetUniqueEvaluation(context.Evaluations, evaluationTitle) };
        var possiblePoints = evaluations.Sum(RequireMaxPoints);

        var students = evaluations.Length == 0
            ? Array.Empty<object>()
            : context.Grades
                .Select(row =>
                {
                    var earnedPoints = evaluations.Sum(evaluation => GetScore(row, evaluation.Title!));
                    return new
                    {
                        student = GetStudentName(row),
                        percent = earnedPoints,
                        evaluationsIncluded = evaluations.Length
                    };
                })
                .Where(student => student.percent >= minimumPercent && student.percent <= maximumPercent)
                .OrderBy(student => student.student, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Cast<object>()
                .ToArray();

        return JsonSerializer.Serialize(new
        {
            classroomCode,
            evaluation = evaluationTitle,
            minimumPercent,
            maximumPercent,
            students,
            dataLimitation = DataLimitation
        });
    }

    [McpServerTool(Name = "create_classroom", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Crea un aula para el profesor autenticado. Requiere un token MCP con permiso write y una instrucción explícita del usuario.")]
    public async Task<string> CreateClassroomAsync(
        [Description("Nombre del aula")] string courseName)
    {
        EnsureWriteAccess();
        ValidateText(courseName, nameof(courseName), 200);

        var classroom = await _professors.createClassRoom(
            GetProfessorId(),
            new ClassRoom { courseName = courseName });

        if (string.IsNullOrWhiteSpace(classroom?.classroomCode))
        {
            throw new InvalidOperationException("The classroom could not be created.");
        }

        return JsonSerializer.Serialize(new
        {
            classroomCode = classroom.classroomCode,
            courseName = classroom.courseName,
            created = true
        });
    }

    [McpServerTool(Name = "update_classroom", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Cambia el nombre de un aula propia. Antes de llamar, pide al usuario confirmación explícita de este cambio y solo entonces pasa confirmed=true. Requiere permiso write.")]
    public async Task<string> UpdateClassroomAsync(
        [Description("Código del aula propia")] string classroomCode,
        [Description("Nuevo nombre del aula")] string courseName,
        [Description("True solo después de que el usuario confirme este cambio")] bool confirmed = false)
    {
        EnsureWriteAccess();
        EnsureConfirmed(confirmed);
        ValidateText(courseName, nameof(courseName), 200);
        var professorId = GetProfessorId();
        ValidateClassroomCode(classroomCode);

        using var connection = CreateConnection();
        var updated = await connection.ExecuteAsync(
            """
            UPDATE classrooms
            SET courseName = @courseName
            WHERE classroomCode = @classroomCode AND professor_id = @professorId
            """,
            new { classroomCode, professorId, courseName });

        if (updated == 0)
        {
            throw new KeyNotFoundException("The classroom was not found for the authenticated professor.");
        }

        return JsonSerializer.Serialize(new { classroomCode, courseName, updated = true });
    }

    [McpServerTool(Name = "create_evaluation", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Crea una evaluación propia. No adjunta archivos. Requiere permiso write y una instrucción explícita del usuario.")]
    public async Task<string> CreateEvaluationAsync(
        [Description("Código del aula propia")] string classroomCode,
        [Description("Título de la evaluación")] string title,
        [Description("Peso de la evaluación en el curso, de 0 a 100")] decimal weight,
        [Description("Fecha límite ISO 8601")] DateTime dueDate,
        [Description("Materia o categoría opcional")] string? subject = null)
    {
        EnsureWriteAccess();
        ValidateClassroomCode(classroomCode);
        ValidateText(title, nameof(title), 200);
        ValidateEvaluationWeight(weight);
        ValidateText(subject, nameof(subject), 200, allowEmpty: true);
        var professorId = GetProfessorId();

        await EnsureClassroomOwnedAsync(classroomCode, professorId);
        int titleExists;
        using (var connection = CreateConnection())
        {
            titleExists = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM evaluations e
                INNER JOIN classrooms c ON c.classroom_id = e.classroom_id
                WHERE c.classroomCode = @classroomCode
                  AND c.professor_id = @professorId
                  AND e.title = @title
                """,
                new { classroomCode, professorId, title });
        }

        if (titleExists > 0)
        {
            throw new InvalidOperationException("An evaluation with this title already exists in the classroom.");
        }

        var created = await _professors.CreateEvaluation(
            professorId,
            classroomCode,
            new Evaluation
            {
                title = title,
                value = (float)weight,
                dueDate = dueDate,
                subject = subject
            });

        if (!created)
        {
            throw new InvalidOperationException("The evaluation could not be created.");
        }

        return JsonSerializer.Serialize(new
        {
            classroomCode,
            title,
            weight,
            dueDate,
            subject,
            created = true
        });
    }

    [McpServerTool(Name = "update_evaluation", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Actualiza una evaluación propia. Antes de llamar, pide al usuario confirmación explícita de los cambios y solo entonces pasa confirmed=true. Requiere permiso write.")]
    public async Task<string> UpdateEvaluationAsync(
        [Description("Código del aula propia")] string classroomCode,
        [Description("Título actual exacto de la evaluación")] string currentTitle,
        [Description("Nuevo título de la evaluación")] string newTitle,
        [Description("Nuevo peso de la evaluación en el curso, de 0 a 100")] decimal weight,
        [Description("Nueva fecha límite ISO 8601")] DateTime dueDate,
        [Description("Nueva materia o categoría opcional")] string? subject = null,
        [Description("True solo después de que el usuario confirme estos cambios")] bool confirmed = false)
    {
        EnsureWriteAccess();
        EnsureConfirmed(confirmed);
        ValidateClassroomCode(classroomCode);
        ValidateText(currentTitle, nameof(currentTitle), 200);
        ValidateText(newTitle, nameof(newTitle), 200);
        ValidateEvaluationWeight(weight);
        ValidateText(subject, nameof(subject), 200, allowEmpty: true);
        var professorId = GetProfessorId();

        using var connection = CreateConnection();
        var currentTitleCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM evaluations e
            INNER JOIN classrooms c ON c.classroom_id = e.classroom_id
            WHERE c.classroomCode = @classroomCode
              AND c.professor_id = @professorId
              AND e.title = @currentTitle
            """,
            new { classroomCode, professorId, currentTitle });
        if (currentTitleCount == 0)
        {
            throw new KeyNotFoundException("The evaluation was not found for the authenticated professor.");
        }
        if (currentTitleCount > 1)
        {
            throw new InvalidOperationException("The evaluation title is ambiguous in this classroom.");
        }

        if (!string.Equals(currentTitle, newTitle, StringComparison.OrdinalIgnoreCase))
        {
            var newTitleCount = await connection.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM evaluations e
                INNER JOIN classrooms c ON c.classroom_id = e.classroom_id
                WHERE c.classroomCode = @classroomCode
                  AND c.professor_id = @professorId
                  AND e.title = @newTitle
                """,
                new { classroomCode, professorId, newTitle });
            if (newTitleCount > 0)
            {
                throw new InvalidOperationException("An evaluation with the new title already exists in the classroom.");
            }
        }

        await _professors.EditEvaluation(professorId, classroomCode, new Evaluation
        {
            title = newTitle,
            value = (float)weight,
            dueDate = dueDate,
            subject = subject
        }, currentTitle);

        return JsonSerializer.Serialize(new
        {
            classroomCode,
            previousTitle = currentTitle,
            title = newTitle,
            weight,
            dueDate,
            subject,
            updated = true
        });
    }

    [McpServerTool(Name = "set_student_grade", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Actualiza una calificación y conserva el feedback. Antes de llamar, pide al usuario confirmación explícita de la nota y solo entonces pasa confirmed=true. Requiere permiso write.")]
    public async Task<string> SetStudentGradeAsync(
        [Description("Código del aula propia")] string classroomCode,
        [Description("Nombre exacto del estudiante")] string studentName,
        [Description("Título exacto de la evaluación")] string evaluationTitle,
        [Description("Porcentaje obtenido del curso, desde cero hasta el valor de la evaluación inclusive")] decimal score,
        [Description("True solo después de que el usuario confirme este cambio")] bool confirmed = false)
    {
        EnsureWriteAccess();
        EnsureConfirmed(confirmed);
        ValidateClassroomCode(classroomCode);
        ValidateText(studentName, nameof(studentName), 200);
        ValidateText(evaluationTitle, nameof(evaluationTitle), 200);
        var professorId = GetProfessorId();
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        var ownedClassroom = await connection.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT classroom_id FROM classrooms
            WHERE classroomCode = @classroomCode AND professor_id = @professorId
            FOR UPDATE
            """,
            new { classroomCode, professorId }, transaction);
        if (ownedClassroom is null)
            throw new KeyNotFoundException("The classroom was not found for the authenticated professor.");

        var maxPoints = await connection.QuerySingleOrDefaultAsync<decimal?>(
            """
            SELECT e.value
            FROM evaluations e
            INNER JOIN classrooms c ON c.classroom_id = e.classroom_id
            WHERE c.classroomCode = @classroomCode
              AND c.professor_id = @professorId
              AND e.title = @evaluationTitle
            """,
            new { classroomCode, professorId, evaluationTitle }, transaction);

        if (maxPoints is null)
        {
            throw new KeyNotFoundException("The evaluation was not found for the authenticated professor.");
        }
        if (score < 0m || score > maxPoints.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(score), $"Score must be between 0 and {maxPoints.Value}.");
        }

        var parameters = new
        {
            professor_id = professorId,
            classCode = classroomCode,
            student_name = studentName,
            evaluation_title = evaluationTitle
        };
        var currentGrade = await connection.QueryFirstOrDefaultAsync<GradeWriteState>(
            "CALL GetGradeInfo(@professor_id, @classCode, @student_name, @evaluation_title)",
            parameters, transaction);

        if (currentGrade is null)
        {
            throw new KeyNotFoundException("No grade record exists for this student and evaluation.");
        }

        var result = await connection.QueryFirstOrDefaultAsync<Grade>(
            """
            CALL EditGradeInfo(
                @professor_id, @classCode, @student_name, @evaluation_title, @grade, @feedback
            )
            """,
            new
            {
                professor_id = professorId,
                classCode = classroomCode,
                student_name = studentName,
                evaluation_title = evaluationTitle,
                grade = (float)score,
                feedback = currentGrade.Feedback
            }, transaction);

        if (result is null)
        {
            throw new InvalidOperationException("The grade could not be saved.");
        }
        await transaction.CommitAsync();

        return JsonSerializer.Serialize(new
        {
            classroomCode,
            student = studentName,
            evaluation = evaluationTitle,
            score,
            maxPercent = maxPoints.Value,
            percent = score,
            updated = true
        });
    }

    private string GetProfessorId()
    {
        var professorId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var hasReadAccess = _httpContextAccessor.HttpContext?.User.HasClaim("mcp_access", "read") == true;
        return string.IsNullOrEmpty(professorId) || !hasReadAccess
            ? throw new UnauthorizedAccessException("The MCP request has no authenticated professor.")
            : professorId;
    }

    private void EnsureWriteAccess()
    {
        if (_httpContextAccessor.HttpContext?.User.HasClaim("mcp_access", "write") != true)
        {
            throw new UnauthorizedAccessException("This MCP operation requires a write-enabled access token.");
        }
    }

    private static void EnsureConfirmed(bool confirmed)
    {
        if (!confirmed)
            throw new McpException("Ask the user to confirm this specific change before calling again with confirmed=true.");
    }

    private async Task EnsureClassroomOwnedAsync(string classroomCode, string professorId)
    {
        using var connection = CreateConnection();
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
    }

    private MySqlConnection CreateConnection() => new(_connectionString);

    private async Task<ClassroomData> LoadClassroomDataAsync(string classroomCode)
    {
        if (string.IsNullOrWhiteSpace(classroomCode) || classroomCode.Length > 100)
        {
            throw new ArgumentException("A valid classroom code is required.", nameof(classroomCode));
        }

        var professorId = GetProfessorId();
        using var connection = CreateConnection();
        var isOwned = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM classrooms
            WHERE classroomCode = @classroomCode AND professor_id = @professorId
            """,
            new { classroomCode, professorId });

        if (isOwned == 0)
        {
            throw new KeyNotFoundException("The classroom was not found for the authenticated professor.");
        }

        var evaluations = (await connection.QueryAsync<EvaluationInfo>(
            """
            SELECT e.title AS Title, e.value AS MaxPoints, e.dueDate AS DueDate
            FROM evaluations e
            INNER JOIN classrooms c ON c.classroom_id = e.classroom_id
            WHERE c.classroomCode = @classroomCode AND c.professor_id = @professorId
            ORDER BY e.evaluation_id
            """,
            new { classroomCode, professorId })).ToArray();

        var gradeRows = await connection.QueryAsync(
            "CALL getEvaluationsClassRoom(@classCode, @professor_id)",
            new { classCode = classroomCode, professor_id = professorId });
        var grades = gradeRows
            .Select(row => (IDictionary<string, object?>)row)
            .ToArray();

        return new ClassroomData(evaluations, grades);
    }

    private static EvaluationInfo GetUniqueEvaluation(IEnumerable<EvaluationInfo> evaluations, string title)
    {
        var matches = evaluations
            .Where(evaluation => string.Equals(evaluation.Title, title, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException($"Evaluation '{title}' was not found in this classroom."),
            _ => throw new InvalidOperationException($"Evaluation title '{title}' is ambiguous in this classroom.")
        };
    }

    private static decimal RequireMaxPoints(EvaluationInfo evaluation)
    {
        if (evaluation.MaxPoints is null or < 0m or > 100m)
        {
            throw new InvalidOperationException($"Evaluation '{evaluation.Title}' has no valid percentage value.");
        }

        return evaluation.MaxPoints.Value;
    }

    private static decimal GetScore(IDictionary<string, object?> row, string evaluationTitle)
    {
        var column = row.FirstOrDefault(entry =>
            string.Equals(entry.Key, evaluationTitle, StringComparison.OrdinalIgnoreCase));

        if (column.Key is null)
        {
            throw new InvalidOperationException(
                $"The classroom grade query did not return an evaluation column named '{evaluationTitle}'.");
        }

        if (column.Value is null or DBNull)
        {
            return 0m;
        }

        return decimal.TryParse(
            Convert.ToString(column.Value, CultureInfo.InvariantCulture),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var score)
            ? score
            : throw new InvalidOperationException($"The score for '{evaluationTitle}' is not numeric.");
    }

    private static string GetStudentName(IDictionary<string, object?> row)
    {
        var studentName = row.FirstOrDefault(entry =>
            entry.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
            entry.Key.Equals("student_name", StringComparison.OrdinalIgnoreCase) ||
            entry.Key.Equals("studentName", StringComparison.OrdinalIgnoreCase));

        return studentName.Value is string name && !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new InvalidOperationException("The classroom grade query did not return a student name.");
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be between 1 and 100.");
        }
    }

    private static void ValidateClassroomCode(string classroomCode)
    {
        if (string.IsNullOrWhiteSpace(classroomCode) || classroomCode.Length > 100)
        {
            throw new ArgumentException("A valid classroom code is required.", nameof(classroomCode));
        }
    }

    private static void ValidateText(string? value, string parameterName, int maximumLength, bool allowEmpty = false)
    {
        if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) || (value?.Length ?? 0) > maximumLength)
        {
            throw new ArgumentException($"The value must be {(allowEmpty ? "at most" : "non-empty and at most")} {maximumLength} characters.", parameterName);
        }
    }

    private static void ValidateEvaluationWeight(decimal weight)
    {
        if (weight is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(weight), "The course weight must be between 0 and 100.");
    }

    private sealed record EvaluationInfo(string? Title, decimal? MaxPoints, DateTime? DueDate);

    private sealed class GradeWriteState
    {
        public decimal? Grade { get; set; }
        public string? Feedback { get; set; }
    }

    private sealed record ClassroomData(
        EvaluationInfo[] Evaluations,
        IDictionary<string, object?>[] Grades);
}
