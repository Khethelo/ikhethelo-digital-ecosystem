using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using Microsoft.IdentityModel.Tokens;

namespace IKhethelo.Digital.Infrastructure.Identity;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string PrivateKeyPem { get; set; } = "";
}

public sealed class JwtKeys : IDisposable
{
    private readonly RSA rsa = RSA.Create();
    public RsaSecurityKey Key { get; }
    public JwtKeys(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience) || string.IsNullOrWhiteSpace(settings.PrivateKeyPem))
            throw new InvalidOperationException("Configure JWT issuer, audience and an RS256 private key using protected configuration.");
        rsa.ImportFromPem(settings.PrivateKeyPem);
        if (rsa.KeySize < 2048) throw new InvalidOperationException("JWT RSA key must be at least 2048 bits.");
        Key = new RsaSecurityKey(rsa);
    }
    public void Dispose() => rsa.Dispose();
}

public sealed class JwtTokens(JwtSettings settings, JwtKeys keys, TimeProvider clock) : ITokenIssuer
{
    public TokenResponse Issue(AdminUser admin)
    {
        var now = clock.GetUtcNow();
        var claims = new List<Claim> { new("sub", admin.Id.ToString()), new("role", admin.Role.ToString()),
            new("jti", Guid.NewGuid().ToString()), new("iat", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64) };
        if (admin.TenantId is { } tenant) claims.Add(new("tenant_id", tenant.ToString()));
        claims.Add(new("security_version", admin.SecurityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime, now.AddMinutes(30).UtcDateTime,
            new SigningCredentials(keys.Key, SecurityAlgorithms.RsaSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", 1800);
    }
}
