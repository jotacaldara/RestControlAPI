using Microsoft.IdentityModel.Tokens;
using RestControlAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RestControlAPI.Services
{
    public class JwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

      // Gera um token JWT para o usuário autenticado
       
        public string GenerateToken(User user, int? restaurantId = null)
        {
            // 1. Buscar configurações do appsettings.json
            var secretKey = _configuration["JwtSettings:SecretKey"];
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience = _configuration["JwtSettings:Audience"];
            var expirationHours = int.Parse(_configuration["JwtSettings:ExpirationHours"] ?? "8");

            // 2. Criar chave de segurança
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 3. Criar Claims (informações do usuário no token)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.Name.ToString()),
                new Claim("UserId", user.UserId.ToString()) // Claim customizada
            };

            // Se for Owner, adicionar o RestaurantId
            if (restaurantId.HasValue && user.Role.ToString() == "Owner")
            {
                claims.Add(new Claim("RestaurantId", restaurantId.Value.ToString()));
            }

            // 4. Criar o Token
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expirationHours),
                signingCredentials: credentials
            );

            // 5. Serializar para string
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return tokenString;
        }

     // Valida se um token é válido (opcional - use para debug)

        public bool ValidateToken(string token)
        {
            try
            {
                var secretKey = _configuration["JwtSettings:SecretKey"];
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

                var tokenHandler = new JwtSecurityTokenHandler();
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["JwtSettings:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["JwtSettings:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}