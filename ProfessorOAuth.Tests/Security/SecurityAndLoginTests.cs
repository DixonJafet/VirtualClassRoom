using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using ProfessorOAuth.Mcp;
using ProfessorOAuth.Services;

namespace ProfessorOAuth.Tests.Security;

public class SecurityAndLoginTests
{
    private static McpOAuthLoginController CreateController()
    {
        var mockProfessors = new Mock<IProfessorCredentialService>();
        var mockAntiforgery = new Mock<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        var mockLogger = new Mock<ILogger<McpOAuthLoginController>>();
        var mockUrlHelper = new Mock<IUrlHelper>();
        mockUrlHelper
            .Setup(u => u.IsLocalUrl(It.IsAny<string>()))
            .Returns<string>(url => !string.IsNullOrEmpty(url) && url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\"));

        return new McpOAuthLoginController(mockProfessors.Object, mockAntiforgery.Object, mockLogger.Object)
        {
            Url = mockUrlHelper.Object
        };
    }

    [Fact]
    public void McpOAuthLoginController_GetLoginPage_WithInvalidReturnUrl_ReturnsBadRequest()
    {
        var controller = CreateController();

        // Disallowed external or malformed return URLs
        var result1 = controller.GetLoginPage("https://malicious.com/steal-token");
        var result2 = controller.GetLoginPage("/api/other");
        var result3 = controller.GetLoginPage(null);

        Assert.IsType<BadRequestObjectResult>(result1);
        Assert.IsType<BadRequestObjectResult>(result2);
        Assert.IsType<BadRequestObjectResult>(result3);
    }

    [Fact]
    public async Task McpOAuthLoginController_SubmitLogin_WithInvalidReturnUrl_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.SubmitLogin("https://evil.com/callback", "prof@test.com", "pass123");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
