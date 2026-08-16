namespace SecureBox.Core.Entities;

public class ChainState
{
    public int Id { get; set; } = 1;
    public string? ContractAddress { get; set; }
    public string? SystemId { get; set; }
    public string? DeployTxHash { get; set; }
    public string? RpcUrls { get; set; }
    public int? Quorum { get; set; }
    public string? SystemName { get; set; }
    public bool? RequireOnRetrieve { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
