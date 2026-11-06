using System.Text;

namespace ConsStock.Api.Auth;

public sealed class JwtSettings
{
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required byte[] Key { get; init; }
    public int LifetimeMinutes { get; init; } = 15;

    public static JwtSettings From(IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var key = configuration["Jwt:Key"];
        var lifetime = configuration.GetValue("Jwt:LifetimeMinutes", 15);
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32 || lifetime is < 1 or > 60)
            throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience, Jwt:Key (mínimo 32 bytes) e validade de 1 a 60 minutos.");
        return new JwtSettings
        {
            Issuer = issuer, Audience = audience, Key = Encoding.UTF8.GetBytes(key), LifetimeMinutes = lifetime
        };
    }
}
