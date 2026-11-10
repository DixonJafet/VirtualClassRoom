using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ProfessorAPI.Mcp;

[McpServerPromptType]
public sealed class AnalyticsPrompts
{
    [McpServerPrompt(Name = "analyze_classroom"), Description("Analiza el desempeño general de un aula y contesta una pregunta del profesor.")]
    public static string AnalyzeClassroom(
        [Description("Código del aula")] string classroomCode,
        [Description("Pregunta o aspecto a analizar")] string question)
    {
        return $"""
            Analiza los datos del aula {classroomCode} para responder: {question}

            Antes de concluir:
            - Usa las herramientas MCP de análisis disponibles y solo consulta datos de esta aula.
            - Interpreta cada nota como porcentaje de su propio valor máximo; para promedios, pondera sumando puntos obtenidos y máximos.
            - Distingue puntos porcentuales de puntos de calificación.
            - Explica qué evaluaciones se incluyeron y cualquier limitación de los datos.
            - No infieras que una nota cero está pendiente: el sistema no distingue ceros reales de placeholders.
            - No inventes datos que no devuelvan las herramientas.
            """;
    }

    [McpServerPrompt(Name = "rank_students"), Description("Prepara una respuesta sobre mejores o peores estudiantes de un aula.")]
    public static string RankStudents(
        [Description("Código del aula")] string classroomCode,
        [Description("Escribe 'top' para mejores o 'bottom' para peores")] string direction = "bottom",
        [Description("Número de estudiantes a incluir")] int limit = 5)
    {
        return $"""
            En el aula {classroomCode}, identifica los {limit} estudiantes en la posición {direction}.
            Usa la herramienta rank_students con direction="{direction}" y limit={limit}.
            Si no se especifica una evaluación, el ranking incluye evaluaciones cuya fecha de entrega ya llegó.
            Explica el porcentaje normalizado y las evaluaciones incluidas. Informa empates y limitaciones.
            Nunca interpretes automáticamente un cero como calificación pendiente o como calificación revisada.
            """;
    }

    [McpServerPrompt(Name = "compare_evaluations"), Description("Compara progreso entre dos evaluaciones de la misma aula.")]
    public static string CompareEvaluations(
        [Description("Código del aula")] string classroomCode,
        [Description("Título exacto de la evaluación inicial")] string firstEvaluation,
        [Description("Título exacto de la evaluación final")] string secondEvaluation)
    {
        return $"""
            Compara el progreso de estudiantes del aula {classroomCode} entre "{firstEvaluation}" y "{secondEvaluation}".
            Usa la herramienta compare_evaluations y compara porcentajes sobre el valor máximo de cada evaluación.
            Resume cuántos mejoraron, se mantuvieron o bajaron, destaca los cambios mayores y aclara que la diferencia
            está expresada en puntos porcentuales. No atribuyas causalidad ni infieras estados de revisión.
            """;
    }

    [McpServerPrompt(Name = "plan_next_week"), Description("Resume las próximas evaluaciones y sugiere un plan de seguimiento.")]
    public static string PlanNextWeek(
        [Description("Código del aula; déjalo vacío para consultar todas las aulas")] string? classroomCode = null,
        [Description("Días a consultar, entre 1 y 30")] int days = 7)
    {
        var classroomScope = string.IsNullOrWhiteSpace(classroomCode)
            ? "todas las aulas propias"
            : $"el aula {classroomCode}";

        return $"""
            Revisa las evaluaciones que vencen en los próximos {days} días en {classroomScope}.
            Usa get_upcoming_evaluations con days={days}{(string.IsNullOrWhiteSpace(classroomCode) ? "" : $" y classroomCode=\"{classroomCode}\"")}.
            Ordena el resumen por fecha y aula. Sugiere un plan de seguimiento basado en fechas y valores máximos,
            sin afirmar recuentos de tareas pendientes: el esquema no registra de forma confiable el estado revisado.
            """;
    }
}
