namespace SecureBox.Core.Interfaces;

public interface IChainVerificationService
{
    bool IsEnabled { get; }

    Task RegisterAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default);

    Task EnsureVerifiedAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid keyId, CancellationToken cancellationToken = default);
}
