using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SecureBox.Core.DTOs;
using SecureBox.Infrastructure.Security;

namespace SecureBox.Tests.Integration;

public class MfaTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MfaTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SetupEnableAndChallenge()
    {
        var login = await _client.PostAsJsonAsync("/api/v1/Auth/login", new LoginRequest("admin", "Admin@123"));
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        session!.Data.RequiresMfa.Should().BeFalse();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Data.AccessToken);

        var setupResp = await _client.PostAsJsonAsync("/api/v1/Auth/mfa/setup", new { });
        setupResp.EnsureSuccessStatusCode();
        var setup = await setupResp.Content.ReadFromJsonAsync<ApiEnvelope<MfaSetupDto>>();
        setup!.Data.Secret.Should().NotBeNullOrWhiteSpace();

        var code = Totp.GenerateCode(setup.Data.Secret);
        var enable = await _client.PostAsJsonAsync("/api/v1/Auth/mfa/enable", new MfaEnableRequest(code));
        enable.StatusCode.Should().Be(HttpStatusCode.OK);

        _client.DefaultRequestHeaders.Authorization = null;
        var challenged = await _client.PostAsJsonAsync("/api/v1/Auth/login", new LoginRequest("admin", "Admin@123"));
        challenged.EnsureSuccessStatusCode();
        var challenge = await challenged.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        challenge!.Data.RequiresMfa.Should().BeTrue();
        challenge.Data.AccessToken.Should().BeNull();

        var verifyCode = Totp.GenerateCode(setup.Data.Secret);
        var verify = await _client.PostAsJsonAsync("/api/v1/Auth/mfa/verify", new MfaVerifyRequest(challenge.Data.MfaChallengeId!, verifyCode));
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var verified = await verify.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        verified!.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
    }
}
