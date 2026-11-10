using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using ProfessorOAuth.Configuration;

namespace ProfessorOAuth.Certificates;

/// <summary>
/// Retrieves OpenIddict signing and encryption certificates from Azure Key Vault using Managed Identity.
/// Private keys are kept in memory using ephemeral storage and disposed at application shutdown.
/// </summary>
public sealed class KeyVaultCertificateProvider : IOAuthCertificateProvider
{
    private readonly KeyVaultOptions _options;
    private readonly SecretClient _secretClient;
    private OAuthCertificates? _certificates;
    private bool _disposed;

    public KeyVaultCertificateProvider(IOptions<KeyVaultOptions> options, TokenCredential? credential = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.VaultUri))
        {
            throw new InvalidOperationException("KeyVault:VaultUri must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.SigningCertificateName))
        {
            throw new InvalidOperationException("KeyVault:SigningCertificateName must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.EncryptionCertificateName))
        {
            throw new InvalidOperationException("KeyVault:EncryptionCertificateName must be configured.");
        }

        var vaultUri = new Uri(_options.VaultUri);
        var cred = credential ?? new DefaultAzureCredential();
        _secretClient = new SecretClient(vaultUri, cred);
    }

    /// <summary>
    /// Constructor for custom/mock SecretClient (useful for unit testing and custom client configurations).
    /// </summary>
    public KeyVaultCertificateProvider(KeyVaultOptions options, SecretClient secretClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
    }

    public async Task<OAuthCertificates> LoadCertificatesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_certificates is not null)
        {
            return _certificates;
        }

        var signingCertificate = await FetchCertificateAsync(
            _options.SigningCertificateName,
            "signing",
            cancellationToken);

        X509Certificate2? encryptionCertificate = null;
        try
        {
            encryptionCertificate = await FetchCertificateAsync(
                _options.EncryptionCertificateName,
                "encryption",
                cancellationToken);

            if (string.Equals(signingCertificate.Thumbprint, encryptionCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "OpenIddict signing and encryption certificates must be distinct. Reusing the same certificate for both purposes is not permitted.");
            }

            _certificates = new OAuthCertificates(signingCertificate, encryptionCertificate);
            return _certificates;
        }
        catch
        {
            signingCertificate.Dispose();
            encryptionCertificate?.Dispose();
            throw;
        }
    }

    private async Task<X509Certificate2> FetchCertificateAsync(
        string certificateName,
        string purpose,
        CancellationToken cancellationToken)
    {
        KeyVaultSecret secret;
        try
        {
            var response = await _secretClient.GetSecretAsync(certificateName, null, cancellationToken);
            if (response?.Value is null)
            {
                throw new InvalidOperationException(
                    $"Key Vault secret for OpenIddict {purpose} certificate '{certificateName}' was not found.");
            }
            secret = response.Value;
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve OpenIddict {purpose} certificate '{certificateName}' from Key Vault '{_options.VaultUri}'. " +
                $"Status: {ex.Status}, ErrorCode: {ex.ErrorCode}. Ensure the managed identity has the 'Key Vault Secrets User' role.", ex);
        }

        if (string.IsNullOrWhiteSpace(secret.Value))
        {
            throw new InvalidOperationException(
                $"Key Vault secret for OpenIddict {purpose} certificate '{certificateName}' was empty.");
        }

        byte[] pfxBytes;
        try
        {
            pfxBytes = Convert.FromBase64String(secret.Value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Key Vault secret for OpenIddict {purpose} certificate '{certificateName}' was not valid base64 PFX data.", ex);
        }

        X509Certificate2 certificate;
        try
        {
            try
            {
                // EphemeralKeySet ensures private key is kept in memory and not persisted to disk / user profile.
                // X509CertificateLoader is the .NET 9+ preferred API (avoids SYSLIB0057).
                certificate = X509CertificateLoader.LoadPkcs12(
                    pfxBytes,
                    password: null,
                    keyStorageFlags: X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (CryptographicException)
            {
                // If the secret contains a raw certificate without private key, load it so ValidateCertificate can report the missing private key
                certificate = X509CertificateLoader.LoadCertificate(pfxBytes);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse OpenIddict {purpose} certificate '{certificateName}' from Key Vault secret data.", ex);
        }

        ValidateCertificate(certificate, certificateName, purpose);
        return certificate;
    }

    private static void ValidateCertificate(X509Certificate2 certificate, string name, string purpose)
    {
        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"The OpenIddict {purpose} certificate '{name}' does not contain a usable private key.");
        }

        var now = DateTimeOffset.UtcNow;
        if (now < certificate.NotBefore)
        {
            var notBefore = certificate.NotBefore;
            certificate.Dispose();
            throw new InvalidOperationException(
                $"The OpenIddict {purpose} certificate '{name}' is not yet valid (valid from {notBefore:u}).");
        }

        if (now > certificate.NotAfter)
        {
            var notAfter = certificate.NotAfter;
            certificate.Dispose();
            throw new InvalidOperationException(
                $"The OpenIddict {purpose} certificate '{name}' has expired (expired at {notAfter:u}).");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _certificates?.Dispose();
        _certificates = null;
    }
}
