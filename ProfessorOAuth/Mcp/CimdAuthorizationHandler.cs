using System.Text.Json;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace ProfessorOAuth.Mcp;

public sealed class CimdAuthorizationHandler(
    CimdMetadataFetcher fetcher,
    IOpenIddictApplicationManager applications,
    IConfiguration configuration,
    ILogger<CimdAuthorizationHandler> logger)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
{
    private const string MetadataMarker = "cimd";

    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
    {
        var clientId = context.ClientId;
        if (string.IsNullOrEmpty(clientId) ||
            !clientId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var challenge = context.Request.CodeChallenge;
        if (context.Request.ResponseType != "code" ||
            context.Request.CodeChallengeMethod != "S256" ||
            challenge is not { Length: 43 } ||
            challenge.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest,
                "A code response and valid S256 PKCE challenge are required.");
            return;
        }

        CimdClientMetadata metadata;
        try
        {
            metadata = await fetcher.FetchAsync(clientId, context.CancellationToken);
        }
        catch (Exception exception) when (exception is CimdMetadataException or HttpRequestException or
                                           IOException or OperationCanceledException)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning("Client metadata could not be retrieved or validated: {ErrorType}.",
                exception.GetType().Name);
            context.Reject(OpenIddictConstants.Errors.InvalidClient,
                "Client metadata could not be retrieved or validated.");
            return;
        }

        if (context.Request.RedirectUri is null ||
            !metadata.RedirectUris!.Contains(context.Request.RedirectUri, StringComparer.Ordinal))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest, "The redirect URI is not registered.");
            return;
        }

        var application = await applications.FindByClientIdAsync(clientId, context.CancellationToken);
        var descriptor = new OpenIddictApplicationDescriptor();
        if (application is not null)
        {
            await applications.PopulateAsync(descriptor, application, context.CancellationToken);
            if (!descriptor.Properties.TryGetValue(MetadataMarker, out var marker) ||
                marker.ValueKind != JsonValueKind.True)
            {
                context.Reject(OpenIddictConstants.Errors.InvalidClient,
                    "This client identifier is already registered.");
                return;
            }
        }

        descriptor.ClientId = clientId;
        descriptor.ClientType = OpenIddictConstants.ClientTypes.Public;
        descriptor.ConsentType = OpenIddictConstants.ConsentTypes.Explicit;
        descriptor.DisplayName = string.IsNullOrWhiteSpace(metadata.ClientName) ? clientId : metadata.ClientName;
        descriptor.Properties[MetadataMarker] = JsonSerializer.SerializeToElement(true);
        descriptor.RedirectUris.Clear();
        foreach (var redirect in metadata.RedirectUris!)
        {
            descriptor.RedirectUris.Add(new Uri(redirect, UriKind.Absolute));
        }
        descriptor.Permissions.Clear();
        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
            OpenIddictConstants.Permissions.Prefixes.Scope + (configuration["McpOAuth:ReadScope"] ?? "classroom:read"),
            OpenIddictConstants.Permissions.Prefixes.Scope + (configuration["McpOAuth:WriteScope"] ?? "classroom:write")
        ]);
        if (metadata.GrantTypes?.Contains("refresh_token", StringComparer.Ordinal) == true)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        }
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        if (application is null)
        {
            await applications.CreateAsync(descriptor, context.CancellationToken);
        }
        else
        {
            await applications.UpdateAsync(application, descriptor, context.CancellationToken);
        }
    }
}
