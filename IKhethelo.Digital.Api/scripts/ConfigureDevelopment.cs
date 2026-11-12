// Run from the backend directory: dotnet run --file scripts/ConfigureDevelopment.cs
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using var rsa = RSA.Create(2048);
var start = new ProcessStartInfo("dotnet")
{
    UseShellExecute = false,
    RedirectStandardInput = true
};
foreach (var argument in new[] { "user-secrets", "set", "--id", "ikhethelo-digital-api-development" })
    start.ArgumentList.Add(argument);
using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet user-secrets.");
process.StandardInput.Write(JsonSerializer.Serialize(new Dictionary<string, string>
{
    ["Jwt:Issuer"] = "https://localhost:57972",
    ["Jwt:Audience"] = "ikhethelo-digital-development",
    ["Jwt:PrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem()
}, DevelopmentSecretsJson.Default.DictionaryStringString));
process.StandardInput.Close();
process.WaitForExit();
if (process.ExitCode != 0) return process.ExitCode;
Console.WriteLine("Development JWT settings saved. Restart the API. Running this command again rotates the development key and invalidates existing tokens.");
return 0;

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class DevelopmentSecretsJson : JsonSerializerContext;
