
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ------------------------
// CORS POLICY
// ------------------------
// CORS is only required when a browser (JS frontend) calls this API from a different origin.
// For server-to-server API calls (like your Flow API calling this Areas API), CORS is NOT needed.
// Here we define a safe, flexible policy for testing or future frontend usage.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()    // In production, replace with specific origins like "https://your-flow-api.com"
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll"); // Apply CORS globally

// ------------------------
// Sample Data
// ------------------------
var areas = new[]
{
    new { id = "lekki", title = "Lekki Phase 1", fee = 1500, active = true },
    new { id = "ikeja", title = "Ikeja GRA", fee = 1000, active = true },
    new { id = "yaba", title = "Yaba", fee = 800, active = false }
};

// ------------------------
// Endpoints
// ------------------------

// Health check endpoint
app.MapGet("/healthz", () => Results.Ok("Healthy"));

// GET /api/areas - returns only active areas
app.MapGet("/api/areas", () =>
{
    var activeAreas = areas
        .Where(a => a.active)
        .Select(a => new { a.id, a.title, a.fee });

    return Results.Ok(activeAreas);
});

app.Run();











// using CjEndpoint.Services;
// using CjEndpoint.Models;
// using System.Text;

// var builder = WebApplication.CreateBuilder(args);

// // Minimal API - keep everything small and focused for the flow endpoint
// builder.Services.AddOpenApi();
// var app = builder.Build();

// if (app.Environment.IsDevelopment()) app.MapOpenApi();

// // Minimal flows endpoint - reads private key from ENV, supports base64-encoded PEM fallback
// app.MapPost("/flows/endpoint", async (FlowEncryptedRequest req) =>
// {
//     try
//     {
//         // Load private key from env: raw PEM or base64-encoded PEM
//         var privateKeyPem = Environment.GetEnvironmentVariable("PRIVATE_KEY_PEM");
//         if (string.IsNullOrWhiteSpace(privateKeyPem))
//         {
//             var b64 = Environment.GetEnvironmentVariable("PRIVATE_KEY_PEM_B64");
//             if (!string.IsNullOrWhiteSpace(b64))
//             {
//                 try { privateKeyPem = Encoding.UTF8.GetString(Convert.FromBase64String(b64)); }
//                 catch { return Results.BadRequest(new { error = "PRIVATE_KEY_PEM_B64 invalid base64" }); }
//             }
//         }

//         if (string.IsNullOrWhiteSpace(privateKeyPem))
//             return Results.BadRequest(new { error = "PRIVATE_KEY_PEM environment variable not set" });

//         using var rsa = FlowEncryption.LoadRsaFromPem(privateKeyPem);

//         // 1) decrypt incoming flow request
//         var decryptedJson = FlowEncryption.DecryptFlowRequest(req, rsa, out var aesKey, out var iv);

//         using var doc = System.Text.Json.JsonDocument.Parse(decryptedJson);
//         var action = doc.RootElement.GetProperty("action").GetString();

//         // 2) simple ping handler for quick health
//         if (action == "ping")
//         {
//             var responseObj = new { version = "3.0", data = new { status = "active" } };
//             var encryptedResponse = FlowEncryption.EncryptFlowResponse(responseObj, aesKey, iv);
//             return Results.Text(encryptedResponse, "application/json");
//         }

//         // Default/fallback behavior: return active to satisfy checks
//         var fallback = FlowEncryption.EncryptFlowResponse(new { version = "3.0", data = new { status = "active" } }, aesKey, iv);
//         return Results.Text(fallback, "application/json");
//     }
//     catch (Exception ex)
//     {
//         Console.Error.WriteLine($"Error in /flows/endpoint: {ex}");
//         return Results.StatusCode(500);
//     }
// })
// .WithName("FlowEndpoint");

// // lightweight health
// app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// app.Run();
