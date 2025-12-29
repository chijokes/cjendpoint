using CjEndpoint.Tests;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Find public key
var publicKeyPath = Path.Combine(Directory.GetCurrentDirectory(), "certs", "public_key.pem");

// Check if running from bin/Debug or source root
if (!File.Exists(publicKeyPath))
    publicKeyPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "certs", "public_key.pem");

if (!File.Exists(publicKeyPath))
{
    Console.Error.WriteLine("❌ Could not find certs/public_key.pem");
    return;
}

// Generate payload
var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "flow.json");
FlowPayloadGenerator.GenerateTestPayload(outputPath, publicKeyPath);
