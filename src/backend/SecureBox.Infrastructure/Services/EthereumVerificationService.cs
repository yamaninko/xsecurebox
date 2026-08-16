using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using SecureBox.Core.DTOs;
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

    public Task<ChainDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyDashboard());

    public Task<ChainDashboardDto> UpdateSettingsAsync(ChainSettingsRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyDashboard());

    public Task<ChainDashboardDto> RedeployAsync(string? systemName, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyDashboard());

    public Task<ChainDashboardDto> ScaleClusterAsync(int nodeCount, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyDashboard());

    private static ChainDashboardDto EmptyDashboard() =>
        new(false, null, null, null, null, null, null, 0, 1, false, "", "", Ethereum.EthereumArtifacts.Source, Ethereum.EthereumArtifacts.Abi, Array.Empty<ChainNodeDto>(), Array.Empty<SealedKeyDto>(), 0, 7, 0, false, null, null, null, null);
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
        _quorum = int.TryParse(configuration["Ethereum:Quorum"], out var q) ? Math.Max(1, q) : Math.Max(1, _rpcUrls.Length);
        IsEnabled = configuration.GetValue<bool>("Ethereum:Enabled") || _rpcUrls.Length > 0;
    }

    public bool IsEnabled { get; }

    public async Task RegisterAsync(Guid keyId, byte[] ciphertext, byte[] iv, byte[] tag, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        var runtime = await LoadRuntimeAsync(cancellationToken);
        if (runtime.RpcUrls.Length == 0)
        {
            throw new InvalidOperationException("No Ethereum RPC URL configured");
        }

        var contract = await EnsureContractAsync(cancellationToken);
        var payload = CommitmentHasher.PayloadHash(ciphertext, iv, tag);
        var algorithm = CommitmentHasher.AlgorithmId();
        var id = CommitmentHasher.KeyId32(keyId);

        var account = new Account(_privateKey, runtime.ChainId);
        var web3 = new Web3(account, runtime.RpcUrls[0]);
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
        var runtime = await LoadRuntimeAsync(cancellationToken);
        if (key?.ChainPayloadHash is null)
        {
            if (runtime.RequireOnRetrieve)
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
        foreach (var rpc in runtime.RpcUrls)
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

        if (votes < runtime.Quorum)
        {
            throw new InvalidOperationException(
                $"Ethereum quorum failed for key ({votes}/{runtime.Quorum} nodes confirmed the hash)");
        }
    }

    public async Task RevokeAsync(Guid keyId, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        var runtime = await LoadRuntimeAsync(cancellationToken);
        var contract = await EnsureContractAsync(cancellationToken);
        var account = new Account(_privateKey, runtime.ChainId);
        var web3 = new Web3(account, runtime.RpcUrls[0]);
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

        var runtime = await LoadRuntimeAsync(cancellationToken);
        if (runtime.RpcUrls.Length == 0)
        {
            throw new InvalidOperationException("No Ethereum RPC URL configured");
        }

        var systemName = runtime.SystemName;
        var systemId = CommitmentHasher.SystemId(systemName);
        var account = new Account(_privateKey, runtime.ChainId);
        var web3 = new Web3(account, runtime.RpcUrls[0]);

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

    public async Task<ChainDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var runtime = await LoadRuntimeAsync(cancellationToken);
        var cluster = await SupervisorSnapshotAsync(cancellationToken);
        var probeUrls = new List<(string Name, string Url, string Role)>();
        foreach (var node in cluster.Nodes)
        {
            probeUrls.Add((node.Name, node.Url, node.Index == 1 ? "sealer" : "replica"));
        }

        foreach (var url in runtime.RpcUrls)
        {
            if (probeUrls.Any(p => string.Equals(p.Url, url, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var role = url.Contains("eth-lb", StringComparison.OrdinalIgnoreCase) ? "load-balancer" : "rpc";
            probeUrls.Add((role == "load-balancer" ? "eth-lb" : url, url, role));
        }

        if (!probeUrls.Any(p => p.Role == "load-balancer"))
        {
            probeUrls.Add(("eth-lb", "http://eth-lb:8545", "load-balancer"));
        }

        var nodes = new List<ChainNodeDto>();
        foreach (var item in probeUrls)
        {
            nodes.Add(await ProbeNodeAsync(item.Name, item.Url, item.Role));
        }

        string? owner = null;
        bool? paused = null;
        string? onChainSystemId = null;
        string? contractError = null;
        var readUrl = nodes.FirstOrDefault(n => n.Reachable && n.Role != "load-balancer")?.Url
                      ?? nodes.FirstOrDefault(n => n.Reachable)?.Url;
        if (!string.IsNullOrWhiteSpace(runtime.ContractAddress) && readUrl != null)
        {
            try
            {
                var web3 = new Web3(readUrl);
                var contract = web3.Eth.GetContract(EthereumArtifacts.Abi, runtime.ContractAddress);
                owner = await contract.GetFunction("owner").CallAsync<string>();
                paused = await contract.GetFunction("paused").CallAsync<bool>();
                var sid = await contract.GetFunction("systemId").CallAsync<byte[]>();
                if (sid is { Length: > 0 })
                {
                    onChainSystemId = CommitmentHasher.ToHex(sid);
                }
            }
            catch (Exception ex)
            {
                contractError = ex.Message;
                _logger.LogWarning(ex, "Could not read contract view state");
            }
        }
        else if (string.IsNullOrWhiteSpace(runtime.ContractAddress))
        {
            contractError = "Kontrat adresi yok. Yeni kontrat yayınlayın.";
        }

        var sealedKeys = await _db.Keys.AsNoTracking()
            .Where(k => k.ChainTxHash != null)
            .OrderByDescending(k => k.UpdatedAt)
            .Take(25)
            .Select(k => new SealedKeyDto(k.KeyId, k.Name, k.ChainPayloadHash, k.ChainTxHash, k.ChainBlockNumber))
            .ToListAsync(cancellationToken);

        var account = new Account(_privateKey, runtime.ChainId);
        var lb = nodes.FirstOrDefault(n => n.Role == "load-balancer");
        var workers = nodes.Where(n => n.Role is "sealer" or "replica").ToList();
        return new ChainDashboardDto(
            IsEnabled,
            runtime.ContractAddress,
            runtime.SystemId,
            runtime.SystemName,
            runtime.DeployTxHash,
            owner,
            paused,
            runtime.ChainId,
            runtime.Quorum,
            runtime.RequireOnRetrieve,
            string.Join("\n", runtime.RpcUrls),
            account.Address,
            EthereumArtifacts.Source,
            EthereumArtifacts.Abi,
            nodes,
            sealedKeys,
            cluster.Count,
            cluster.Max,
            workers.Count(n => n.Reachable),
            cluster.SupervisorOk,
            "http://eth-lb:8545",
            lb?.Reachable,
            onChainSystemId,
            contractError);
    }

    public async Task<ChainDashboardDto> UpdateSettingsAsync(ChainSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var state = await _db.ChainStates.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new ChainState { Id = 1 };
            _db.ChainStates.Add(state);
        }

        if (request.RpcUrlsText != null)
        {
            state.RpcUrls = request.RpcUrlsText;
        }

        if (request.Quorum.HasValue)
        {
            state.Quorum = Math.Max(1, request.Quorum.Value);
        }

        if (request.SystemName != null)
        {
            state.SystemName = request.SystemName.Trim();
        }

        if (request.RequireOnRetrieve.HasValue)
        {
            state.RequireOnRetrieve = request.RequireOnRetrieve;
        }

        if (!string.IsNullOrWhiteSpace(request.ContractAddress))
        {
            state.ContractAddress = request.ContractAddress.Trim();
        }

        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (request.Paused.HasValue || !string.IsNullOrWhiteSpace(request.NewOwner))
        {
            var runtime = await LoadRuntimeAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(runtime.ContractAddress) || runtime.RpcUrls.Length == 0)
            {
                throw new InvalidOperationException("Contract is not deployed");
            }

            var account = new Account(_privateKey, runtime.ChainId);
            var web3 = new Web3(account, runtime.RpcUrls[0]);
            var contract = web3.Eth.GetContract(EthereumArtifacts.Abi, runtime.ContractAddress);

            if (request.Paused.HasValue)
            {
                await contract.GetFunction("setPaused").SendTransactionAndWaitForReceiptAsync(
                    account.Address, new HexBigInteger(120_000), null, null, request.Paused.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.NewOwner))
            {
                await contract.GetFunction("transferOwnership").SendTransactionAndWaitForReceiptAsync(
                    account.Address, new HexBigInteger(120_000), null, null, request.NewOwner.Trim());
            }
        }

        return await GetDashboardAsync(cancellationToken);
    }

    public async Task<ChainDashboardDto> RedeployAsync(string? systemName, CancellationToken cancellationToken = default)
    {
        var state = await _db.ChainStates.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new ChainState { Id = 1 };
            _db.ChainStates.Add(state);
        }

        if (!string.IsNullOrWhiteSpace(systemName))
        {
            state.SystemName = systemName.Trim();
        }

        state.ContractAddress = null;
        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await EnsureContractAsync(cancellationToken);
        return await GetDashboardAsync(cancellationToken);
    }

    public async Task<ChainDashboardDto> ScaleClusterAsync(int nodeCount, CancellationToken cancellationToken = default)
    {
        var supervisor = SupervisorUrl();
        if (string.IsNullOrWhiteSpace(supervisor))
        {
            throw new InvalidOperationException("Ethereum supervisor is not configured");
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
        var response = await http.PostAsJsonAsync(
            supervisor.TrimEnd('/') + "/scale",
            new { count = nodeCount },
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(body);
        }

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var urls = new List<string>();
        if (doc.RootElement.TryGetProperty("nodes", out var nodesEl))
        {
            foreach (var node in nodesEl.EnumerateArray())
            {
                if (node.TryGetProperty("url", out var url))
                {
                    urls.Add(url.GetString() ?? "");
                }
            }
        }

        var state = await _db.ChainStates.FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new ChainState { Id = 1 };
            _db.ChainStates.Add(state);
        }

        var all = new List<string> { "http://eth-lb:8545" };
        all.AddRange(urls.Where(u => !string.IsNullOrWhiteSpace(u) && !u.Contains("eth-lb", StringComparison.Ordinal)));
        state.RpcUrls = string.Join("\n", all);
        if (!state.Quorum.HasValue || state.Quorum.Value > urls.Count)
        {
            state.Quorum = Math.Max(1, (urls.Count + 1) / 2);
        }

        state.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetDashboardAsync(cancellationToken);
    }

    private string? SupervisorUrl() => _configuration["Ethereum:SupervisorUrl"];

    private sealed record SupervisorNode(int Index, string Name, string Url);

    private sealed record SupervisorSnapshot(bool SupervisorOk, int Count, int Max, IReadOnlyList<SupervisorNode> Nodes);

    private async Task<SupervisorSnapshot> SupervisorSnapshotAsync(CancellationToken cancellationToken)
    {
        var supervisor = SupervisorUrl();
        if (string.IsNullOrWhiteSpace(supervisor))
        {
            return new SupervisorSnapshot(false, 0, 7, Array.Empty<SupervisorNode>());
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var json = await http.GetStringAsync(supervisor.TrimEnd('/') + "/status", cancellationToken);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var count = doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
            var max = doc.RootElement.TryGetProperty("max", out var m) ? m.GetInt32() : 7;
            var nodes = new List<SupervisorNode>();
            if (doc.RootElement.TryGetProperty("nodes", out var arr))
            {
                foreach (var n in arr.EnumerateArray())
                {
                    nodes.Add(new SupervisorNode(
                        n.TryGetProperty("index", out var i) ? i.GetInt32() : 0,
                        n.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                        n.TryGetProperty("url", out var url) ? url.GetString() ?? "" : ""));
                }
            }

            return new SupervisorSnapshot(true, count, max, nodes);
        }
        catch
        {
            return new SupervisorSnapshot(false, 0, 7, Array.Empty<SupervisorNode>());
        }
    }

    private async Task<Runtime> LoadRuntimeAsync(CancellationToken cancellationToken)
    {
        var state = await _db.ChainStates.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var urls = ParseUrls(state?.RpcUrls);
        if (urls.Length == 0)
        {
            urls = _rpcUrls;
        }

        var quorum = state?.Quorum ?? _quorum;
        if (quorum < 1)
        {
            quorum = 1;
        }

        return new Runtime(
            urls,
            Math.Min(quorum, Math.Max(1, urls.Length)),
            state?.SystemName ?? _configuration["Ethereum:SystemName"] ?? "xsecurebox",
            state?.RequireOnRetrieve ?? _requireOnRetrieve,
            state?.ContractAddress ?? _configuration["Ethereum:ContractAddress"],
            state?.SystemId,
            state?.DeployTxHash,
            _chainId);
    }

    private static async Task<ChainNodeDto> ProbeNodeAsync(string name, string url, string role)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var block = await RpcHexAsync(http, url, "eth_blockNumber");
            var chain = await RpcHexAsync(http, url, "eth_chainId");
            var peers = await RpcHexAsync(http, url, "net_peerCount");
            var miningEl = await RpcRawAsync(http, url, "eth_mining");
            var syncEl = await RpcRawAsync(http, url, "eth_syncing");
            var versionEl = await RpcRawAsync(http, url, "web3_clientVersion");
            sw.Stop();
            var mining = miningEl.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
                ? miningEl.GetBoolean()
                : (bool?)null;
            var syncing = syncEl.ValueKind == System.Text.Json.JsonValueKind.False
                ? false
                : syncEl.ValueKind == System.Text.Json.JsonValueKind.Object;
            return new ChainNodeDto(
                name,
                url,
                role,
                true,
                block,
                chain,
                peers.HasValue ? (int)peers.Value : null,
                syncing,
                mining,
                versionEl.ValueKind == System.Text.Json.JsonValueKind.String ? versionEl.GetString() : null,
                sw.ElapsedMilliseconds,
                null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ChainNodeDto(name, url, role, false, null, null, null, null, null, null, sw.ElapsedMilliseconds, ex.Message);
        }
    }

    private static async Task<long?> RpcHexAsync(HttpClient http, string url, string method)
    {
        var el = await RpcRawAsync(http, url, method);
        if (el.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return null;
        }

        var hex = el.GetString();
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        return Convert.ToInt64(hex.Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
    }

    private static async Task<System.Text.Json.JsonElement> RpcRawAsync(HttpClient http, string url, string method)
    {
        var payload = new { jsonrpc = "2.0", method, @params = Array.Empty<object>(), id = 1 };
        using var resp = await http.PostAsJsonAsync(url, payload);
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (doc.RootElement.TryGetProperty("error", out var err))
        {
            throw new InvalidOperationException(err.ToString());
        }

        return doc.RootElement.GetProperty("result").Clone();
    }

    private sealed record Runtime(
        string[] RpcUrls,
        int Quorum,
        string SystemName,
        bool RequireOnRetrieve,
        string? ContractAddress,
        string? SystemId,
        string? DeployTxHash,
        int ChainId);

    private static string[] ParseUrls(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
