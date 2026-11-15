# Remote MCP

The API exposes an MCP Streamable HTTP endpoint at `https://classroomapi.runasp.net/mcp`. OAuth clients discover authentication through `https://classroomapi.runasp.net/.well-known/oauth-protected-resource`. Read tools provide classroom listing, evaluation curves, normalized student rankings (top or bottom), comparisons, upcoming evaluations, and percentage-range searches. Every tool and resource is limited to the professor associated with its access token. There is no arbitrary-SQL tool.

The MCP also exposes:

- Resources: `classrooms://mine` and `classroom://{classroomCode}/evaluations` for discovering owned classrooms and their evaluation values and due dates.
- Prompt templates: `analyze_classroom`, `rank_students`, `compare_evaluations`, and `plan_next_week`.

For example, “show me the five worst students in this class” maps to `rank_students` with `direction="bottom"` and `limit=5`. Without an evaluation title, it ranks by the sum of course percentages from evaluations due today or earlier; provide a specific title to rank by that evaluation's contribution. `availableWeight` identifies the included portion of the course. Evaluation curves and comparisons also show percentages relative to each evaluation's value.

## Write access

Write tools can create or rename a classroom, create or update an evaluation, and set a student's grade. They do not delete records, modify student or professor account information, or change evaluation/classroom file attachments. Grade updates preserve the existing feedback reference and reject scores outside the evaluation's range. A grade row must already exist for the student and evaluation; the application normally creates these rows when it creates an evaluation.

Evaluation create and update tools require `weight` (the evaluation's maximum contribution to the course). The combined values of evaluations in a classroom cannot exceed 100%.

OAuth access tokens need the `classroom:read` scope. Tokens granted `classroom:write` in addition to `classroom:read` can also use write tools. The validated token subject (`sub`) must match an existing professor ID. Treat a write-enabled token as sensitive: it can change classroom names, evaluation details, and student scores. Claude should only call a write tool when the user explicitly requests that change.

## External OpenIddict OAuth service

OAuth authorization is hosted separately by the `ProfessorOAuth` ASP.NET Core service. Configure `McpOAuth__Issuer` on that service to its canonical public HTTPS URL (for example, an Azure App Service URL), and configure `McpOAuth__ResourceUrl` to this API's MCP URL, `https://classroomapi.runasp.net/mcp`. Discovery, authorization, token, and dynamic registration endpoints are served by `ProfessorOAuth` at `/.well-known/openid-configuration`, `/connect/authorize`, `/connect/token`, and `/connect/register`.

The OAuth service verifies existing professor email/password credentials against the same MySQL `professors` table and stores OpenIddict clients, authorizations, and tokens in the existing OpenIddict tables. It does not migrate professor accounts or change the website's existing `POST /api/profes/LogIn` JWT flow. Grant MCP access only to a trusted OAuth service; it requires read access to professor credential hashes and write access to the OpenIddict tables.

Set `McpOAuth__Issuer` on the API to the OAuth service's exact issuer URL and `McpOAuth__ResourceUrl` to the API's MCP URL. The API validates the external issuer's signing keys and tokens remotely; it no longer loads or needs the OAuth private certificates. The protected-resource metadata remains at the API's `/.well-known/oauth-protected-resource`.

MCP OAuth is optional on the API. If `McpOAuth__Issuer` is unset, the API still starts and the regular website/JWT endpoints remain available; OAuth bearer tokens are rejected and OAuth protected-resource metadata returns service unavailable. Legacy database-backed MCP tokens remain independently supported. Set a valid HTTPS issuer and resource URL when the OAuth service is ready.

Registered public clients are restricted to authorization-code + S256 PKCE and exact registered redirect URIs. Users see a consent screen before approving `classroom:read` and, optionally, `classroom:write`. Access tokens last 15 minutes and refresh tokens 30 days. The API validates the token signature, issuer, audience, scopes, and that `sub` still maps to an existing professor ID. The write scope enables MCP write tools.

The consent page's Content Security Policy permits form submission to the
authorization service and the origin of the validated client redirect URI.
Browsers need the latter to follow the authorization response after consent.

After consent, the OAuth service stores an authorization with its approved
scopes. Users can review and revoke authorizations at the OAuth service's
`/connect/authorizations` page using their existing professor credentials. The
API checks the authorization status on every OAuth request; revoked grants
cannot obtain refresh tokens or access MCP. Requests already in flight may
complete. Legacy database-backed MCP tokens are unaffected.

The OAuth service must be deployed before the API version that requires the
`mcp_authorization_id` claim. Existing OAuth access and refresh tokens without
that claim cannot be used with the updated API; affected clients must reconnect.
This is necessary because older tokens cannot be linked reliably to a
user-revocable authorization. The website login and legacy MCP tokens are
independent.

The OAuth server registers exactly the configured MCP resource with OpenIddict.
Per-client resource permissions are ignored so existing client registrations
remain usable; the registered-resource validation still rejects every other
resource. Revisit this policy before adding additional resources.

Client ID Metadata Documents (CIMD) are implemented behind the
`McpOAuth__EnableCimd` setting, which defaults to false. Enable it only after
the client-ID migration and coordinated OAuth/API deployment have completed.
With CIMD disabled, discovery does not advertise it.
The new client-ID schema migration is a prerequisite.
It expands the indexed ID to 512 characters with a case-sensitive collation.
Review the generated SQL, index limits, backup and rollback implications on the
target MySQL server before applying it. Rolling back may fail or lose data if
IDs exceed 100 characters or differ only by case.

The CIMD metadata fetcher connects only to public IPv4
addresses and does not follow redirects, so IPv6-only metadata hosts are not
currently supported. The fetcher accepts clients declaring `none` in
`token_endpoint_auth_methods_supported`, or, if that field is absent, declaring
`token_endpoint_auth_method` as `none`. Discovery advertises `none` so clients
that also support private-key authentication can select the public-client flow.
Public `jwks_uri` metadata is accepted but never fetched or used; inline keys
and shared secrets are rejected. Private-key client authentication is not
implemented for CIMD clients. Client metadata is re-fetched for authorization requests;
redirect URIs must match exactly. The client name is self-asserted; the consent
page also shows the client identifier so users can verify its origin.

Refresh tokens rotate without a reuse grace period. Clients must serialize
refresh requests and save the newly issued refresh token.

### OAuth service production configuration

Set these environment variables on `ProfessorOAuth`:

- `ConnectionStrings__MySQLConnection`: connection to the same MySQL database as the API.
- `McpOAuth__Issuer`: the service's canonical public HTTPS URL.
- `McpOAuth__ResourceUrl`: the API's canonical `/mcp` URL.
- `KeyVault__VaultUri`, `KeyVault__SigningCertificateName`, and
  `KeyVault__EncryptionCertificateName`.
- `DataProtection__BlobContainerUri` and `DataProtection__KeyVaultKeyUri`.

Keep private keys and passwords out of source control. The Azure service loads
the certificates from Key Vault using its managed identity and stores Data
Protection keys in Blob Storage protected by a Key Vault key. Do not use the
website's `JWTkey:key` as an OAuth signing key.

The existing MySQL connection credential is shared with this service and must be rotated if exposed. The OAuth service is not ready for deployment until the actual Azure issuer URL, certificate storage, and its restricted database credentials are configured.

Existing database-backed MCP tokens remain supported independently of OAuth. They can still be created/revoked through `/api/profes/mcp/token`.

## Database setup

Apply [`Database/Migrations/001_CreateMcpAccessTokens.sql`](./Database/Migrations/001_CreateMcpAccessTokens.sql) and then [`Database/Migrations/002_AddMcpTokenAccessLevel.sql`](./Database/Migrations/002_AddMcpTokenAccessLevel.sql) to the same MySQL database used by the API before using legacy MCP token routes.

The OpenIddict client, authorization, scope, and token tables are managed by EF Core in the `ProfessorOAuth` project. The initial migration and client-ID expansion have been applied to the current production database. For a new database, install an EF CLI compatible with the project's EF Core 9 packages, configure `ConnectionStrings__MySQLConnection`, then run `dotnet ef database update --project ProfessorOAuth/ProfessorOAuth.csproj`. Do not run this against production without reviewing and approving pending migrations.

## Legacy token creation and revocation

While signed in to the website, send an authenticated `POST` request to `/api/profes/mcp/token` to create a database-backed token. The request may contain `{"accessLevel":"read"}` or `{"accessLevel":"write"}`; it defaults to `write`. The response contains the remote MCP URL and a one-year token. The plaintext token is returned only when it is created; the database stores its SHA-256 hash and access level. These legacy tokens are separate from OpenIddict OAuth.

To invalidate every legacy MCP token for the signed-in professor, send an authenticated `DELETE` request to `/api/profes/mcp/token`.

## Data interpretation

Percentages are normalized by each evaluation's `value`. Overall rankings and range searches without a specific evaluation include evaluations whose due date is today or earlier. The current grade schema does not record whether a score was reviewed, so a zero cannot reliably be distinguished from an automatically-created pending placeholder. Tool results call out this limitation.

## References

- [OpenIddict server guide](https://documentation.openiddict.com/guides/getting-started/creating-your-own-server-instance)
- [OpenIddict Entity Framework Core integration](https://documentation.openiddict.com/integrations/entity-framework-core)
