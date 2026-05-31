using IntelligentSalesAssistantAPI.Exceptions;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.Models;

namespace IntelligentSalesAssistantAPI.Controllers
{
    /// <summary>
    /// Hanterar autentisering och JWT-token-generering
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public AuthController(IConfiguration configuration, ApplicationDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        /// <summary>
        /// Request-modell för inloggning
        /// </summary>
        public class LoginRequest
        {
            [Required]
            public string Username { get; set; } = string.Empty;
            [Required]
            public string Password { get; set; } = string.Empty;
        }
        
        /// <summary>
        /// Response-modell med JWT-token
        /// </summary>
        public record LoginResponse(string Token);

        /// <summary>
        /// Autentiserar användare och genererar JWT-token
        /// </summary>
        /// <param name="request">Inloggningsuppgifter (användarnamn och lösenord)</param>
        /// <returns>JWT-token som används för att autentisera API-anrop</returns>
        /// <response code="200">Returnerar JWT-token vid lyckad inloggning</response>
        /// <response code="400">Om inloggningsdata är ogiltig</response>
        /// <response code="401">Om användarnamn eller lösenord är felaktigt</response>
        /// <remarks>
        /// Exempel på request:
        /// 
        ///     POST /api/auth/login
        ///     {
        ///       "username": "admin",
        ///       "password": "your-password"
        ///     }
        /// 
        /// Tillgängliga användare:
        /// - admin (roll: Admin)
        /// - seller1 (roll: Seller)
        /// 
        /// Token är giltig i 2 timmar och måste inkluderas i Authorization-headern för alla skyddade endpoints:
        /// Authorization: Bearer {token}
        /// </remarks>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                throw new IntelligentSalesAssistantAPI.Exceptions.ValidationException("Felaktig inloggningsdata.");
            }

            // Sök efter användaren i databasen
            var user = _context.Users.SingleOrDefault(u => u.Username == request.Username);
            if (user == null)
            {
                throw new UnauthorizedException("Felaktigt användarnamn eller lösenord.");
            }

            // Verifiera lösenordshash
            var hasher = new PasswordHasher<User>();
            var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedException("Felaktigt användarnamn eller lösenord.");
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var jwtSection = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new LoginResponse(tokenString));
        }
    }
}