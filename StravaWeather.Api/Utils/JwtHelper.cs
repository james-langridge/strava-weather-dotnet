
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace StravaWeather.Api.Utils
{
    public class JwtHelper
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtHelper> _logger;
        private readonly JwtSecurityTokenHandler _tokenHandler;
        private readonly byte[] _key;
        
        public JwtHelper(IConfiguration configuration, ILogger<JwtHelper> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _tokenHandler = new JwtSecurityTokenHandler();
            
            var jwtSecret = configuration["JWT_SECRET"] 
                ?? throw new InvalidOperationException("JWT_SECRET not configured");
            _key = Encoding.UTF8.GetBytes(jwtSecret);
        }
        
        public string GenerateToken(Guid userId, string stravaAthleteId)
        {
            _logger.LogDebug("Generating JWT token for user {UserId}", userId);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim("stravaAthleteId", stravaAthleteId)
                }),
                Expires = DateTime.UtcNow.AddDays(Constants.Jwt.ExpiryDays),
                Issuer = Constants.Jwt.Issuer,
                Audience = Constants.Jwt.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(_key),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            
            var token = _tokenHandler.CreateToken(tokenDescriptor);
            return _tokenHandler.WriteToken(token);
        }
        
        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(_key),
                    ValidateIssuer = true,
                    ValidIssuer = Constants.Jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = Constants.Jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                
                var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Token validation failed");
                return null;
            }
        }
        
        public void SetAuthCookie(HttpResponse response, string token)
        {
            response.Cookies.Append(Constants.Jwt.CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(Constants.Jwt.ExpiryDays),
                Path = "/"
            });
            
            _logger.LogInformation("Authentication cookie set");
        }
        
        public void ClearAuthCookie(HttpResponse response)
        {
            response.Cookies.Delete(Constants.Jwt.CookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
            
            _logger.LogInformation("Authentication cookie cleared");
        }
    }
}