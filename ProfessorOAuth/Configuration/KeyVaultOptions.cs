namespace ProfessorOAuth.Configuration;

public sealed class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public string? VaultUri { get; set; }
    public string SigningCertificateName { get; set; } = "openiddict-signing";
    public string EncryptionCertificateName { get; set; } = "openiddict-encryption";
}

