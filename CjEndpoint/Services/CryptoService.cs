using System.Security.Cryptography;
using System.Text;

namespace CjEndpoint.Services;

/// <summary>
/// Production-ready cryptographic service for RSA and AES operations.
/// Supports WhatsApp encrypted webhook payloads with message integrity.
/// </summary>
public class CryptoService
{
    private readonly RSA _privateRsa;
    private readonly RSA _publicRsa;
    private readonly ILogger<CryptoService> _logger;

    public CryptoService(ILogger<CryptoService> logger)
    {
        _logger = logger;
        _privateRsa = RSA.Create();
        _publicRsa = RSA.Create();

        // Load keys from PEM files
        LoadKeys();
    }

    private void LoadKeys()
    {
        try
        {
            // Try several likely locations for the certs directory so running from bin/ works
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "certs"),
                Path.Combine(Directory.GetCurrentDirectory(), "certs"),
                // when running from source tree the bin folder is typically three levels down
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "certs"))
            };

            string? foundDir = candidates.FirstOrDefault(d => Directory.Exists(d));
            if (foundDir == null)
                throw new FileNotFoundException($"Could not find a 'certs' directory. Searched: {string.Join(';', candidates)}");

            var privateKeyPath = Path.Combine(foundDir, "private_key.pem");
            var publicKeyPath = Path.Combine(foundDir, "public_key.pem");

            if (!File.Exists(privateKeyPath) || !File.Exists(publicKeyPath))
                throw new FileNotFoundException($"Key files not found. Expected: {privateKeyPath}, {publicKeyPath}");

            var privateKeyPem = File.ReadAllText(privateKeyPath);
            var publicKeyPem = File.ReadAllText(publicKeyPath);

            _privateRsa.ImportFromPem(privateKeyPem.ToCharArray());
            _publicRsa.ImportFromPem(publicKeyPem.ToCharArray());

            _logger.LogInformation("RSA keys loaded successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load RSA keys");
            throw;
        }
    }

    /// <summary>
    /// Decrypts an AES key that was encrypted with our public RSA key.
    /// WhatsApp sends the AES key encrypted; we decrypt it with our private key.
    /// </summary>
    public byte[] DecryptAesKey(byte[] encryptedAesKey)
    {
        try
        {
            var decryptedKey = _privateRsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
            _logger.LogInformation("AES key decrypted successfully.");
            return decryptedKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt AES key");
            throw;
        }
    }

    /// <summary>
    /// Encrypts data with AES-256-CBC for outbound responses.
    /// </summary>
    public (byte[] ciphertext, byte[] iv) EncryptWithAes(byte[] plaintext, byte[] aesKey)
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            cs.Write(plaintext, 0, plaintext.Length);
            cs.FlushFinalBlock();

            _logger.LogInformation("Data encrypted with AES-256-CBC.");
            return (ms.ToArray(), aes.IV);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt with AES");
            throw;
        }
    }

    /// <summary>
    /// Decrypts AES-256-CBC encrypted payload.
    /// </summary>
    public byte[] DecryptWithAes(byte[] ciphertext, byte[] aesKey, byte[] iv)
    {
        try
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = aesKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(ciphertext);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var result = new MemoryStream();
            cs.CopyTo(result);

            _logger.LogInformation("Data decrypted with AES-256-CBC.");
            return result.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt with AES");
            throw;
        }
    }

    /// <summary>
    /// Signs data with RSA-SHA256 (PKCS#1 v1.5 padding).
    /// Used to sign webhook responses for integrity.
    /// </summary>
    public byte[] SignData(byte[] data)
    {
        try
        {
            var signature = _privateRsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _logger.LogInformation("Data signed with RSA-SHA256.");
            return signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign data");
            throw;
        }
    }

    /// <summary>
    /// Verifies a signature using our public RSA key.
    /// </summary>
    public bool VerifySignature(byte[] data, byte[] signature)
    {
        try
        {
            var isValid = _publicRsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            _logger.LogInformation($"Signature verification: {(isValid ? "valid" : "invalid")}");
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify signature");
            return false;
        }
    }

    /// <summary>
    /// Encrypts data with the public RSA key (for external parties to send us secrets).
    /// </summary>
    public byte[] EncryptWithRsa(byte[] plaintext)
    {
        try
        {
            var encrypted = _publicRsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);
            _logger.LogInformation("Data encrypted with RSA public key.");
            return encrypted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt with RSA");
            throw;
        }
    }

    /// <summary>
    /// Returns the public key in PEM format (for sharing with third parties).
    /// </summary>
    public string GetPublicKeyPem()
    {
        using var publicKey = RSA.Create();
        var publicKeyPath = Path.Combine(AppContext.BaseDirectory, "certs", "public_key.pem");
        return File.ReadAllText(publicKeyPath);
    }
}
