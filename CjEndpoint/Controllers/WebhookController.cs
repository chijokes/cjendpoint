using Microsoft.AspNetCore.Mvc;
using CjEndpoint.Models;
using CjEndpoint.Services;
using System.Text;

namespace CjEndpoint.Controllers;

/// <summary>
/// WhatsApp Cloud API webhook controller with end-to-end encryption support.
/// Handles encrypted inbound messages, decrypts them, processes business logic, and sends encrypted responses.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly CryptoService _cryptoService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(CryptoService cryptoService, ILogger<WebhookController> logger)
    {
        _cryptoService = cryptoService;
        _logger = logger;
    }
    
    /// <summary>
    /// GET: Verification endpoint for WhatsApp webhooks (standard challenge-response).
    /// </summary>
    [HttpGet]
    public IActionResult Verify([FromQuery] string mode, [FromQuery] string token, [FromQuery] string challenge)
    {
        const string verifyToken = "your-webhook-verify-token"; // Change this in production (use env var)

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Webhook verified successfully.");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed: invalid token or mode.");
        return Unauthorized();
    }

    /// <summary>
    /// POST: Receive and decrypt WhatsApp encrypted webhook payloads.
    /// Expects JSON body:
    /// {
    ///   "encrypted_aes_key": "base64(...)",
    ///   "iv": "base64(...)",
    ///   "ciphertext": "base64(...)",
    ///   "signature": "base64(...)" // optional
    /// }
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceiveEncryptedMessage([FromBody] EncryptedWebhookRequest request)
    {
        try
        {
            _logger.LogInformation("Received encrypted webhook payload.");

            // Decode base64 fields
            byte[] encryptedAesKey = Convert.FromBase64String(request.EncryptedAesKey);
            byte[] iv = Convert.FromBase64String(request.Iv);
            byte[] ciphertext = Convert.FromBase64String(request.Ciphertext);

            // Step 1: Decrypt the AES key using our private RSA key
            byte[] aesKey = _cryptoService.DecryptAesKey(encryptedAesKey);
            _logger.LogInformation("AES key decrypted successfully.");

            // Step 2: Decrypt the payload using the AES key
            byte[] decryptedBytes = _cryptoService.DecryptWithAes(ciphertext, aesKey, iv);
            string decryptedPayload = Encoding.UTF8.GetString(decryptedBytes);
            _logger.LogInformation($"Payload decrypted. Content preview: {decryptedPayload.Substring(0, Math.Min(100, decryptedPayload.Length))}...");

            // Step 3 (Optional): Verify signature if provided
            if (!string.IsNullOrEmpty(request.Signature))
            {
                byte[] signature = Convert.FromBase64String(request.Signature);
                bool isValid = _cryptoService.VerifySignature(ciphertext, signature);
                if (!isValid)
                {
                    _logger.LogWarning("Signature verification failed. Payload may be tampered.");
                    return BadRequest(new { error = "Signature verification failed" });
                }
                _logger.LogInformation("Signature verified successfully.");
            }

            // Step 4: Process the decrypted message (business logic)
            var processed = await ProcessMessage(decryptedPayload);

            // Step 5: Prepare encrypted response
            var responsePayload = new
            {
                status = "success",
                message = processed.Message,
                timestamp = DateTime.UtcNow
            };
            string responseJson = System.Text.Json.JsonSerializer.Serialize(responsePayload);
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);

            // Step 6: Encrypt the response with the same AES key
            var (encryptedResponse, responseIv) = _cryptoService.EncryptWithAes(responseBytes, aesKey);

            // Step 7: Sign the encrypted response for integrity
            byte[] responseSignature = _cryptoService.SignData(encryptedResponse);

            var response = new EncryptedWebhookResponse
            {
                Ciphertext = Convert.ToBase64String(encryptedResponse),
                Iv = Convert.ToBase64String(responseIv),
                Signature = Convert.ToBase64String(responseSignature),
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Encrypted response prepared and signed.");
            return Ok(response);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Invalid base64 encoding in request.");
            return BadRequest(new { error = "Invalid base64 encoding" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing encrypted webhook.");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy oo", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Endpoint to retrieve our public key (for third parties).
    /// </summary>
    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
    {
        var publicKey = _cryptoService.GetPublicKeyPem();
        return Ok(new { publicKey, format = "PEM" });
    }

    /// <summary>
    /// Internal method to process decrypted webhook message.
    /// Replace with your business logic (save to DB, send response, etc.).
    /// </summary>
    private async Task<(string Message, bool Success)> ProcessMessage(string payload)
    {
        _logger.LogInformation($"Processing message: {payload}");

        // Simulate async work (e.g., database operation)
        await Task.Delay(10);

        // Example: Echo back a success message
        return ("Message received and processed successfully", true);
    }
}
