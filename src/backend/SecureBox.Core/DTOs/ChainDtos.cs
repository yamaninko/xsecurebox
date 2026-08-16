namespace SecureBox.Core.DTOs;

public record ChainNodeDto(
    string Url,
    bool Reachable,
    long? BlockNumber,
    long? ChainId,
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
    int MaxNodeCount = 7
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
