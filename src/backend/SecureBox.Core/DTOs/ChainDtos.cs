namespace SecureBox.Core.DTOs;

public record ChainNodeDto(
    string Name,
    string Url,
    string Role,
    bool Reachable,
    long? BlockNumber,
    long? ChainId,
    int? PeerCount,
    bool? Syncing,
    bool? Mining,
    string? ClientVersion,
    long? LatencyMs,
    string? Error
);

public record SealedKeyDto(
    Guid KeyId,
    string Name,
    string? PayloadHash,
    string? TxHash,
    long? BlockNumber
);

public record ChainDashboardDto(
    bool Enabled,
    string? ContractAddress,
    string? SystemId,
    string? SystemName,
    string? DeployTxHash,
    string? Owner,
    bool? Paused,
    int ConfiguredChainId,
    int Quorum,
    bool RequireOnRetrieve,
    string RpcUrlsText,
    string OperatorAddress,
    string ContractSource,
    string Abi,
    IReadOnlyList<ChainNodeDto> Nodes,
    IReadOnlyList<SealedKeyDto> SealedKeys,
    int RunningNodeCount = 0,
    int MaxNodeCount = 7,
    int HealthyNodeCount = 0,
    bool SupervisorReachable = false,
    string? LoadBalancerUrl = null,
    bool? LoadBalancerReachable = null,
    string? OnChainSystemId = null,
    string? ContractReadError = null
);

public record ChainScaleRequest(int NodeCount);

public record ChainSettingsRequest(
    string? RpcUrlsText,
    int? Quorum,
    string? SystemName,
    bool? RequireOnRetrieve,
    string? ContractAddress,
    bool? Paused,
    string? NewOwner
);
