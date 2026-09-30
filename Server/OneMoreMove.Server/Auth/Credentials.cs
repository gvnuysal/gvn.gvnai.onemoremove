using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OneMoreMove.Server.Auth
{
    /// <summary>Device secrets: 256 random bits, stored as a salted PBKDF2-SHA256 hash, compared in constant time.</summary>
    public static class SecretHasher
    {
        private const int Iterations = 100_000;
        private const int HashBytes = 32;

        public static string NewSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));

        public static (byte[] Salt, byte[] Hash) Hash(string secret)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            return (salt, Derive(secret, salt));
        }

        public static bool Verify(string secret, byte[] salt, byte[] hash) =>
            !string.IsNullOrEmpty(secret) && salt != null && hash != null && CryptographicOperations.FixedTimeEquals(Derive(secret, salt), hash);

        private static byte[] Derive(string secret, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, Iterations, HashAlgorithmName.SHA256, HashBytes);

        private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Issues and validates HMAC-SHA256 access tokens whose subject is the player id.</summary>
    public sealed class TokenIssuer
    {
        public const string Issuer = "onemoremove";

        private readonly SymmetricSecurityKey _key;
        private readonly TimeSpan _lifetime;

        public TokenIssuer(ServerOptions options)
        {
            if (string.IsNullOrEmpty(options.SigningKey) || options.SigningKey.Length < 32)
                throw new InvalidOperationException("Server:SigningKey must be set to at least 32 characters.");

            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
            _lifetime = options.TokenLifetime;
        }

        public TokenValidationParameters ValidationParameters => new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Issuer,
            IssuerSigningKey = _key,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        public (string Token, DateTime ExpiresUtc) Issue(Guid playerId)
        {
            var expires = DateTime.UtcNow.Add(_lifetime);
            var token = new JwtSecurityToken(Issuer, Issuer, new[] { new Claim(JwtRegisteredClaimNames.Sub, playerId.ToString()) },
                DateTime.UtcNow, expires, new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }

        public static bool TryGetPlayerId(ClaimsPrincipal user, out Guid playerId)
        {
            var subject = user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(subject, out playerId);
        }
    }
}
