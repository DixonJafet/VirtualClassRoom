using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Moq;
using ProfessorOAuth.Certificates;
using ProfessorOAuth.Configuration;
using ProfessorOAuth.Tests.Helpers;

namespace ProfessorOAuth.Tests.Certificates;

public class CertificateProviderTests
{
    [Fact]
    public async Task LoadCertificatesAsync_WithValidDistinctCertificates_Succeeds()
    {
        var now = DateTimeOffset.UtcNow;
        var (signingCert, signingPfxBase64) = CertificateTestHelper.CreateTestCertificateWithPfx("signing", now.AddMinutes(-5), now.AddYears(1));
        var (encCert, encPfxBase64) = CertificateTestHelper.CreateTestCertificateWithPfx("encryption", now.AddMinutes(-5), now.AddYears(1));

        using (signingCert)
        using (encCert)
        {
            var mockSecretClient = new Mock<SecretClient>();
            mockSecretClient
                .Setup(c => c.GetSecretAsync("openiddict-signing", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("openiddict-signing"), signingPfxBase64), Mock.Of<Response>()));

            mockSecretClient
                .Setup(c => c.GetSecretAsync("openiddict-encryption", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("openiddict-encryption"), encPfxBase64), Mock.Of<Response>()));

            var options = new KeyVaultOptions
            {
                VaultUri = "https://test-vault.vault.azure.net/",
                SigningCertificateName = "openiddict-signing",
                EncryptionCertificateName = "openiddict-encryption"
            };

            using var provider = new KeyVaultCertificateProvider(options, mockSecretClient.Object);
            var certificates = await provider.LoadCertificatesAsync();

            Assert.NotNull(certificates);
            Assert.NotNull(certificates.SigningCertificate);
            Assert.NotNull(certificates.EncryptionCertificate);
            Assert.True(certificates.SigningCertificate.HasPrivateKey);
            Assert.True(certificates.EncryptionCertificate.HasPrivateKey);
            Assert.NotEqual(certificates.SigningCertificate.Thumbprint, certificates.EncryptionCertificate.Thumbprint);
        }
    }

    [Fact]
    public async Task LoadCertificatesAsync_WhenSigningAndEncryptionAreSameCertificate_ThrowsInvalidOperationException()
    {
        var now = DateTimeOffset.UtcNow;
        var (reusedCert, reusedPfxBase64) = CertificateTestHelper.CreateTestCertificateWithPfx("shared", now.AddMinutes(-5), now.AddYears(1));

        using (reusedCert)
        {
            var mockSecretClient = new Mock<SecretClient>();
            mockSecretClient
                .Setup(c => c.GetSecretAsync("openiddict-signing", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("openiddict-signing"), reusedPfxBase64), Mock.Of<Response>()));

            mockSecretClient
                .Setup(c => c.GetSecretAsync("openiddict-encryption", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("openiddict-encryption"), reusedPfxBase64), Mock.Of<Response>()));

            var options = new KeyVaultOptions
            {
                VaultUri = "https://test-vault.vault.azure.net/",
                SigningCertificateName = "openiddict-signing",
                EncryptionCertificateName = "openiddict-encryption"
            };

            using var provider = new KeyVaultCertificateProvider(options, mockSecretClient.Object);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadCertificatesAsync());
            Assert.Contains("must be distinct", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LoadCertificatesAsync_WhenCertificateLacksPrivateKey_ThrowsInvalidOperationException()
    {
        var now = DateTimeOffset.UtcNow;
        var (noPrivateKeyCert, base64) = CertificateTestHelper.CreateTestCertificateWithPfx("publiconly", now.AddMinutes(-5), now.AddYears(1), includePrivateKey: false);

        using (noPrivateKeyCert)
        {
            var mockSecretClient = new Mock<SecretClient>();
            mockSecretClient
                .Setup(c => c.GetSecretAsync("openiddict-signing", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("openiddict-signing"), base64), Mock.Of<Response>()));

            var options = new KeyVaultOptions
            {
                VaultUri = "https://test-vault.vault.azure.net/",
                SigningCertificateName = "openiddict-signing",
                EncryptionCertificateName = "openiddict-encryption"
            };

            using var provider = new KeyVaultCertificateProvider(options, mockSecretClient.Object);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadCertificatesAsync());
            Assert.Contains("usable private key", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LoadCertificatesAsync_WhenCertificateIsExpired_ThrowsInvalidOperationException()
    {
        var now = DateTimeOffset.UtcNow;
        var (expiredCert, base64) = CertificateTestHelper.CreateTestCertificateWithPfx("expired", now.AddYears(-2), now.AddDays(-1));

        using (expiredCert)
        {
            var mockSecretClient = new Mock<SecretClient>();
            mockSecretClient
                .Setup(c => c.GetSecretAsync("openiddict-signing", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(SecretModelFactory.KeyVaultSecret(new SecretProperties("openiddict-signing"), base64), Mock.Of<Response>()));

            var options = new KeyVaultOptions
            {
                VaultUri = "https://test-vault.vault.azure.net/",
                SigningCertificateName = "openiddict-signing",
                EncryptionCertificateName = "openiddict-encryption"
            };

            using var provider = new KeyVaultCertificateProvider(options, mockSecretClient.Object);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadCertificatesAsync());
            Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task LoadCertificatesAsync_WhenKeyVaultAccessDenied_ThrowsClearExceptionWithoutFallback()
    {
        var mockSecretClient = new Mock<SecretClient>();
        mockSecretClient
            .Setup(c => c.GetSecretAsync("openiddict-signing", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Access denied by Key Vault firewall or RBAC"));

        var options = new KeyVaultOptions
        {
            VaultUri = "https://test-vault.vault.azure.net/",
            SigningCertificateName = "openiddict-signing",
            EncryptionCertificateName = "openiddict-encryption"
        };

        using var provider = new KeyVaultCertificateProvider(options, mockSecretClient.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadCertificatesAsync());
        Assert.Contains("Key Vault Secrets User", ex.Message);
        Assert.Contains("403", ex.Message);
    }

    [Fact]
    public void OAuthCertificates_Dispose_DisposesInnerCertificates()
    {
        var now = DateTimeOffset.UtcNow;
        var (signingCert, _) = CertificateTestHelper.CreateTestCertificateWithPfx("signing", now.AddMinutes(-5), now.AddYears(1));
        var (encCert, _) = CertificateTestHelper.CreateTestCertificateWithPfx("encryption", now.AddMinutes(-5), now.AddYears(1));

        var oauthCertificates = new OAuthCertificates(signingCert, encCert);
        oauthCertificates.Dispose();

        // Disposing again should be idempotent
        oauthCertificates.Dispose();
    }
}
