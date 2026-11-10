using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProfessorOAuth.Certificates;
using ProfessorOAuth.Health;
using ProfessorOAuth.Tests.Helpers;

namespace ProfessorOAuth.Tests.Health;

public class HealthCheckTests
{
    [Fact]
    public async Task KeyVaultCertificateHealthCheck_WithValidCertificates_ReturnsHealthy()
    {
        var now = DateTimeOffset.UtcNow;
        var (signingCert, _) = CertificateTestHelper.CreateTestCertificateWithPfx("signing", now.AddMinutes(-5), now.AddYears(1));
        var (encCert, _) = CertificateTestHelper.CreateTestCertificateWithPfx("encryption", now.AddMinutes(-5), now.AddYears(1));
        using var certs = new OAuthCertificates(signingCert, encCert);

        var healthCheck = new KeyVaultCertificateHealthCheck(certs);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        // Ensure no private key or thumbprint is leaked in description
        Assert.DoesNotContain(signingCert.Thumbprint, result.Description ?? string.Empty);
        Assert.DoesNotContain(encCert.Thumbprint, result.Description ?? string.Empty);
    }

    [Fact]
    public async Task KeyVaultCertificateHealthCheck_WithNullCertificates_ReturnsHealthyInDev()
    {
        var healthCheck = new KeyVaultCertificateHealthCheck(null);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
