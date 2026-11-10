using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ProfessorOAuth.Tests.Helpers;

public static class CertificateTestHelper
{
    public static (X509Certificate2 Certificate, string Base64Pfx) CreateTestCertificateWithPfx(
        string subjectName,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool includePrivateKey = true)
    {
        RSA rsa;
        if (OperatingSystem.IsWindows())
        {
            var cngParams = new CngKeyCreationParameters
            {
                ExportPolicy = CngExportPolicies.AllowPlaintextExport
            };
            var key = CngKey.Create(CngAlgorithm.Rsa, null, cngParams);
            rsa = new RSACng(key);
        }
        else
        {
            rsa = RSA.Create(2048);
        }

        using (rsa)
        {
            var request = new CertificateRequest(
                $"CN={subjectName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            using var cert = request.CreateSelfSigned(notBefore, notAfter);

            if (includePrivateKey)
            {
                var pfxBytes = cert.Export(X509ContentType.Pfx);
                var base64 = Convert.ToBase64String(pfxBytes);
                var certInstance = X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
                return (certInstance, base64);
            }

            var pubBytes = cert.Export(X509ContentType.Cert);
            var pubBase64 = Convert.ToBase64String(pubBytes);
            return (X509CertificateLoader.LoadCertificate(pubBytes), pubBase64);
        }
    }
}
