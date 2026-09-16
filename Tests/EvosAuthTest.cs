using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EvoS.DirectoryServer;
using Microsoft.IdentityModel.Tokens;

namespace Tests;

// Guards M1: token validation must accept only correctly-signed HS512 tokens and reject "none" / other
// algorithms. Exercises the exact TokenValidationParameters EvosAuth builds.
public class EvosAuthTest
{
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes(new string('k', 64)));

    private static readonly JwtSecurityTokenHandler Handler = new JwtSecurityTokenHandler();

    private static string CreateToken(string algorithm, SecurityKey key, DateTime? expires = null)
    {
        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "42") }),
            Issuer = EvosAuth.TokenIssuer,
            Audience = EvosAuth.TokenAudience,
            NotBefore = DateTime.UtcNow.AddMinutes(-10),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(key, algorithm),
        };
        return Handler.WriteToken(Handler.CreateToken(descriptor));
    }

    private static void Validate(string token)
    {
        Handler.ValidateToken(token, EvosAuth.BuildValidationParameters(Key), out _);
    }

    [Fact]
    public void ValidHs512Token_Validates()
    {
        Validate(CreateToken(SecurityAlgorithms.HmacSha512Signature, Key)); // header alg == "HS512"
    }

    [Fact]
    public void UnsignedNoneToken_IsRejected()
    {
        // Craft an unsigned "alg: none" token by hand (the handler will not emit one).
        string Encode(string json) =>
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
        string noneToken =
            Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." +
            Encode($"{{\"iss\":\"{EvosAuth.TokenIssuer}\",\"aud\":\"{EvosAuth.TokenAudience}\",\"nameid\":\"42\"}}") + ".";

        Assert.ThrowsAny<Exception>(() => Validate(noneToken));
    }

    [Fact]
    public void DifferentAlgorithm_IsRejected()
    {
        // Same key, but signed with HS256 - must be rejected because the algorithm is pinned to HS512.
        Assert.ThrowsAny<Exception>(() => Validate(CreateToken(SecurityAlgorithms.HmacSha256Signature, Key)));
    }

    [Fact]
    public void WrongSigningKey_IsRejected()
    {
        SymmetricSecurityKey otherKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 64)));
        Assert.ThrowsAny<Exception>(() => Validate(CreateToken(SecurityAlgorithms.HmacSha512Signature, otherKey)));
    }

    [Fact]
    public void ExpiredToken_IsRejected()
    {
        string expired = CreateToken(SecurityAlgorithms.HmacSha512Signature, Key, DateTime.UtcNow.AddMinutes(-5));
        Assert.Throws<SecurityTokenExpiredException>(() => Validate(expired));
    }
}
