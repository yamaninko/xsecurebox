using SecureBox.Core.DTOs;

namespace SecureBox.Core.Interfaces;

public interface IChainVerificationService
{
    bool IsEnabled { get; }

    Task RegisterAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default);

    Task EnsureVerifiedAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid keyId, CancellationToken cancellationToken = default);

    Task<ChainDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<ChainDashboardDto> UpdateSettingsAsync(ChainSettingsRequest request, CancellationToken cancellationToken = default);

    Task<ChainDashboardDto> RedeployAsync(string? systemName, CancellationToken cancellationToken = default);

    Task<ChainDashboardDto> ScaleClusterAsync(int nodeCount, CancellationToken cancellationToken = default);
}
