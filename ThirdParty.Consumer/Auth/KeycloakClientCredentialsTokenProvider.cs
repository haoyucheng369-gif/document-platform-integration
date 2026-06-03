using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Platform.DotNetApi.Auth;

namespace ThirdParty.Consumer.Auth;

public class KeycloakClientCredentialsTokenProvider : IAccessTokenProvider
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public KeycloakClientCredentialsTokenProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && _expiresAt > DateTimeOffset.UtcNow.Add(RefreshSkew))
        {
            return _accessToken;
        }

        await _refreshLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && _expiresAt > DateTimeOffset.UtcNow.Add(RefreshSkew))
            {
                return _accessToken;
            }

            var httpClient = _httpClientFactory.CreateClient();
            using var response = await httpClient.PostAsync(GetRequiredSetting("DocuwareClient:TokenEndpoint"), new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = GetRequiredSetting("DocuwareClient:ClientId"),
                ["client_secret"] = GetRequiredSetting("DocuwareClient:ClientSecret")
            }));

            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException("Keycloak returned an empty access token.");
            }

            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

            return _accessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private string GetRequiredSetting(string key)
    {
        return _configuration[key] ?? throw new InvalidOperationException($"{key} is required");
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
