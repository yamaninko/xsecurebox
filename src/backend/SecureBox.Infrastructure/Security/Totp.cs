using System.Security.Cryptography;
using System.Text;

namespace SecureBox.Infrastructure.Security;

public static class Totp
{
    public static string GenerateSecret()
    {
        return Base32Encode(RandomNumberGenerator.GetBytes(20));
    }

    public static string GenerateCode(string secret)
    {
        var key = Base32Decode(secret);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        return Compute(key, timestep);
    }

    public static bool Verify(string secret, string code, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length != 6)
        {
            return false;
        }

        var key = Base32Decode(secret);
        var timestep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        for (var offset = -window; offset <= window; offset++)
        {
            if (Compute(key, timestep + offset) == digits)
            {
                return true;
            }
        }

        return false;
    }

    public static string OtpAuthUri(string issuer, string account, string secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";

    private static string Compute(byte[] key, long timestep)
    {
        var data = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(data);
        }

        var hash = HMACSHA1.HashData(key, data);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            output.Append(alphabet[(buffer << (5 - bits)) & 31]);
        }

        return output.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var clean = new string(input.Trim().ToUpperInvariant().Where(c => c != '=').ToArray());
        var buffer = 0;
        var bits = 0;
        var bytes = new List<byte>();
        foreach (var c in clean)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException("Invalid TOTP secret");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
