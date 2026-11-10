namespace ProfessorOAuth.Certificates;

/// <summary>
/// Provides signing and encryption certificates for OpenIddict.
/// </summary>
public interface IOAuthCertificateProvider : IDisposable
{
    Task<OAuthCertificates> LoadCertificatesAsync(CancellationToken cancellationToken = default);
}

