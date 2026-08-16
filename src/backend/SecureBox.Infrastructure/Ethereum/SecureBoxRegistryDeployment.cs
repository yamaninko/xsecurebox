using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace SecureBox.Infrastructure.Ethereum;

public class SecureBoxRegistryDeployment : ContractDeploymentMessage
{
    public static string BYTECODE = EthereumArtifacts.Bytecode;

    public SecureBoxRegistryDeployment() : base(BYTECODE)
    {
    }

    [Parameter("bytes32", "_systemId", 1)]
    public byte[] SystemId { get; set; } = Array.Empty<byte>();
}
