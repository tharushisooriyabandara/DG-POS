using System;
using System.Threading.Tasks;
using POS_UI.Models;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Linq;

namespace POS_UI.Services
{
    public class TokenService
    {
        //private static readonly string SecretKey = "your-secret-key-here-min-16-chars"; // In production, use a secure key management system
        //private static readonly string Issuer = "POS_UI";
        //private static readonly string Audience = "POS_UI_Client";
        //private static readonly int AccessTokenExpiryMinutes = 15;
       // private static readonly int RefreshTokenExpiryDays = 7;

        public bool IsTokenExpired(string token)
        {
            try
            {
                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var expiryClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
                if (expiryClaim != null)
                {
                    var expiryDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiryClaim));
                    return DateTime.UtcNow >= expiryDateTimeOffset.UtcDateTime;
                }
                return true; // If no expiry, treat as expired
            }
            catch
            {
                return true; // If any error, treat as expired
            }
        }

        public bool IsTokenValid(string token)
        {
            return !IsTokenExpired(token);
        }

        public TimeSpan GetTimeUntilExpiry(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                
                var expiryClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;
                if (expiryClaim != null)
                {
                    var expiryDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiryClaim));
                    return expiryDateTimeOffset.UtcDateTime - DateTime.UtcNow;
                }

                return TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }
} 