using System.Text;
using Nethereum.Util;

namespace SecureBox.Infrastructure.Ethereum;

public static class CommitmentHasher
{
    public const string AlgorithmName = "AES256-GCM+RSA-OAEP-SHA256";

    public static byte[] PayloadHash(byte[] ciphertext, byte[] iv, byte[] tag)
    {
        var packed = new byte[ciphertext.Length + iv.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, packed, 0, ciphertext.Length);
        Buffer.BlockCopy(iv, 0, packed, ciphertext.Length, iv.Length);
        Buffer.BlockCopy(tag, 0, packed, ciphertext.Length + iv.Length, tag.Length);
        return Sha3Keccack.Current.CalculateHash(packed);
    }

    public static byte[] AlgorithmId() =>
        Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(AlgorithmName));

    public static byte[] KeyId32(Guid keyId)
    {
        var id = new byte[32];
        keyId.ToByteArray().CopyTo(id, 0);
        return id;
    }

    public static byte[] SystemId(string name)
    {
        var raw = "XSecureBox:" + (string.IsNullOrWhiteSpace(name) ? "default" : name.Trim());
        return Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(raw));
    }

    public static string ToHex(byte[] value) => "0x" + Convert.ToHexString(value).ToLowerInvariant();
}
