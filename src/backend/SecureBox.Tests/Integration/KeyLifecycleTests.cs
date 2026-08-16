using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SecureBox.Core.DTOs;

namespace SecureBox.Tests.Integration;

public class KeyLifecycleTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public KeyLifecycleTests(CustomWebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadEncryptRetrieveRoundTrip()
    {
        var login = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var pem = CreatePemWithPrivateKey();
        var upload = await _client.PostAsJsonAsync("/api/v1/Certificates", new
        {
            name = "Test Encryption Cert",
            description = "integration",
            certificateFile = pem,
            password = (string?)null,
            isForSigning = false,
            isForEncryption = true
        });

        var uploadBody = await upload.Content.ReadAsStringAsync();
        upload.StatusCode.Should().Be(HttpStatusCode.Created, uploadBody);
        var cert = await upload.Content.ReadFromJsonAsync<ApiEnvelope<CertificateDto>>();
        cert!.Data.IsForEncryption.Should().BeTrue();

        var create = await _client.PostAsJsonAsync("/api/v1/Keys", new CreateKeyRequest(
            "TEST_SECRET",
            "roundtrip",
            "Secret",
            "super-secret-value-42",
            cert.Data.CertificateId,
            "AES256",
            "DEV"));

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<ApiEnvelope<KeyDto>>();
        created!.Data.Name.Should().Be("TEST_SECRET");

        var retrieve = await _client.PostAsJsonAsync($"/api/v1/Keys/{created.Data.KeyId}/retrieve", new
        {
            password = "Admin@123",
            reason = "integration-test"
        });

        retrieve.StatusCode.Should().Be(HttpStatusCode.OK);
        var secret = await retrieve.Content.ReadFromJsonAsync<ApiEnvelope<RetrieveKeyResponse>>();
        secret!.Data.Value.Should().Be("super-secret-value-42");

        var metrics = await _client.GetAsync("/api/v1/metrics");
        metrics.StatusCode.Should().Be(HttpStatusCode.OK);

        var health = await _client.GetAsync("/health/live");
        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RetrieveWithoutPassword_FailsValidation()
    {
        var login = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var pem = CreatePemWithPrivateKey();
        var upload = await _client.PostAsJsonAsync("/api/v1/Certificates", new
        {
            name = "NoPw Cert",
            certificateFile = pem,
            isForEncryption = true
        });
        upload.EnsureSuccessStatusCode();
        var cert = await upload.Content.ReadFromJsonAsync<ApiEnvelope<CertificateDto>>();
        var create = await _client.PostAsJsonAsync("/api/v1/Keys", new CreateKeyRequest(
            "NOPW", "x", "Secret", "hidden", cert!.Data.CertificateId, "AES256", "DEV"));
        create.EnsureSuccessStatusCode();
        var key = await create.Content.ReadFromJsonAsync<ApiEnvelope<KeyDto>>();

        var response = await _client.PostAsJsonAsync($"/api/v1/Keys/{key!.Data.KeyId}/retrieve", new { reason = "no-password" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<AuthResponse> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/Auth/login", new LoginRequest("admin", "Admin@123"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        return payload!.Data;
    }

    private static byte[] CreatePemWithPrivateKey()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=SecureBox Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        var pem = cert.ExportCertificatePem() + Environment.NewLine + rsa.ExportPkcs8PrivateKeyPem();
        return System.Text.Encoding.UTF8.GetBytes(pem);
    }
}
