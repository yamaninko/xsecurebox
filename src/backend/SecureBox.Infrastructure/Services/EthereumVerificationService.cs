using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;
using SecureBox.Infrastructure.Ethereum;

namespace SecureBox.Infrastructure.Services;

public sealed class DisabledChainVerificationService : IChainVerificationService
{
    public bool IsEnabled => false;

    public Task RegisterAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task EnsureVerifiedAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RevokeAsync(Guid keyId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class EthereumVerificationService : IChainVerificationService
{
    public const string AnvilDevKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

    private readonly SecureBoxDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EthereumVerificationService> _logger;
    private readonly string[] _rpcUrls;
    private readonly string _privateKey;
    private readonly int _chainId;
    private readonly bool _requireOnRetrieve;
    private readonly int _quorum;

    public EthereumVerificationService(
        SecureBoxDbContext db,
        IConfiguration configuration,
        ILogger<EthereumVerificationService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
        _rpcUrls = ReadRpcUrls(configuration);
        _privateKey = configuration["Ethereum:PrivateKey"] ?? AnvilDevKey;
        _chainId = int.TryParse(configuration["Ethereum:ChainId"], out var id) ? id : 4242;
        _requireOnRetrieve = !string.Equals(configuration["Ethereum:RequireOnRetrieve"], "false", StringComparison.OrdinalIgnoreCase);
        _quorum = int.TryParse(configuration["Ethereum:Quorum"], out var q) ? Math.Max(1, q) : _rpcUrls.Length;
        IsEnabled = _rpcUrls.Length > 0;
    }

    public bool IsEnabled { get; }

    public async Task RegisterAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        var contract = await EnsureContractAsync(cancellationToken);
        var payload = CommitmentHasher.PayloadHash(ciphertext, iv, tag);
        var algorithm = CommitmentHasher.AlgorithmId();
        var id = CommitmentHasher.KeyId32(keyId);

        var account = new Account(_privateKey, _chainId);
        var web3 = new Web3(account, _rpcUrls[0]);
        var fn = web3.Eth.GetContract(EthereumArtifacts.Abi, contract).GetFunction("register");
        var receipt = await fn.SendTransactionAndWaitForReceiptAsync(
            account.Address,
            new HexBigInteger(500_000),
            null,
            null,
            id,
            payload,
            algorithm);

        var key = await _db.Keys.FirstOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);
        if (key != null)
        {
            key.ChainPayloadHash = CommitmentHasher.ToHex(payload);
            key.ChainTxHash = receipt.TransactionHash;
            key.ChainBlockNumber = (long?)receipt.BlockNumber?.Value;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Registered key {KeyId} on chain tx {Tx} block {Block}",
            keyId,
            receipt.TransactionHash,
            receipt.BlockNumber?.Value);
    }

    public async Task EnsureVerifiedAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        var key = await _db.Keys.AsNoTracking().FirstOrDefaultAsync(k => k.KeyId == keyId, cancellationToken);
        if (key?.ChainPayloadHash is null)
        {
            if (_requireOnRetrieve)
            {
                throw new InvalidOperationException("Key is not sealed on the SecureBox Ethereum registry");
            }

            return;
        }

        var payload = CommitmentHasher.PayloadHash(ciphertext, iv, tag);
        var expected = CommitmentHasher.ToHex(payload);
        if (!string.Equals(key.ChainPayloadHash, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Ciphertext hash does not match the sealed commitment");
        }

        var contract = await EnsureContractAsync(cancellationToken);
        var id = CommitmentHasher.KeyId32(keyId);
        var votes = 0;
        foreach (var rpc in _rpcUrls)
        {
            try
            {
                var web3 = new Web3(rpc);
                var ok = await web3.Eth.GetContract(EthereumArtifacts.Abi, contract)
                    .GetFunction("verify")
                    .CallAsync<bool>(id, payload);
                if (ok)
                {
                    votes++;
                }
                else
                {
                    _logger.LogWarning("ETH node {Rpc} rejected commitment for {KeyId}", rpc, keyId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ETH node {Rpc} verify failed", rpc);
            }
        }

        if (votes < _quorum)
        {
            throw new InvalidOperationException(
                $"Ethereum quorum failed for key ({votes}/{_quorum} nodes confirmed the hash)");
        }
    }

    public async Task RevokeAsync(Guid keyId, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        var contract = await EnsureContractAsync(cancellationToken);
        var account = new Account(_privateKey, _chainId);
        var web3 = new Web3(account, _rpcUrls[0]);
        var fn = web3.Eth.GetContract(EthereumArtifacts.Abi, contract).GetFunction("revoke");
        await fn.SendTransactionAndWaitForReceiptAsync(
            account.Address,
            new HexBigInteger(200_000),
            null,
            null,
            CommitmentHasher.KeyId32(keyId));
    }

    public async Task<string> EnsureContractAsync(CancellationToken cancellationToken = default)
    {
        var configured = _configuration["Ethereum:ContractAddress"];
        var state = await _db.ChainStates.FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (!string.IsNullOrWhiteSpace(state?.ContractAddress))
        {
            return state!.ContractAddress!;
        }

        var systemName = _configuration["Ethereum:SystemName"] ?? "xsecurebox";
        var systemId = CommitmentHasher.SystemId(systemName);
        var account = new Account(_privateKey, _chainId);
        var web3 = new Web3(account, _rpcUrls[0]);

        var receipt = await web3.Eth.GetContractDeploymentHandler<SecureBoxRegistryDeployment>()
            .SendRequestAndWaitForReceiptAsync(new SecureBoxRegistryDeployment { SystemId = systemId });

        var address = receipt.ContractAddress
                      ?? throw new InvalidOperationException("Contract deploy returned no address");

        if (state is null)
        {
            state = new ChainState { Id = 1 };
            _db.ChainStates.Add(state);
        }

        state.ContractAddress = address;
        state.SystemId = CommitmentHasher.ToHex(systemId);
        state.DeployTxHash = receipt.TransactionHash;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deployed SecureBoxRegistry at {Address} system {SystemId}", address, state.SystemId);
        return address;
    }

    private static string[] ReadRpcUrls(IConfiguration configuration)
    {
        var section = configuration.GetSection("Ethereum:RpcUrls");
        var listed = section.Get<string[]>() ?? Array.Empty<string>();
        if (listed.Length > 0)
        {
            return listed.Where(u => !string.IsNullOrWhiteSpace(u)).ToArray();
        }

        var csv = configuration["Ethereum:RpcUrl"];
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
