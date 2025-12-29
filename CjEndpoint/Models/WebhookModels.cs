namespace CjEndpoint.Models;

/// <summary>
/// Represents an encrypted WhatsApp webhook request.
/// The payload contains: encrypted AES key (RSA-encrypted), IV, and ciphertext.
/// </summary>
public class EncryptedWebhookRequest
{
    /// <summary>
    /// The AES encryption key, encrypted with our public RSA key (base64 encoded).
    /// </summary>
    public string EncryptedAesKey { get; set; } = string.Empty;

    /// <summary>
    /// Initialization vector for AES decryption (base64 encoded).
    /// </summary>
    public string Iv { get; set; } = string.Empty;

    /// <summary>
    /// The encrypted payload (base64 encoded).
    /// </summary>
    public string Ciphertext { get; set; } = string.Empty;

    /// <summary>
    /// HMAC signature for integrity verification (base64 encoded, optional).
    /// </summary>
    public string? Signature { get; set; }
}

/// <summary>
/// Represents the decrypted payload (usually JSON).
/// </summary>
public class DecryptedPayload
{
    public string Content { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response to send back to WhatsApp (encrypted).
/// </summary>
public class EncryptedWebhookResponse
{
    /// <summary>
    /// The encrypted response payload (base64 encoded).
    /// </summary>
    public string Ciphertext { get; set; } = string.Empty;

    /// <summary>
    /// Initialization vector (base64 encoded).
    /// </summary>
    public string Iv { get; set; } = string.Empty;

    /// <summary>
    /// The AES key encrypted with WhatsApp's public key, if applicable (base64 encoded).
    /// </summary>
    public string? EncryptedAesKey { get; set; }

    /// <summary>
    /// Signature of the response for integrity (base64 encoded).
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Timestamp when the response was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
