using System.Net;
using System.Text;
using ProfessorOAuth.Mcp;

namespace ProfessorOAuth.Tests.Security;

public class CimdMetadataFetcherTests
{
    private const string ClientId = "https://client.example/metadata.json";

    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("168.63.129.16", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("172.31.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("2606:4700:4700::1111", false)]
    [InlineData("8.8.8.8", true)]
    public void AddressPolicy_RejectsUnsafeRanges(string address, bool expected) =>
        Assert.Equal(expected, CimdMetadataFetcher.IsPublicAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData("https://client.example/metadata.json", true)]
    [InlineData("https://client.example", false)]
    [InlineData("http://client.example/metadata", false)]
    [InlineData("https://user@client.example/metadata", false)]
    [InlineData("https://@client.example/metadata", false)]
    [InlineData("https://client.example/metadata#fragment", false)]
    [InlineData("https://client.example/metadata#", false)]
    [InlineData("https://client.example/white space", false)]
    [InlineData("https://client.example/a/../metadata", false)]
    [InlineData("https://client.example/%2e%2e/metadata", false)]
    [InlineData("https://client.example/a%2fb", false)]
    public void ClientId_ValidatesUrl(string id, bool expected) =>
        Assert.Equal(expected, CimdMetadataFetcher.IsClientIdUrl(id));

    [Fact]
    public async Task Fetch_ValidMetadata_ReturnsClient()
    {
        var fetcher = CreateFetcher("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"token_endpoint_auth_method":"none"}""");
        var result = await fetcher.FetchAsync(ClientId, CancellationToken.None);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Equal("https://client.example/callback", Assert.Single(result.RedirectUris!));
    }

    [Fact]
    public async Task Fetch_AdditionalClientGrants_DoNotPreventAuthorizationCodeSupport()
    {
        var fetcher = CreateFetcher("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"grant_types":["authorization_code","refresh_token","urn:ietf:params:oauth:grant-type:jwt-bearer"],"response_types":["code"],"token_endpoint_auth_method":"none"}""");
        var metadata = await fetcher.FetchAsync(ClientId, CancellationToken.None);
        Assert.Contains("urn:ietf:params:oauth:grant-type:jwt-bearer", metadata.GrantTypes!);
    }

    [Fact]
    public async Task Fetch_ChatGptStyleMetadata_AcceptsPublicClientMethod()
    {
        var fetcher = CreateFetcher("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"token_endpoint_auth_method":"private_key_jwt","token_endpoint_auth_methods_supported":["none","private_key_jwt"],"jwks_uri":"https://client.example/oauth/jwks.json"}""");
        var result = await fetcher.FetchAsync(ClientId, CancellationToken.None);
        Assert.Contains("none", result.TokenEndpointAuthMethodsSupported!);
    }

    [Theory]
    [InlineData("""{"client_id":"https://client.example:443/metadata.json","redirect_uris":["https://client.example/callback"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["http://evil.example/callback"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback#fragment"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://@client.example/callback"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback#"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"token_endpoint_auth_method":"client_secret_basic"}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"token_endpoint_auth_method":"none","token_endpoint_auth_methods_supported":["private_key_jwt"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"token_endpoint_auth_methods_supported":null}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"token_endpoint_auth_methods_supported":["none","none"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"token_endpoint_auth_method":"none","jwks_uri":"http://client.example/keys"}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"]}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"client_secret":"not-allowed"}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"jwks":{"keys":[{"d":"private"}]}}""")]
    [InlineData("""{"client_id":"https://client.example/metadata.json","redirect_uris":["https://client.example/callback"],"grant_types":["implicit"]}""")]
    [InlineData("""not-json""")]
    public async Task Fetch_InvalidMetadata_IsRejected(string json)
    {
        var fetcher = CreateFetcher(json);
        await Assert.ThrowsAsync<CimdMetadataException>(() => fetcher.FetchAsync(ClientId, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_Redirect_IsRejected()
    {
        var fetcher = CreateFetcher("", HttpStatusCode.Redirect);
        await Assert.ThrowsAsync<CimdMetadataException>(() => fetcher.FetchAsync(ClientId, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_OversizedDocument_IsRejected()
    {
        var fetcher = CreateFetcher(new string(' ', 65537));
        await Assert.ThrowsAsync<CimdMetadataException>(() => fetcher.FetchAsync(ClientId, CancellationToken.None));
    }

    [Theory]
    [InlineData("https://127.0.0.1/metadata")]
    [InlineData("https://169.254.169.254/metadata")]
    [InlineData("https://localhost/metadata")]
    public async Task Fetch_RealTransportRejectsNonPublicAddresses(string clientId)
    {
        using var client = CimdMetadataFetcher.CreateHttpClient();
        var fetcher = new CimdMetadataFetcher(client);
        await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.FetchAsync(clientId, CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_WrongContentType_IsRejected()
    {
        using var client = new HttpClient(new WrongContentTypeHandler());
        var fetcher = new CimdMetadataFetcher(client);
        await Assert.ThrowsAsync<CimdMetadataException>(() => fetcher.FetchAsync(ClientId, CancellationToken.None));
    }

    private static CimdMetadataFetcher CreateFetcher(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new StubHandler(content, status)));

    private sealed class WrongContentTypeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "text/html")
            });
    }

    private sealed class StubHandler(string content, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}
