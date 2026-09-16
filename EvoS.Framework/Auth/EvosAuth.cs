using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using EvoS.Framework;
using EvoS.Framework.Auth;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace EvoS.DirectoryServer;

public class EvosAuth
{
    internal const string TokenIssuer = "AtlasReactorServer";
    internal const string TokenAudience = "AtlasReactorPlayer";

    public enum Context
    {
        UNKNOWN,
        USER_API,
        ADMIN_API,
        TICKET_AUTH
    }
    
    public class TokenData
    {
        public long AccountId;
        public IPAddress IpAddress;
        public string Handle;

        public TokenData(long accountId, IPAddress ipAddress, string handle)
        {
            AccountId = accountId;
            IpAddress = ipAddress;
            Handle = handle;
        }
    }

    public static string GenerateToken(Context context, TokenData data)
    {
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken token = tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, data.AccountId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, data.Handle),
                new Claim(ClaimTypes.Dns, data.IpAddress.ToString()),
            }),
            Expires = GetExpires(context),
            Issuer = TokenIssuer,
            Audience = TokenAudience,
            SigningCredentials = new SigningCredentials(GetSigningKey(context, data.AccountId), SecurityAlgorithms.HmacSha512Signature)
        });
        return tokenHandler.WriteToken(token);
    }
    
    public static TokenData ValidateToken(Context context, string token)
    {
        ClaimsPrincipal principal = ValidateToken(context, token, out _);
        return GetTokenData(principal);
    }

    public static TokenData GetTokenData(ClaimsPrincipal principal)
    {
        string accountIdString = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        long.TryParse(accountIdString, out long accountId);
        string ipString = principal.FindFirstValue(ClaimTypes.Dns);
        IPAddress ipAddress = ipString is not null ? IPAddress.Parse(ipString) : null;
        string handle = principal.FindFirstValue(ClaimTypes.Name);

        if (accountId == 0L || ipAddress is null)
        {
            return null;
        }

        return new TokenData(accountId, ipAddress, handle);
    }

    public static ClaimsPrincipal ValidateToken(Context context, string token, out SecurityToken validatedToken)
    {
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

        Claim accountIdClaim = tokenHandler.ReadJwtToken(token).Claims
            .FirstOrDefault(c => JwtRegisteredClaimNames.NameId.Equals(c.Type));
        if (accountIdClaim is null || !long.TryParse(accountIdClaim.Value, out long accountId) || accountId == 0L)
        {
            throw new EvosException(AuthTicket.TICKET_CORRUPT);
        }

        TokenValidationParameters validationParameters = BuildValidationParameters(GetSigningKey(context, accountId));

        return tokenHandler.ValidateToken(token, validationParameters, out validatedToken);
    }

    internal static TokenValidationParameters BuildValidationParameters(SecurityKey signingKey)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = TokenIssuer,
            ValidAudience = TokenAudience,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
            ClockSkew = TimeSpan.Zero,
        };
    }

    private static SecurityKey GetSigningKey(Context context, long accountId)
    {
        string staticKey = context switch
        {
            Context.ADMIN_API => EvosConfiguration.GetAdminApiKey(),
            Context.USER_API => EvosConfiguration.GetUserApiKey(),
            Context.TICKET_AUTH => EvosConfiguration.GetTicketAuthKey(),
            _ => throw new EvosException("Unknown security context")
        };
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
        string dynamicKey = account?.ApiKey ?? string.Empty;
        return Key(staticKey + dynamicKey);
    }

    private static DateTime GetExpires(Context context)
    {
        return context switch
        {
            Context.ADMIN_API => DateTime.UtcNow.AddDays(1),
            Context.USER_API => DateTime.UtcNow.AddDays(30),
            Context.TICKET_AUTH => DateTime.UtcNow.AddMinutes(10),
            _ => throw new EvosException("Unknown security context")
        };
    }

    public static bool ValidateTokenData(HttpContext httpContext, TokenData tokenData, Context authContext)
    {
        return tokenData is not null
               && (SkipIpCheck(authContext)
                    || tokenData.IpAddress.IsSameSubnet(
                        httpContext.Connection.RemoteIpAddress,
                        GetAllowedSubnet(authContext)));
    }

    private static bool SkipIpCheck(Context authContext)
    {
        return EvosConfiguration.GetDisableUserIpCheck() && authContext is Context.TICKET_AUTH or Context.USER_API;
    }

    private static int GetAllowedSubnet(Context context)
    {
        return context switch
        {
            Context.ADMIN_API => 0,
            Context.USER_API => 12,
            Context.TICKET_AUTH => 12,
            _ => throw new EvosException("Unknown security context")
        };
    }

    private static SymmetricSecurityKey Key(string key)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}