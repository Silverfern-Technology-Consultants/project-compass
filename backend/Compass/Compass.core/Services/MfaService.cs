using System.Security.Cryptography;
using System.Text;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace Compass.Core.Services;

public class MfaService : IMfaService
{
    private const int TotpWindow = 3; // ✅ INCREASED: Allow 3 steps (90 seconds) tolerance for debugging
    private const int TotpDigits = 6;
    private const int TotpPeriod = 30;

    public string GenerateSecret()
    {
        // Generate a 20-byte (160-bit) secret for TOTP
        var bytes = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Base32Encode(bytes);
    }

    public string GenerateQrCodeUri(string email, string secret, string issuer = "Compass")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secret}&issuer={encodedIssuer}&digits={TotpDigits}&period={TotpPeriod}";
    }

    public bool ValidateTotp(string secret, string token)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(token))
            return false;

        if (token.Length != TotpDigits || !token.All(char.IsDigit))
            return false;

        var secretBytes = Base32Decode(secret);
        var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timeStep = unixTime / TotpPeriod;

        // ✅ DEBUG: Log timing information
        Console.WriteLine($"[MFA DEBUG] Current Unix Time: {unixTime}");
        Console.WriteLine($"[MFA DEBUG] Current Time Step: {timeStep}");
        Console.WriteLine($"[MFA DEBUG] Token to validate: {token}");

        // Check current time step and adjacent ones for tolerance
        for (int i = -TotpWindow; i <= TotpWindow; i++)
        {
            var stepToCheck = timeStep + i;
            var expectedToken = GenerateTotpCode(secretBytes, stepToCheck);

            // ✅ DEBUG: Log each step being checked
            Console.WriteLine($"[MFA DEBUG] Checking step {stepToCheck} (offset {i}): Expected={expectedToken}, Provided={token}");

            if (expectedToken == token)
            {
                Console.WriteLine($"[MFA DEBUG] ✅ MATCH FOUND at step {stepToCheck} (offset {i})");
                return true;
            }
        }

        Console.WriteLine($"[MFA DEBUG] ❌ NO MATCH FOUND for token {token}");
        return false;
    }

    public List<string> GenerateBackupCodes(int count = 10)
    {
        var codes = new List<string>();
        using (var rng = RandomNumberGenerator.Create())
        {
            for (int i = 0; i < count; i++)
            {
                var bytes = new byte[4]; // 8-character code
                rng.GetBytes(bytes);
                var code = BitConverter.ToString(bytes).Replace("-", "").ToLower();
                codes.Add($"{code.Substring(0, 4)}-{code.Substring(4, 4)}");
            }
        }
        return codes;
    }

    public bool ValidateBackupCode(string providedCode, List<string> validCodes)
    {
        if (string.IsNullOrEmpty(providedCode) || validCodes == null)
            return false;

        var normalizedCode = providedCode.Trim().ToLower();
        return validCodes.Contains(normalizedCode);
    }

    public byte[] GenerateQrCodeImage(string qrCodeUri)
    {
        using (var qrGenerator = new QRCodeGenerator())
        {
            var qrCodeData = qrGenerator.CreateQrCode(qrCodeUri, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        }
    }

    private string GenerateTotpCode(byte[] secret, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using (var hmac = new HMACSHA1(secret))
        {
            var hash = hmac.ComputeHash(timeBytes);
            var offset = hash[hash.Length - 1] & 0x0F;

            var binaryCode = (hash[offset] & 0x7F) << 24
                           | (hash[offset + 1] & 0xFF) << 16
                           | (hash[offset + 2] & 0xFF) << 8
                           | (hash[offset + 3] & 0xFF);

            var code = binaryCode % (int)Math.Pow(10, TotpDigits);
            return code.ToString($"D{TotpDigits}");
        }
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        var buffer = 0;
        var bufferLength = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bufferLength += 8;

            while (bufferLength >= 5)
            {
                var index = (buffer >> (bufferLength - 5)) & 0x1F;
                result.Append(alphabet[index]);
                bufferLength -= 5;
            }
        }

        if (bufferLength > 0)
        {
            var index = (buffer << (5 - bufferLength)) & 0x1F;
            result.Append(alphabet[index]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new List<byte>();
        var buffer = 0;
        var bufferLength = 0;

        foreach (var c in data.ToUpper())
        {
            if (c == '=') break;

            var index = alphabet.IndexOf(c);
            if (index < 0) continue;

            buffer = (buffer << 5) | index;
            bufferLength += 5;

            if (bufferLength >= 8)
            {
                result.Add((byte)(buffer >> (bufferLength - 8)));
                bufferLength -= 8;
            }
        }

        return result.ToArray();
    }
}