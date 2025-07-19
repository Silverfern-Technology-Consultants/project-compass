using System.Drawing;

namespace Compass.core.Interfaces;

public interface IMfaService
{
    string GenerateSecret();
    string GenerateQrCodeUri(string email, string secret, string issuer = "Compass");
    bool ValidateTotp(string secret, string token);
    List<string> GenerateBackupCodes(int count = 10);
    bool ValidateBackupCode(string providedCode, List<string> validCodes);
    byte[] GenerateQrCodeImage(string qrCodeUri);
}