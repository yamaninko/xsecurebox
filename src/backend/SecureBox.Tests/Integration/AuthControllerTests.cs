using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SecureBox.API.Middleware;
using SecureBox.Core.DTOs;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Tests.Integration;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ReturnsBadRequest_WithValidationErrors()
    {
        var request = new LoginRequest("", "");

        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        content.Should().NotBeNull();
        content!.Success.Should().BeFalse();
        content.Error.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var request = new LoginRequest("nonexistent", "wrongpass");

        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        content.Should().NotBeNull();
        content!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokensAndRoles()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", new LoginRequest("admin", "Admin@123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.Data.RequiresMfa.Should().BeFalse();
        payload.Data.User!.Username.Should().Be("admin");
        payload.Data.User.Roles.Should().Contain("Admin");
        payload.Data.User.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Logout_BlacklistsAccessToken()
    {
        var login = await LoginAdminAsync();

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        logoutRequest.Content = JsonContent.Create(new { });
        var logout = await _client.SendAsync(logoutRequest);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/Auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var meResponse = await _client.SendAsync(me);
        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        await LoginAdminAsync();
        var first = await _client.PostAsJsonAsync("/api/v1/Auth/refresh", new { });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstPayload = await first.Content.ReadFromJsonAsync<ApiEnvelope<TokenResponse>>();
        firstPayload!.Data.AccessToken.Should().NotBeNullOrWhiteSpace();

        var second = await _client.PostAsJsonAsync("/api/v1/Auth/refresh", new { });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<AuthResponse> LoginAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", new LoginRequest("admin", "Admin@123"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        return payload!.Data;
    }
}

public record ApiEnvelope<T>(bool Success, T Data, string? Message);
