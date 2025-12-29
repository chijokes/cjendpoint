using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Standalone test client that generates an encrypted flow.json payload.
/// Encrypts a sample request using RSA (public key) + AES-GCM, outputs as JSON.
/// Run: dotnet run FlowTestClientGenerator.cs
/// </summary>
class FlowTestClientGenerator
{
    const int TagLength = 16;

    static void Main()
    {
        try
        {
            Console.WriteLine("🔐 WhatsApp Flow Test Client - Payload Generator");
            Console.WriteLine("================================================\n");

            // 1. Load public key from certs/public_key.pem
            var publicKeyPath = Path.Combine(Directory.GetCurrentDirectory(), "certs", "public_key.pem");
            if (!File.Exists(publicKeyPath))
            {
                Console.Error.WriteLine($"❌ Public key not found at: {publicKeyPath}");
                Console.Error.WriteLine("Make sure you're running from the CjEndpoint project root.");
                return;
            }

            var publicKeyPem = File.ReadAllText(publicKeyPath);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem.ToCharArray());
            Console.WriteLine($"✅ Loaded public key from: {publicKeyPath}\n");

            // 2. Create sample plaintext JSON request
            var plainRequest = new
            {
                action = "ping",
                timestamp = DateTime.UtcNow.ToString("O"),
                data = new { client_version = "3.0" }
            };
            var plainJson = JsonSerializer.Serialize(plainRequest);
            var plainBytes = Encoding.UTF8.GetBytes(plainJson);
            Console.WriteLine($"📝 Plain request:\n{plainJson}\n");

            // 3. Generate random AES key (256-bit) and IV (96-bit for GCM)
            using var aesKeyGen = Aes.Create();
            aesKeyGen.KeySize = 256;
            aesKeyGen.GenerateKey();
            byte[] aesKey = aesKeyGen.Key;

            byte[] iv = new byte[12]; // GCM standard: 96 bits (12 bytes)
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }
            Console.WriteLine($"🔑 Generated AES-256 key and 96-bit IV");

            // 4. Encrypt plaintext with AES-GCM
            byte[] ciphertext = new byte[plainBytes.Length];
            byte[] tag = new byte[TagLength];
            using (var aesgcm = new AesGcm(aesKey, 128)) // 128-bit tag
            {
                aesgcm.Encrypt(iv, plainBytes, ciphertext, tag, null);
            }
            Console.WriteLine($"🔐 Encrypted with AES-GCM (ciphertext: {ciphertext.Length} bytes, tag: {tag.Length} bytes)");

            // 5. Combine ciphertext and tag, then base64
            byte[] combined = new byte[ciphertext.Length + tag.Length];
            Array.Copy(ciphertext, 0, combined, 0, ciphertext.Length);
            Array.Copy(tag, 0, combined, ciphertext.Length, tag.Length);
            string encryptedFlowDataB64 = Convert.ToBase64String(combined);

            // 6. Encrypt AES key with RSA (OAEP-SHA256)
            byte[] encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
            string encryptedAesKeyB64 = Convert.ToBase64String(encryptedAesKey);
            Console.WriteLine($"🔒 Encrypted AES key with RSA-OAEP-SHA256 ({encryptedAesKey.Length} bytes)");

            // 7. Base64 the IV
            string ivB64 = Convert.ToBase64String(iv);

            // 8. Create final payload
            var payload = new
            {
                encrypted_aes_key = encryptedAesKeyB64,
                encrypted_flow_data = encryptedFlowDataB64,
                initial_vector = ivB64
            };

            string outputJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

            // 9. Save to flow.json
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "flow.json");
            File.WriteAllText(outputPath, outputJson);
            Console.WriteLine($"\n✅ Payload saved to: {outputPath}\n");
            Console.WriteLine("📋 Payload preview:");
            Console.WriteLine(outputJson);

            Console.WriteLine("\n🚀 To test the endpoint, run:");
            Console.WriteLine($"curl -X POST http://localhost:5000/flows/endpoint \\");
            Console.WriteLine($"  -H \"Content-Type: application/json\" \\");
            Console.WriteLine($"  -d @flow.json\n");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }
}
