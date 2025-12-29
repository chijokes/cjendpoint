using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CjEndpoint.Services;

/// <summary>
/// Helper for flow encryption/decryption using RSA (OAEP-SHA256) + AES-GCM.
/// Uses the convention: ciphertext bytes = encryptedData || authTag (tag appended to end, 16 bytes).
/// </summary>
public static class FlowEncryption
{
    private const int TagLength = 16; // bytes

    /// <summary>
    /// Load an RSA instance from an unencrypted PEM string (PKCS#1 or PKCS#8).
    /// Throws if the PEM is not valid or encrypted.
    /// </summary>
    public static RSA LoadRsaFromPem(string privateKeyPem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.ToCharArray());
        return rsa;
    }

    /// <summary>
    /// Decrypts the flow request: RSA-decrypt AES key, then AES-GCM decrypt payload.
    /// Returns the decrypted JSON text, AES key and IV bytes for later responses.
    /// </summary>
    public static string DecryptFlowRequest(FlowEncryptedRequest req, RSA rsa, out byte[] aesKey, out byte[] iv)
    {
        // 1) RSA-decrypt AES key (OAEP SHA-256)
        var encryptedAesKey = Convert.FromBase64String(req.encrypted_aes_key);
        aesKey = rsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);

        // 2) Parse IV and ciphertext
        iv = Convert.FromBase64String(req.initial_vector);
        var combined = Convert.FromBase64String(req.encrypted_flow_data);
        if (combined.Length < TagLength) throw new CryptographicException("Malformed encrypted payload");

        int cipherLen = combined.Length - TagLength;
        var ciphertext = new byte[cipherLen];
        var tag = new byte[TagLength];
        Array.Copy(combined, 0, ciphertext, 0, cipherLen);
        Array.Copy(combined, cipherLen, tag, 0, TagLength);

        // 3) AES-GCM decrypt
        var plaintext = new byte[cipherLen];
        using (var aesgcm = new AesGcm(aesKey))
        {
            aesgcm.Decrypt(iv, ciphertext, tag, plaintext, null);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Encrypts a response object using AES-GCM with the provided AES key and IV (IV is flipped before use).
    /// Returns base64 of (ciphertext || tag).
    /// </summary>
    public static string EncryptFlowResponse(object responseObj, byte[] aesKey, byte[] iv)
    {
        // Flip IV bytes as an agreed convention when sending responses
        var flippedIv = iv.Select(b => (byte)~b).ToArray();

        var plain = JsonSerializer.SerializeToUtf8Bytes(responseObj);
        var ciphertext = new byte[plain.Length];
        var tag = new byte[TagLength];

        using (var aesgcm = new AesGcm(aesKey))
        {
            aesgcm.Encrypt(flippedIv, plain, ciphertext, tag, null);
        }

        // Append tag to the ciphertext (ciphertext || tag)
        var outBuf = new byte[ciphertext.Length + tag.Length];
        Array.Copy(ciphertext, 0, outBuf, 0, ciphertext.Length);
        Array.Copy(tag, 0, outBuf, ciphertext.Length, tag.Length);

        return Convert.ToBase64String(outBuf);
    }
}
