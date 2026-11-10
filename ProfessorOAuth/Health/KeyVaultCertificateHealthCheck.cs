using Microsoft.Extensions.Diagnostics.HealthChecks;
using ProfessorOAuth.Certificates;

namespace ProfessorOAuth.Health;

public sealed class KeyVaultCertificateHealthCheck : IHealthCheck
{
    private readonly OAuthCertificates? _certificates;

    public KeyVaultCertificateHealthCheck(OAuthCertificates? certificates = null)
    {
        _certificates = certificates;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_certificates is null)
        {
            // In Development with ephemeral dev certs or if not injected
            return Task.FromResult(HealthCheckResult.Healthy("Development certificates in use or certificates managed externally."));
        }

        var now = DateTimeOffset.UtcNow;
        var signing = _certificates.SigningCertificate;
        var encryption = _certificates.EncryptionCertificate;

        if (!signing.HasPrivateKey || now < signing.NotBefore || now > signing.NotAfter)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Signing certificate is expired or invalid."));
        }

        if (!encryption.HasPrivateKey || now < encryption.NotBefore || now > encryption.NotAfter)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Encryption certificate is expired or invalid."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Certificates are valid."));
    }
}

