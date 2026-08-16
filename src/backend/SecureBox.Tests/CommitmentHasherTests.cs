using SecureBox.Infrastructure.Ethereum;

namespace SecureBox.Tests;

public class CommitmentHasherTests
{
    [Fact]
    public void PayloadHash_IsDeterministicAndChangesWithCiphertext()
    {
        var cipher = new byte[] { 1, 2, 3, 4 };
        var iv = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 };
        var tag = new byte[] { 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7 };

        var a = CommitmentHasher.PayloadHash(cipher, iv, tag);
        var b = CommitmentHasher.PayloadHash(cipher, iv, tag);
        a.Should().Equal(b);
        a.Should().HaveCount(32);

        cipher[0] = 99;
        var c = CommitmentHasher.PayloadHash(cipher, iv, tag);
        c.Should().NotEqual(a);
    }

    [Fact]
    public void SystemId_UsesInstallationName()
    {
        var one = CommitmentHasher.SystemId("plant-a");
        var two = CommitmentHasher.SystemId("plant-b");
        one.Should().NotEqual(two);
        CommitmentHasher.ToHex(one).Should().StartWith("0x");
    }
}
