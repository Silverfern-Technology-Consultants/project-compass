namespace Compass.Core.Models;

public class MfaStatusResponse
{
    public bool IsEnabled { get; set; }
    public DateTime? SetupDate { get; set; }
    public DateTime? LastUsedDate { get; set; }
    public int BackupCodesRemaining { get; set; }
    public bool RequiresSetup { get; set; }
}

public class MfaSetupResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty; // Base64 encoded QR code image
    public string ManualEntryKey { get; set; } = string.Empty; // Formatted secret for manual entry
    public List<string> BackupCodes { get; set; } = new();
}

public class MfaVerifySetupRequest
{
    public string TotpCode { get; set; } = string.Empty;
}

public class MfaVerifyRequest
{
    public string Token { get; set; } = string.Empty;
    public bool IsBackupCode { get; set; } = false;
}

public class MfaVerifyResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? RemainingBackupCodes { get; set; }
}

public class MfaDisableRequest
{
    public string Password { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class MfaRegenerateBackupCodesRequest
{
    public string Password { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}