using System.Security.Cryptography.X509Certificates;

namespace ProfessorOAuth.Certificates;

/// <summary>
/// Holds the loaded OpenIddict signing and encryption certificates and ensures deterministic disposal at shutdown.
/// </summary>
public sealed class OAuthCertificates : IDisposable
{
    private bool _disposed;

    public OAuthCertificates(X509Certificate2 signingCertificate, X509Certificate2 encryptionCertificate)
    {
        SigningCertificate = signingCertificate ?? throw new ArgumentNullException(nameof(signingCertificate));
        EncryptionCertificate = encryptionCertificate ?? throw new ArgumentNullException(nameof(encryptionCertificate));
    }

    public X509Certificate2 SigningCertificate { get; }
    public X509Certificate2 EncryptionCertificate { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SigningCertificate.Dispose();
        EncryptionCertificate.Dispose();
    }
}

