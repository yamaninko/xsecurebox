using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SecureBox.Core.DTOs;
using SecureBox.Infrastructure.Services;

namespace SecureBox.Tests.Integration;

public class HardeningTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public HardeningTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_UsesHttpOnlyCookie()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/Auth/login", new LoginRequest("admin", "Admin@123"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.StartsWith("sb_refresh=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var refresh = await _client.PostAsJsonAsync("/api/v1/Auth/refresh", new { });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await refresh.Content.ReadFromJsonAsync<ApiEnvelope<TokenResponse>>();
        payload!.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Sweep_ExpiresPastDueKeys()
    {
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var certId = await UploadCertAsync();

        var create = await _client.PostAsJsonAsync("/api/v1/Keys", new CreateKeyRequest(
            "EXPIRED_SOON",
            "sweep",
            "Secret",
            "old-secret",
            certId,
            "AES256",
            "DEV",
            null,
            null,
            null,
            DateTime.UtcNow.AddMinutes(-5)));
        create.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var sweep = scope.ServiceProvider.GetRequiredService<ILifecycleService>();
        var result = await sweep.SweepAsync();
        result.ExpiredKeys.Should().BeGreaterThanOrEqualTo(1);

        var stats = await _client.GetFromJsonAsync<ApiEnvelope<DashboardStatsDto>>("/api/v1/metrics");
        stats!.Data.ExpiredKeys.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ApiClient_CanRetrieveWithoutPassword()
    {
        var adminToken = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var certId = await UploadCertAsync();

        var createdKey = await _client.PostAsJsonAsync("/api/v1/Keys", new CreateKeyRequest(
            "CLIENT_SECRET",
            "client",
            "Secret",
            "machine-secret",
            certId,
            "AES256",
            "DEV"));
        createdKey.EnsureSuccessStatusCode();
        var key = (await createdKey.Content.ReadFromJsonAsync<ApiEnvelope<KeyDto>>())!.Data;

        var clientResp = await _client.PostAsJsonAsync("/api/v1/clients", new CreateApiClientRequest(
            "e2e-client",
            "test",
            new List<string> { "keys:read", "keys:retrieve" }));
        clientResp.EnsureSuccessStatusCode();
        var created = await clientResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var clientId = created.GetProperty("data").GetProperty("client").GetProperty("clientIdString").GetString();
        var clientSecret = created.GetProperty("data").GetProperty("clientSecret").GetString();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!,
            ["scope"] = "keys:read keys:retrieve"
        });
        var tokenResp = await _client.PostAsync("/api/v1/oauth/token", form);
        tokenResp.EnsureSuccessStatusCode();
        var oauth = await tokenResp.Content.ReadFromJsonAsync<OAuthTokenResponse>();
        oauth!.access_token.Should().NotBeNullOrWhiteSpace();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauth.access_token);
        var retrieve = await _client.PostAsJsonAsync($"/api/v1/Keys/{key.KeyId}/retrieve", new { reason = "machine" });
        retrieve.StatusCode.Should().Be(HttpStatusCode.OK);
        var secret = await retrieve.Content.ReadFromJsonAsync<ApiEnvelope<RetrieveKeyResponse>>();
        secret!.Data.Value.Should().Be("machine-secret");
    }

    [Fact]
    public async Task ApiClient_WithoutRetrieveScope_IsForbidden()
    {
        var adminToken = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var clientResp = await _client.PostAsJsonAsync("/api/v1/clients", new CreateApiClientRequest(
            "read-only-client",
            "test",
            new List<string> { "certs:read" }));
        clientResp.EnsureSuccessStatusCode();
        var created = await clientResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var clientId = created.GetProperty("data").GetProperty("client").GetProperty("clientIdString").GetString();
        var clientSecret = created.GetProperty("data").GetProperty("clientSecret").GetString();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!,
            ["scope"] = "certs:read"
        });
        var tokenResp = await _client.PostAsync("/api/v1/oauth/token", form);
        tokenResp.EnsureSuccessStatusCode();
        var oauth = await tokenResp.Content.ReadFromJsonAsync<OAuthTokenResponse>();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oauth!.access_token);
        var retrieve = await _client.PostAsJsonAsync($"/api/v1/Keys/{Guid.NewGuid()}/retrieve", new { reason = "nope" });
        retrieve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", new LoginRequest("admin", "Admin@123"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        return payload!.Data.AccessToken;
    }

    private async Task<Guid> UploadCertAsync()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Sweep Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var pem = Encoding.UTF8.GetBytes(cert.ExportCertificatePem() + Environment.NewLine + rsa.ExportPkcs8PrivateKeyPem());
        var upload = await _client.PostAsJsonAsync("/api/v1/Certificates", new
        {
            name = "Sweep " + Guid.NewGuid().ToString("N")[..6],
            certificateFile = pem,
            isForEncryption = true
        });
        upload.EnsureSuccessStatusCode();
        var created = await upload.Content.ReadFromJsonAsync<ApiEnvelope<CertificateDto>>();
        return created!.Data.CertificateId;
    }
}
