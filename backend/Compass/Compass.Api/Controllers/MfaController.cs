using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Compass.Core.Services;
using Compass.Core.Models;
using Compass.Data.Repositories;
using System.Text.Json;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<MfaController> _logger;

    public MfaController(
        IMfaService mfaService,
        ICustomerRepository customerRepository,
        ICurrentUserService currentUserService,
        ILogger<MfaController> logger)
    {
        _mfaService = mfaService;
        _customerRepository = customerRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<MfaStatusResponse>> GetMfaStatus()
    {
        try
        {
            var currentCustomerId = _currentUserService.GetCurrentCustomerId();
            if (currentCustomerId == null)
                return Unauthorized("Customer ID not found");

            var customer = await _customerRepository.GetByIdAsync(currentCustomerId.Value);

            if (customer == null)
                return NotFound("Customer not found");

            var backupCodesCount = 0;
            if (!string.IsNullOrEmpty(customer.MfaBackupCodes))
            {
                var codes = JsonSerializer.Deserialize<List<string>>(customer.MfaBackupCodes) ?? new List<string>();
                backupCodesCount = codes.Count;
            }

            return Ok(new MfaStatusResponse
            {
                IsEnabled = customer.IsMfaEnabled,
                SetupDate = customer.MfaSetupDate,
                LastUsedDate = customer.LastMfaUsedDate,
                BackupCodesRemaining = backupCodesCount,
                RequiresSetup = customer.RequireMfaSetup
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MFA status");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("setup")]
    [Authorize]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa()
    {
        try
        {
            var currentCustomerId = _currentUserService.GetCurrentCustomerId();
            if (currentCustomerId == null)
                return Unauthorized("Customer ID not found");

            var customer = await _customerRepository.GetByIdAsync(currentCustomerId.Value);

            if (customer == null)
                return NotFound("Customer not found");

            if (customer.IsMfaEnabled)
                return BadRequest("MFA is already enabled for this account");

            // Generate secret and backup codes
            var secret = _mfaService.GenerateSecret();
            var backupCodes = _mfaService.GenerateBackupCodes();
            var qrCodeUri = _mfaService.GenerateQrCodeUri(customer.Email, secret);

            // Generate QR code image
            var qrCodeImage = _mfaService.GenerateQrCodeImage(qrCodeUri);
            var qrCodeBase64 = Convert.ToBase64String(qrCodeImage);

            // Format secret for manual entry (groups of 4 characters)
            var manualEntryKey = string.Join(" ",
                Enumerable.Range(0, secret.Length / 4)
                .Select(i => secret.Substring(i * 4, 4)));

            // Store secret and backup codes (but don't enable MFA yet)
            customer.MfaSecret = secret;
            customer.MfaBackupCodes = JsonSerializer.Serialize(backupCodes);

            await _customerRepository.UpdateAsync(customer);

            return Ok(new MfaSetupResponse
            {
                Secret = secret,
                QrCodeUri = qrCodeUri,
                QrCode = $"data:image/png;base64,{qrCodeBase64}",
                ManualEntryKey = manualEntryKey,
                BackupCodes = backupCodes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up MFA");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("verify-setup")]
    [Authorize]
    public async Task<ActionResult<MfaVerifyResponse>> VerifySetup([FromBody] MfaVerifySetupRequest request)
    {
        try
        {
            var currentCustomerId = _currentUserService.GetCurrentCustomerId();
            if (currentCustomerId == null)
                return Unauthorized("Customer ID not found");

            var customer = await _customerRepository.GetByIdAsync(currentCustomerId.Value);

            if (customer == null)
                return NotFound("Customer not found");

            if (customer.IsMfaEnabled)
                return BadRequest("MFA is already enabled for this account");

            if (string.IsNullOrEmpty(customer.MfaSecret))
                return BadRequest("MFA setup has not been initiated. Please call setup first.");

            // Validate the TOTP code
            var isValid = _mfaService.ValidateTotp(customer.MfaSecret, request.TotpCode);

            if (!isValid)
            {
                return Ok(new MfaVerifyResponse
                {
                    IsValid = false,
                    Message = "Invalid verification code"
                });
            }

            // Enable MFA
            customer.IsMfaEnabled = true;
            customer.MfaSetupDate = DateTime.UtcNow;
            customer.RequireMfaSetup = false;
            customer.LastMfaUsedDate = DateTime.UtcNow;

            await _customerRepository.UpdateAsync(customer);

            return Ok(new MfaVerifyResponse
            {
                IsValid = true,
                Message = "MFA has been successfully enabled"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying MFA setup");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("verify")]
    [Authorize]
    public async Task<ActionResult<MfaVerifyResponse>> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        try
        {
            var currentCustomerId = _currentUserService.GetCurrentCustomerId();
            if (currentCustomerId == null)
                return Unauthorized("Customer ID not found");

            var customer = await _customerRepository.GetByIdAsync(currentCustomerId.Value);

            if (customer == null)
                return NotFound("Customer not found");

            if (!customer.IsMfaEnabled || string.IsNullOrEmpty(customer.MfaSecret))
                return BadRequest("MFA is not enabled for this account");

            bool isValid = false;
            List<string>? remainingBackupCodes = null;

            if (request.IsBackupCode)
            {
                // Validate backup code
                if (!string.IsNullOrEmpty(customer.MfaBackupCodes))
                {
                    var backupCodes = JsonSerializer.Deserialize<List<string>>(customer.MfaBackupCodes) ?? new List<string>();
                    isValid = _mfaService.ValidateBackupCode(request.Token, backupCodes);

                    if (isValid)
                    {
                        // Remove used backup code
                        backupCodes.Remove(request.Token.Trim().ToLower());
                        customer.MfaBackupCodes = JsonSerializer.Serialize(backupCodes);
                        remainingBackupCodes = backupCodes;
                        customer.LastMfaUsedDate = DateTime.UtcNow;
                        await _customerRepository.UpdateAsync(customer);
                    }
                }
            }
            else
            {
                // Validate TOTP code
                isValid = _mfaService.ValidateTotp(customer.MfaSecret, request.Token);
                if (isValid)
                {
                    customer.LastMfaUsedDate = DateTime.UtcNow;
                    await _customerRepository.UpdateAsync(customer);
                }
            }

            return Ok(new MfaVerifyResponse
            {
                IsValid = isValid,
                Message = isValid ? "Verification successful" : "Invalid code",
                RemainingBackupCodes = remainingBackupCodes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying MFA code");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("disable")]
    [Authorize]
    public async Task<ActionResult> DisableMfa([FromBody] MfaDisableRequest request)
    {
        try
        {
            var currentCustomerId = _currentUserService.GetCurrentCustomerId();
            if (currentCustomerId == null)
                return Unauthorized("Customer ID not found");

            var customer = await _customerRepository.GetByIdAsync(currentCustomerId.Value);

            if (customer == null)
                return NotFound("Customer not found");

            if (!customer.IsMfaEnabled)
                return BadRequest("MFA is not enabled for this account");

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, customer.PasswordHash))
                return BadRequest("Invalid password");

            // Verify MFA token
            bool mfaValid = false;
            if (!string.IsNullOrEmpty(customer.MfaBackupCodes))
            {
                var backupCodes = JsonSerializer.Deserialize<List<string>>(customer.MfaBackupCodes) ?? new List<string>();
                mfaValid = _mfaService.ValidateBackupCode(request.Token, backupCodes) ||
                          _mfaService.ValidateTotp(customer.MfaSecret ?? "", request.Token);
            }
            else
            {
                mfaValid = _mfaService.ValidateTotp(customer.MfaSecret ?? "", request.Token);
            }

            if (!mfaValid)
                return BadRequest("Invalid MFA code");

            // Disable MFA
            customer.IsMfaEnabled = false;
            customer.MfaSecret = null;
            customer.MfaBackupCodes = null;
            customer.MfaSetupDate = null;
            customer.LastMfaUsedDate = null;
            customer.RequireMfaSetup = false;

            await _customerRepository.UpdateAsync(customer);

            return Ok(new { message = "MFA has been disabled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling MFA");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("regenerate-backup-codes")]
    [Authorize]
    public async Task<ActionResult<List<string>>> RegenerateBackupCodes([FromBody] MfaVerifyRequest request)
    {
        try
        {
            var currentCustomerId = _currentUserService.GetCurrentCustomerId();
            if (currentCustomerId == null)
                return Unauthorized("Customer ID not found");

            var customer = await _customerRepository.GetByIdAsync(currentCustomerId.Value);

            if (customer == null)
                return NotFound("Customer not found");

            if (!customer.IsMfaEnabled)
                return BadRequest("MFA is not enabled for this account");

            // Verify MFA token before regenerating
            var isValid = _mfaService.ValidateTotp(customer.MfaSecret ?? "", request.Token);
            if (!isValid)
                return BadRequest("Invalid MFA code");

            var newBackupCodes = _mfaService.GenerateBackupCodes();
            customer.MfaBackupCodes = JsonSerializer.Serialize(newBackupCodes);

            await _customerRepository.UpdateAsync(customer);

            return Ok(new { backupCodes = newBackupCodes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating backup codes");
            return StatusCode(500, "Internal server error");
        }
    }

    // Keep the test endpoint for debugging
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok(new
        {
            message = "MFA controller works!",
            timestamp = DateTime.UtcNow
        });
    }
}