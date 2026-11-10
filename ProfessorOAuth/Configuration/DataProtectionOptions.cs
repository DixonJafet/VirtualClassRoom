namespace ProfessorOAuth.Configuration;

public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public string? BlobContainerUri { get; set; }
    public string? KeyVaultKeyUri { get; set; }
}

