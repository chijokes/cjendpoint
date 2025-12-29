using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CjEndpoint.Tests;

/// <summary>
/// Helper to generate valid encrypted flow payloads for testing.
/// </summary>
public class FlowPayloadGenerator
{
    private const int TagLength = 16;

    public static void GenerateTestPayload(string outputPath, string publicKeyPath)
    {
        try
        {
            Console.WriteLine("🔐 WhatsApp Flow Test Payload Generator\n");

            // 1. Load public key
            if (!File.Exists(publicKeyPath))
                throw new FileNotFoundException($"Public key not found at {publicKeyPath}");

            var publicKeyPem = File.ReadAllText(publicKeyPath);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem.ToCharArray());
            Console.WriteLine($"✅ Loaded public key\n");

            // 2. Create plaintext request
            var plainRequest = new
            {
                action = "ping",
                timestamp = DateTime.UtcNow.ToString("O"),
                data = new { client_version = "3.0" }
            };
            var plainJson = JsonSerializer.Serialize(plainRequest);
            var plainBytes = Encoding.UTF8.GetBytes(plainJson);
            Console.WriteLine($"📝 Plain request:\n{plainJson}\n");

            // 3. Generate AES key and IV
            using var aesKeyGen = Aes.Create();
            aesKeyGen.KeySize = 256;
            aesKeyGen.GenerateKey();
            byte[] aesKey = aesKeyGen.Key;

            byte[] iv = new byte[12];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(iv);
            Console.WriteLine("🔑 Generated AES-256 key and IV\n");

            // 4. Encrypt with AES-GCM
            byte[] ciphertext = new byte[plainBytes.Length];
            byte[] tag = new byte[TagLength];
            using (var aesgcm = new AesGcm(aesKey, 128))
                aesgcm.Encrypt(iv, plainBytes, ciphertext, tag, null);

            // 5. Combine and base64
            byte[] combined = new byte[ciphertext.Length + tag.Length];
            Array.Copy(ciphertext, combined, ciphertext.Length);
            Array.Copy(tag, 0, combined, ciphertext.Length, tag.Length);
            string encryptedFlowDataB64 = Convert.ToBase64String(combined);

            // 6. Encrypt AES key with RSA
            byte[] encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
            string encryptedAesKeyB64 = Convert.ToBase64String(encryptedAesKey);
            string ivB64 = Convert.ToBase64String(iv);

            // 7. Create and save payload
            var payload = new
            {
                encrypted_aes_key = encryptedAesKeyB64,
                encrypted_flow_data = encryptedFlowDataB64,
                initial_vector = ivB64
            };

            string outputJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, outputJson);

            Console.WriteLine($"✅ Payload saved to: {outputPath}\n");
            Console.WriteLine("📋 JSON:\n" + outputJson);
            Console.WriteLine("\n🚀 Test with curl:");
            Console.WriteLine($"curl -X POST http://localhost:5000/flows/endpoint -H \"Content-Type: application/json\" -d @{Path.GetFileName(outputPath)}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}
