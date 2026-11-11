using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProfessorOAuth.Mcp;

public sealed class CimdMetadataFetcher
{
    private const int MaximumDocumentBytes = 65536;
    private readonly HttpClient _client;

    public CimdMetadataFetcher(HttpClient client) => _client = client;

    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            MaxResponseHeadersLength = 32,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = ConnectPublicAddressAsync
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    private static async ValueTask<Stream> ConnectPublicAddressAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = (await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken))
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork).ToArray();
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new HttpRequestException("Client metadata host resolved to a non-public address.");
        }

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                lastError = exception;
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("Could not connect to the client metadata host.", lastError);
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            // Only IPv4 connections are supported; IPv6 transition ranges are not fetched.
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 && bytes[0] < 224 &&
            !(bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) &&
            !(bytes[0] == 169 && bytes[1] == 254) &&
            !(bytes[0] == 168 && bytes[1] == 63 && bytes[2] == 129 && bytes[3] == 16) &&
            !(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) &&
            !(bytes[0] == 192 && bytes[1] == 168) &&
            !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) &&
            !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) &&
            !(bytes[0] == 192 && bytes[1] == 88 && bytes[2] == 99) &&
            !(bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)) &&
            !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
            !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
    }

    public static bool IsClientIdUrl(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 512 ||
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString() ||
            uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || value.Contains('#') ||
            value.Contains('\\') || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl))
        {
            return false;
        }

        var authorityEnd = value.IndexOfAny(['/', '?', '#'], "https://".Length);
        if (authorityEnd < 0 || value[authorityEnd] != '/')
        {
            return false;
        }
        if (value.AsSpan("https://".Length, authorityEnd - "https://".Length).Contains('@'))
        {
            return false;
        }

        var pathEnd = value.IndexOfAny(['?', '#'], authorityEnd);
        var path = value[authorityEnd..(pathEnd < 0 ? value.Length : pathEnd)];
        return !path.Split('/').Any(segment =>
        {
            var decoded = Uri.UnescapeDataString(segment);
            return decoded is "." or ".." || decoded.Contains('/') || decoded.Contains('\\');
        });
    }

    public async Task<CimdClientMetadata> FetchAsync(string clientId, CancellationToken cancellationToken)
    {
        if (!IsClientIdUrl(clientId))
        {
            throw new CimdMetadataException("Invalid client metadata URL.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        cancellationToken = timeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, clientId);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (response.StatusCode != HttpStatusCode.OK ||
            !(string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
              mediaType is not null && mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase) &&
              mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)) ||
            response.Content.Headers.ContentEncoding.Count != 0 ||
            response.Content.Headers.ContentLength > MaximumDocumentBytes)
        {
            throw new CimdMetadataException("Client metadata response is invalid.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + read > MaximumDocumentBytes)
            {
                throw new CimdMetadataException("Client metadata exceeds the maximum size.");
            }
            buffer.Write(chunk, 0, read);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                throw new CimdMetadataException("Client metadata must be an object with unique property names.");
            }

            if (document.RootElement.TryGetProperty("client_secret", out _) ||
                document.RootElement.TryGetProperty("client_secret_expires_at", out _) ||
                document.RootElement.TryGetProperty("jwks", out _))
            {
                throw new CimdMetadataException("Client credentials and key material are not supported.");
            }

            var metadata = document.RootElement.Deserialize<CimdClientMetadata>()
                ?? throw new CimdMetadataException("Client metadata is empty.");
            var hasAuthenticationMethods = document.RootElement.TryGetProperty(
                "token_endpoint_auth_methods_supported", out _);
            if (!string.Equals(metadata.ClientId, clientId, StringComparison.Ordinal) ||
                metadata.RedirectUris is not { Length: > 0 and <= 20 } ||
                metadata.RedirectUris.Any(uri => !IsAllowedRedirectUri(uri)) ||
                metadata.RedirectUris.Distinct(StringComparer.Ordinal).Count() != metadata.RedirectUris.Length ||
                metadata.GrantTypes is { } grants &&
                    (grants.Length > 20 || !grants.Contains("authorization_code", StringComparer.Ordinal) ||
                     grants.Any(grant => string.IsNullOrWhiteSpace(grant) || grant.Length > 256 || grant.Any(char.IsWhiteSpace)) ||
                     grants.Distinct(StringComparer.Ordinal).Count() != grants.Length) ||
                metadata.ResponseTypes is { } responses &&
                    (responses.Length > 20 || !responses.Contains("code", StringComparer.Ordinal) ||
                     responses.Any(response => string.IsNullOrWhiteSpace(response) || response.Length > 256) ||
                     responses.Distinct(StringComparer.Ordinal).Count() != responses.Length) ||
                (hasAuthenticationMethods
                    ? metadata.TokenEndpointAuthMethodsSupported is not { Length: > 0 and <= 20 } methods ||
                      methods.Any(method => string.IsNullOrWhiteSpace(method) || method.Length > 256 ||
                                            method.Any(char.IsWhiteSpace)) ||
                      methods.Distinct(StringComparer.Ordinal).Count() != methods.Length ||
                      !methods.Contains("none", StringComparer.Ordinal)
                    : metadata.TokenEndpointAuthMethod != "none") ||
                document.RootElement.TryGetProperty("jwks_uri", out var jwksUri) &&
                    (jwksUri.ValueKind != JsonValueKind.String || !IsClientIdUrl(jwksUri.GetString())) ||
                metadata.ClientName?.Length > 100)
            {
                throw new CimdMetadataException("Client metadata contains unsupported or invalid values.");
            }
            return metadata;
        }
        catch (JsonException exception)
        {
            throw new CimdMetadataException("Client metadata is not valid JSON.", exception);
        }
    }

    private static bool IsAllowedRedirectUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString() ||
            !string.IsNullOrEmpty(uri.UserInfo) || value!.Contains('#') ||
            value.Contains('\\') || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl))
        {
            return false;
        }

        var authorityStart = value.IndexOf("://", StringComparison.Ordinal) + 3;
        var authorityEnd = value.IndexOfAny(['/', '?'], authorityStart);
        if (authorityEnd < 0) authorityEnd = value.Length;
        if (value.AsSpan(authorityStart, authorityEnd - authorityStart).Contains('@'))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps ||
            uri.Scheme == Uri.UriSchemeHttp &&
            (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
             IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address));
    }
}

public sealed class CimdClientMetadata
{
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }
    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }
    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; set; }
    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; set; }
    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; set; }
    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; }
    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public string[]? TokenEndpointAuthMethodsSupported { get; set; }
}

public sealed class CimdMetadataException : Exception
{
    public CimdMetadataException(string message) : base(message) { }
    public CimdMetadataException(string message, Exception innerException) : base(message, innerException) { }
}
