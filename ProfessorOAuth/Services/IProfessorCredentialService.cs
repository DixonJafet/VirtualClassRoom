namespace ProfessorOAuth.Services;

public interface IProfessorCredentialService
{
    Task<ProfessorAccount?> AuthenticateAsync(string email, string password);
}

public sealed record ProfessorAccount(string ProfessorId, string? Name);
