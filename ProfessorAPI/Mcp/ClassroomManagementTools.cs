using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ProfessorAPI.Mcp;

public sealed partial class AnalyticsTools
{
    private const int MaxAttachmentBytes = 1024 * 1024;
    private static readonly string[] Days =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    [McpServerTool(Name = "get_classroom_summary")]
    [Description("Resume el aula propia: valores de evaluaciones, porcentaje restante y distribución de notas del curso.")]
    public async Task<string> GetClassroomSummaryAsync(string classroomCode)
    {
        ValidateClassroomCode(classroomCode);
        var professorId = GetProfessorId();
        using var connection = CreateConnection();
        await EnsureClassroomOwnedAsync(classroomCode, professorId);
        var evaluations = (await connection.QueryAsync<EvaluationInfo>(
            """
            SELECT e.title AS Title, e.value AS MaxPoints, e.DueDate
            FROM evaluations e JOIN classrooms c ON c.classroom_id=e.classroom_id
            WHERE c.professor_id=@professorId AND c.classroomCode=@classroomCode
            """, new { professorId, classroomCode })).ToArray();
        var available = evaluations.Sum(RequireMaxPoints);
        var totals = (await connection.QueryAsync<decimal>(
            """
            SELECT COALESCE(SUM(g.grade), 0)
            FROM grades g JOIN evaluations e ON e.evaluation_id=g.evaluation_id
            JOIN classrooms c ON c.classroom_id=e.classroom_id
            WHERE c.professor_id=@professorId AND c.classroomCode=@classroomCode
            GROUP BY g.student_id
            """, new { professorId, classroomCode })).ToArray();
        return JsonSerializer.Serialize(new
        {
            classroomCode,
            evaluationCount = evaluations.Length,
            assignedPercent = available,
            remainingPercent = 100m - available,
            studentCount = totals.Length,
            averageCoursePercent = totals.Length == 0 ? (decimal?)null : totals.Average(),
            distribution = new
            {
                below50 = totals.Count(total => total < 50m),
                from50ToBelow68 = totals.Count(total => total >= 50m && total < 68m),
                from68ToBelow80 = totals.Count(total => total >= 68m && total < 80m),
                from80ToBelow90 = totals.Count(total => total >= 80m && total < 90m),
                from90To100 = totals.Count(total => total >= 90m && total <= 100m)
            },
            distributionBasis = "porcentaje obtenido del curso, no normalizado al valor asignado",
            dataLimitation = DataLimitation
        });
    }

    [McpServerTool(Name = "get_classroom_details")]
    [Description("Consulta el nombre, horarios y evaluaciones de un aula propia, con nombres de sus adjuntos.")]
    public async Task<string> GetClassroomDetailsAsync(string classroomCode)
    {
        ValidateClassroomCode(classroomCode);
        var professorId = GetProfessorId();
        var classroom = await _professors.getClassRoom(professorId, classroomCode)
            ?? throw new KeyNotFoundException("The classroom was not found for the authenticated professor.");
        using var connection = CreateConnection();
        var evaluations = await connection.QueryAsync<EvaluationAttachmentState>(
            """
            SELECT e.evaluation_id AS Id, e.title AS Title, e.value AS Value, e.DueDate,
                   e.subject AS Subject, e.statement AS Statement, e.rubric AS Rubric
            FROM evaluations e JOIN classrooms c ON c.classroom_id = e.classroom_id
            WHERE c.professor_id = @professorId AND c.classroomCode = @classroomCode
            ORDER BY e.DueDate, e.title
            """, new { professorId, classroomCode });
        return JsonSerializer.Serialize(new
        {
            classroomCode,
            classroom.courseName,
            classroom.schedule,
            about = classroom.about,
            evaluations = evaluations.Select(e => new
            {
                e.Title, e.Value, e.DueDate, e.Subject,
                statement = AttachmentName(e.Statement),
                rubric = AttachmentName(e.Rubric)
            })
        });
    }

    [McpServerTool(Name = "set_classroom_schedule")]
    [Description("Reemplaza todos los horarios de un aula propia. Requiere permiso write e instrucción explícita.")]
    public async Task<string> SetClassroomScheduleAsync(
        string classroomCode,
        [Description("Lista completa de horarios; una lista vacía elimina los horarios existentes")] ClassroomScheduleInput[] schedules)
    {
        EnsureWriteAccess();
        ValidateClassroomCode(classroomCode);
        if (schedules is null || schedules.Length > 100)
            throw new ArgumentException("Provide at most 100 schedule entries.", nameof(schedules));
        var normalized = schedules.Select(s =>
        {
            if (s is null || !Days.Contains(s.Day, StringComparer.Ordinal) ||
                !TimeSpan.TryParseExact(s.From, @"hh\:mm", CultureInfo.InvariantCulture, out var from) ||
                !TimeSpan.TryParseExact(s.To, @"hh\:mm", CultureInfo.InvariantCulture, out var to) ||
                from >= TimeSpan.FromDays(1) || to >= TimeSpan.FromDays(1) || from >= to)
                throw new ArgumentException("Each schedule needs an English weekday and a valid HH:mm start and end, with start before end.", nameof(schedules));
            return new { s.Day, From = from, To = to };
        }).ToArray();

        var professorId = GetProfessorId();
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        var classroomId = await LockOwnedClassroomAsync(connection, transaction, classroomCode, professorId);
        await connection.ExecuteAsync("DELETE FROM schedules WHERE classroom_id = @classroomId", new { classroomId }, transaction);
        foreach (var schedule in normalized)
            await connection.ExecuteAsync(
                "INSERT INTO schedules (classroom_id, day, from_time, to_time) VALUES (@classroomId, @Day, @From, @To)",
                new { classroomId, schedule.Day, schedule.From, schedule.To }, transaction);
        await transaction.CommitAsync();
        return JsonSerializer.Serialize(new { classroomCode, schedules, updated = true });
    }

    [McpServerTool(Name = "set_student_grades")]
    [Description("Actualiza un lote de notas de una evaluación propia de forma atómica, conservando el feedback. Requiere permiso write e instrucción explícita.")]
    public async Task<string> SetStudentGradesAsync(
        string classroomCode, string evaluationTitle,
        [Description("Entre 1 y 100 estudiantes con sus porcentajes obtenidos")] StudentGradeInput[] grades)
    {
        EnsureWriteAccess();
        ValidateClassroomCode(classroomCode);
        ValidateText(evaluationTitle, nameof(evaluationTitle), 200);
        if (grades is null || grades.Length is < 1 or > 100 ||
            grades.Any(g => g is null || string.IsNullOrWhiteSpace(g.StudentName) || g.StudentName.Length > 200) ||
            grades.Select(g => g.StudentName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != grades.Length)
            throw new ArgumentException("Provide 1 to 100 distinct student names.", nameof(grades));

        var professorId = GetProfessorId();
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        var classroomId = await LockOwnedClassroomAsync(connection, transaction, classroomCode, professorId);
        var evaluation = await connection.QuerySingleOrDefaultAsync<EvaluationAttachmentState>(
            "SELECT evaluation_id AS Id, value AS Value FROM evaluations WHERE classroom_id=@classroomId AND title=@evaluationTitle",
            new { classroomId, evaluationTitle }, transaction)
            ?? throw new KeyNotFoundException("The evaluation was not found.");
        foreach (var grade in grades)
        {
            if (grade.Score < 0m || grade.Score > evaluation.Value || decimal.Round(grade.Score, 2) != grade.Score)
                throw new ArgumentOutOfRangeException(nameof(grades), $"Each score must be between 0 and {evaluation.Value} with at most two decimal places.");
            var args = new { professor_id = professorId, classCode = classroomCode, student_name = grade.StudentName, evaluation_title = evaluationTitle };
            var current = await connection.QuerySingleOrDefaultAsync<GradeWriteState>(
                "CALL GetGradeInfo(@professor_id, @classCode, @student_name, @evaluation_title)", args, transaction)
                ?? throw new KeyNotFoundException($"No grade record exists for student '{grade.StudentName}'.");
            var result = await connection.QuerySingleOrDefaultAsync<ProfessorAPI.Models.Grade>(
                "CALL EditGradeInfo(@professor_id, @classCode, @student_name, @evaluation_title, @grade, @feedback)",
                new { args.professor_id, args.classCode, args.student_name, args.evaluation_title, grade = (float)grade.Score, feedback = current.Feedback }, transaction);
            if (result is null) throw new InvalidOperationException("The grade could not be saved.");
        }
        await transaction.CommitAsync();
        return JsonSerializer.Serialize(new { classroomCode, evaluationTitle, updated = grades.Length });
    }

    [McpServerTool(Name = "delete_evaluation")]
    [Description("Elimina una evaluación propia y todas sus notas solamente tras confirmación interactiva del usuario. Requiere permiso write.")]
    public async Task<string> DeleteEvaluationAsync(
        RequestContext<CallToolRequestParams> request,
        string classroomCode, string evaluationTitle, CancellationToken cancellationToken)
    {
        EnsureWriteAccess();
        ValidateClassroomCode(classroomCode);
        ValidateText(evaluationTitle, nameof(evaluationTitle), 200);
        var professorId = GetProfessorId();
        using var connection = CreateConnection();
        var preview = await connection.QuerySingleOrDefaultAsync<DeletionPreview>(
            """
            SELECT e.evaluation_id AS Id, e.title AS Title, e.value AS Value,
                   (SELECT COUNT(*) FROM grades g WHERE g.evaluation_id=e.evaluation_id) AS GradeCount
            FROM evaluations e JOIN classrooms c ON c.classroom_id=e.classroom_id
            WHERE c.professor_id=@professorId AND c.classroomCode=@classroomCode AND e.title=@evaluationTitle
            """, new { professorId, classroomCode, evaluationTitle })
            ?? throw new KeyNotFoundException("The evaluation was not found.");
        if (!request.Server.IsMrtrSupported)
            throw new McpException("This MCP client cannot request confirmation. Use a client supporting MCP multi-round-trip requests.");

        var state = request.Params?.RequestState;
        var responses = request.Params?.InputResponses;
        if (state is null)
        {
            var protectedState = _deletionProtector.Protect(
                JsonSerializer.Serialize(new DeletionState(professorId, classroomCode, preview)),
                TimeSpan.FromMinutes(5));
            throw new InputRequiredException(
                inputRequests: new Dictionary<string, InputRequest>
                {
                    ["confirmation"] = InputRequest.ForElicitation(new ElicitRequestParams
                    {
                        Message = $"Delete evaluation '{preview.Title}' from classroom '{classroomCode}'? This permanently deletes {preview.GradeCount} associated grades. Its value is {preview.Value}% of the course.",
                        RequestedSchema = new()
                        {
                            Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                            {
                                ["confirm"] = new ElicitRequestParams.BooleanSchema { Description = "Confirm permanent deletion" }
                            },
                            Required = ["confirm"]
                        }
                    })
                }, requestState: protectedState);
        }

        var confirmedState = JsonSerializer.Deserialize<DeletionState>(_deletionProtector.Unprotect(state))
            ?? throw new InvalidOperationException("Invalid deletion confirmation.");
        if (confirmedState.ProfessorId != professorId || confirmedState.ClassroomCode != classroomCode ||
            confirmedState.Preview.Id != preview.Id || confirmedState.Preview.Title != preview.Title ||
            confirmedState.Preview.Value != preview.Value || confirmedState.Preview.GradeCount != preview.GradeCount)
            throw new McpException("The evaluation changed after confirmation; request deletion again.");
        if (responses is null || !responses.TryGetValue("confirmation", out var response))
            throw new McpException("Deletion requires a confirmation response.");
        var confirmation = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
        if (confirmation?.IsAccepted != true || confirmation.Content is null ||
            !confirmation.Content.TryGetValue("confirm", out var confirm) || confirm.ValueKind != JsonValueKind.True)
            throw new McpException("Deletion was not confirmed; nothing was deleted.");

        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        var classroomId = await LockOwnedClassroomAsync(connection, transaction, classroomCode, professorId);
        var current = await connection.QuerySingleOrDefaultAsync<DeletionPreview>(
            """
            SELECT e.evaluation_id AS Id, e.title AS Title, e.value AS Value,
                   (SELECT COUNT(*) FROM grades g WHERE g.evaluation_id=e.evaluation_id) AS GradeCount
            FROM evaluations e WHERE e.evaluation_id=@id AND e.classroom_id=@classroomId FOR UPDATE
            """, new { id = preview.Id, classroomId }, transaction);
        if (current is null || current.Title != preview.Title || current.Value != preview.Value || current.GradeCount != preview.GradeCount)
            throw new McpException("The evaluation changed after confirmation; request deletion again.");
        await connection.ExecuteAsync("DELETE FROM grades WHERE evaluation_id=@id", new { id = preview.Id }, transaction);
        await connection.ExecuteAsync("DELETE FROM evaluations WHERE evaluation_id=@id AND classroom_id=@classroomId", new { id = preview.Id, classroomId }, transaction);
        await transaction.CommitAsync();
        return JsonSerializer.Serialize(new { classroomCode, evaluationTitle, deletedGrades = preview.GradeCount, deleted = true });
    }

    [McpServerTool(Name = "read_classroom_attachment")]
    [Description("Lee el archivo about del aula o statement/rubric de una evaluación propia, en base64, máximo 1 MiB. No permite acceder a feedback de estudiantes.")]
    public async Task<string> ReadClassroomAttachmentAsync(string classroomCode, string kind, string? evaluationTitle = null)
    {
        var reference = await GetAttachmentAsync(classroomCode, kind, evaluationTitle);
        if (string.IsNullOrEmpty(reference)) throw new FileNotFoundException("This attachment is not set.");
        ValidateStoredFileName(reference);
        if (!new[] { ".pdf", ".txt" }.Contains(Path.GetExtension(reference), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only PDF and TXT attachments can be read through MCP.");
        await using var stream = await _files.GetFileAsync(reference)
            ?? throw new FileNotFoundException("The attachment is missing from storage.");
        if (stream.Length > MaxAttachmentBytes) throw new InvalidOperationException("The attachment exceeds the 1 MiB MCP limit.");
        using var content = new MemoryStream();
        await stream.CopyToAsync(content);
        if (content.Length > MaxAttachmentBytes) throw new InvalidOperationException("The attachment exceeds the 1 MiB MCP limit.");
        return JsonSerializer.Serialize(new { classroomCode, kind, evaluationTitle, fileName = AttachmentName(reference), contentBase64 = Convert.ToBase64String(content.ToArray()) });
    }

    [McpServerTool(Name = "replace_classroom_attachment")]
    [Description("Reemplaza el archivo about, statement o rubric de un aula propia. Acepta PDF o TXT en base64 hasta 1 MiB. Requiere permiso write e instrucción explícita.")]
    public async Task<string> ReplaceClassroomAttachmentAsync(
        string classroomCode, string kind, string fileName, string contentBase64, string? evaluationTitle = null)
    {
        EnsureWriteAccess();
        ValidateClassroomCode(classroomCode);
        ValidateAttachmentKind(kind, evaluationTitle);
        ValidateStoredFileName(fileName);
        if (fileName.Length > 180 || !new[] { ".pdf", ".txt" }.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("The file name must be at most 180 characters and end in .pdf or .txt.", nameof(fileName));
        if (string.IsNullOrEmpty(contentBase64) || contentBase64.Length > ((MaxAttachmentBytes + 2) / 3) * 4)
            throw new ArgumentException("The base64 attachment must be nonempty and no larger than 1 MiB.", nameof(contentBase64));
        var bytes = Convert.FromBase64String(contentBase64);
        if (bytes.Length is 0 or > MaxAttachmentBytes) throw new ArgumentException("The attachment must be nonempty and no larger than 1 MiB.", nameof(contentBase64));
        var professorId = GetProfessorId();
        using var connection = CreateConnection();
        await connection.OpenAsync();
        using var transaction = await connection.BeginTransactionAsync();
        var classroomId = await LockOwnedClassroomAsync(connection, transaction, classroomCode, professorId);
        int? evaluationId = null;
        if (kind != "about")
            evaluationId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT evaluation_id FROM evaluations WHERE classroom_id=@classroomId AND title=@evaluationTitle",
                new { classroomId, evaluationTitle }, transaction)
                ?? throw new KeyNotFoundException("The evaluation was not found.");
        await using var content = new MemoryStream(bytes);
        var form = new FormFile(content, 0, bytes.Length, "file", fileName);
        var storedName = await _files.UploadFileAsync(form);
        var sql = kind switch
        {
            "about" => "UPDATE classrooms SET about=@storedName WHERE classroom_id=@classroomId",
            "statement" => "UPDATE evaluations SET statement=@storedName WHERE evaluation_id=@evaluationId AND classroom_id=@classroomId",
            _ => "UPDATE evaluations SET rubric=@storedName WHERE evaluation_id=@evaluationId AND classroom_id=@classroomId"
        };
        await connection.ExecuteAsync(sql, new { storedName, classroomId, evaluationId }, transaction);
        await transaction.CommitAsync();
        return JsonSerializer.Serialize(new { classroomCode, kind, evaluationTitle, fileName, updated = true });
    }

    private async Task<string?> GetAttachmentAsync(string classroomCode, string kind, string? evaluationTitle)
    {
        ValidateClassroomCode(classroomCode);
        ValidateAttachmentKind(kind, evaluationTitle);
        var professorId = GetProfessorId();
        await EnsureClassroomOwnedAsync(classroomCode, professorId);
        using var connection = CreateConnection();
        if (kind == "about")
            return await connection.QuerySingleAsync<string?>(
                "SELECT about FROM classrooms WHERE classroomCode=@classroomCode AND professor_id=@professorId",
                new { classroomCode, professorId });
        var evaluation = await connection.QuerySingleOrDefaultAsync<EvaluationAttachmentState>(
            """
            SELECT e.statement AS Statement, e.rubric AS Rubric
            FROM evaluations e JOIN classrooms c ON c.classroom_id=e.classroom_id
            WHERE c.classroomCode=@classroomCode AND c.professor_id=@professorId AND e.title=@evaluationTitle
            """, new { classroomCode, professorId, evaluationTitle })
            ?? throw new KeyNotFoundException("The evaluation was not found.");
        return kind == "statement" ? evaluation.Statement : evaluation.Rubric;
    }

    private static void ValidateAttachmentKind(string kind, string? evaluationTitle)
    {
        if (kind is not ("about" or "statement" or "rubric"))
            throw new ArgumentException("kind must be 'about', 'statement', or 'rubric'.", nameof(kind));
        if (kind == "about" && evaluationTitle is not null)
            throw new ArgumentException("Do not provide an evaluation title for the classroom about file.", nameof(evaluationTitle));
        if (kind != "about") ValidateText(evaluationTitle, nameof(evaluationTitle), 200);
    }

    private static void ValidateStoredFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.Contains('/') || name.Contains('\\') || name.Contains(':') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("A plain file name without path separators is required.", nameof(name));
    }

    private static string? AttachmentName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        ValidateStoredFileName(name);
        var separator = name.IndexOf("00_PAPI_", StringComparison.Ordinal);
        return separator >= 0 ? name[(separator + "00_PAPI_".Length)..] : name;
    }

    private static async Task<int> LockOwnedClassroomAsync(
        MySql.Data.MySqlClient.MySqlConnection connection, System.Data.IDbTransaction transaction,
        string classroomCode, string professorId)
    {
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT classroom_id FROM classrooms WHERE classroomCode=@classroomCode AND professor_id=@professorId FOR UPDATE",
            new { classroomCode, professorId }, transaction)
            ?? throw new KeyNotFoundException("The classroom was not found for the authenticated professor.");
    }

    public sealed record ClassroomScheduleInput(string Day, string From, string To);
    public sealed record StudentGradeInput(string StudentName, decimal Score);
    private sealed record DeletionState(string ProfessorId, string ClassroomCode, DeletionPreview Preview);
    private sealed class EvaluationAttachmentState
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public decimal Value { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Subject { get; set; }
        public string? Statement { get; set; }
        public string? Rubric { get; set; }
    }
    private sealed class DeletionPreview
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public decimal Value { get; set; }
        public int GradeCount { get; set; }
    }
}
