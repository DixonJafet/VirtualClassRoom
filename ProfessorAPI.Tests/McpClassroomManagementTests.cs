using System.Security.Claims;
using ProfessorAPI.Connexions;
using ProfessorAPI.Interface;
using ProfessorAPI.Mcp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using ModelContextProtocol;
using Xunit;

namespace ProfessorAPI.Tests;

public class McpClassroomManagementTests
{
    [Fact]
    public async Task Mutations_RequireWriteAccess()
    {
        var tools = CreateTools(write: false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tools.SetClassroomScheduleAsync("CLASS", []));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tools.SetStudentGradesAsync("CLASS", "Quiz", [new("Alice", 5)]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tools.ReplaceClassroomAttachmentAsync("CLASS", "about", "file.txt", "aGk="));
    }

    [Fact]
    public async Task Schedule_RejectsInvalidOrReversedTimesBeforeChangingData()
    {
        var tools = CreateTools(write: true);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.SetClassroomScheduleAsync("CLASS", [new("Monday", "12:00", "11:00")], true));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.SetClassroomScheduleAsync("CLASS", [new("Invalid", "09:00", "10:00")], true));
    }

    [Fact]
    public async Task BatchGrades_RejectsEmptyOrDuplicateNamesBeforeChangingData()
    {
        var tools = CreateTools(write: true);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.SetStudentGradesAsync("CLASS", "Quiz", [], true));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.SetStudentGradesAsync("CLASS", "Quiz", [new("Alice", 1), new("alice", 2)], true));
    }

    [Fact]
    public async Task Attachments_RejectPathsAndUnapprovedKinds()
    {
        var tools = CreateTools(write: true);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.ReplaceClassroomAttachmentAsync("CLASS", "about", "../file.txt", "aGk=", confirmed: true));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.ReplaceClassroomAttachmentAsync("CLASS", "feedback", "file.txt", "aGk=", confirmed: true));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.ReplaceClassroomAttachmentAsync("CLASS", "about", "file.exe", "aGk=", confirmed: true));
    }

    [Fact]
    public async Task EditsAndDeletion_RequireConfirmation()
    {
        var tools = CreateTools(write: true);
        await Assert.ThrowsAsync<McpException>(() => tools.UpdateClassroomAsync("CLASS", "New"));
        await Assert.ThrowsAsync<McpException>(() => tools.UpdateEvaluationAsync("CLASS", "Old", "New", 10, DateTime.Today));
        await Assert.ThrowsAsync<McpException>(() => tools.SetStudentGradeAsync("CLASS", "Alice", "Quiz", 5));
        await Assert.ThrowsAsync<McpException>(() => tools.SetClassroomScheduleAsync("CLASS", []));
        await Assert.ThrowsAsync<McpException>(() => tools.SetStudentGradesAsync("CLASS", "Quiz", [new("Alice", 5)]));
        await Assert.ThrowsAsync<McpException>(() => tools.ReplaceClassroomAttachmentAsync("CLASS", "about", "file.txt", "aGk="));
        await Assert.ThrowsAsync<McpException>(() => tools.DeleteEvaluationAsync("CLASS", "Quiz"));
    }

    private static AnalyticsTools CreateTools(bool write)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "professor-1"),
            new("mcp_access", "read")
        };
        if (write) claims.Add(new Claim("mcp_access", "write"));
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:MySQLConnection"] = "Server=localhost;Database=test;User=test",
                ["FileStorage:Path"] = Path.GetTempPath()
            }).Build();
        var environment = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath());
        return new AnalyticsTools(
            new HttpContextAccessor { HttpContext = context },
            configuration,
            Mock.Of<InterProfessors>(),
            new BlobService(configuration, environment));
    }
}
