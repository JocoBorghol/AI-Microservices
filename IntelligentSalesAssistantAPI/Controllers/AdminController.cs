using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using IntelligentSalesAssistantAPI.Data;
using IntelligentSalesAssistantAPI.Models;
using IntelligentSalesAssistantAPI.Exceptions;
using System;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using ValidationException = IntelligentSalesAssistantAPI.Exceptions.ValidationException;


namespace IntelligentSalesAssistantAPI.Controllers
{
    /// <summary>
    /// Tillhandahåller administrativa funktioner. Endast tillgänglig för användare med rollen 'Admin'.
    /// </summary>
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")] // Begränsar hela controllern till användare med rollen 'Admin'
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Request-modell för registrering av säljare
        /// </summary>
        public class RegisterSellerRequest
        {
            [Required(ErrorMessage = "Användarnamn krävs.")]
            [MinLength(3, ErrorMessage = "Användarnamnet måste vara minst 3 tecken.")]
            [MaxLength(100, ErrorMessage = "Användarnamnet får inte överstiga 100 tecken.")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Lösenord krävs.")]
            [MinLength(6, ErrorMessage = "Lösenordet måste vara minst 6 tecken.")]
            public string Password { get; set; } = string.Empty;
        }

        /// <summary>
        /// Registrerar en ny säljare (Seller) i databasen.
        /// </summary>
        /// <param name="request">Användarnamn och lösenord för den nya säljaren</param>
        /// <returns>Framgångsmeddelande</returns>
        /// <response code="200">Om registreringen lyckades</response>
        /// <response code="400">Om indata är ogiltig eller användarnamnet redan är upptaget</response>
        /// <response code="401">Om anroparen inte är inloggad</response>
        /// <response code="403">Om anroparen inte har rollen 'Admin'</response>
        [HttpPost("register-seller")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public IActionResult RegisterSeller([FromBody] RegisterSellerRequest request)
        {
            if (!ModelState.IsValid)
            {
                throw new ValidationException("Felaktig registreringsdata.");
            }

            // Kontrollera om användarnamnet redan är upptaget
            if (_context.Users.Any(u => u.Username == request.Username))
            {
                throw new ValidationException("Användarnamnet är redan upptaget.");
            }

            var hasher = new PasswordHasher<User>();
            var newSeller = new User
            {
                Username = request.Username,
                Role = "Seller", // Framtvingar Seller-rollen för säkerhet
                CreatedAt = DateTime.UtcNow
            };
            newSeller.PasswordHash = hasher.HashPassword(newSeller, request.Password);

            _context.Users.Add(newSeller);
            _context.SaveChanges();

            return Ok(new { Message = $"Säljaren '{request.Username}' har registrerats framgångsrikt." });
        }

        [HttpGet("system-info")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetSystemInfo()
        {
            string environment;
            string storage;
            
            var websiteOwnerName = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME");
            var containerAppEnv = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
            var isRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
            
            if (!string.IsNullOrEmpty(containerAppEnv) || !string.IsNullOrEmpty(websiteOwnerName))
            {
                environment = "Azure Cloud (Container App)";
                storage = "Säker molnlagring (Azure Files)";
            }
            else if (isRunningInContainer)
            {
                environment = "Docker Container (Docker Desktop)";
                storage = "Docker Volume (Lokal disk)";
            }
            else
            {
                environment = "Lokal utvecklingsmiljö (IDE)";
                storage = "Lokal disklagring (HDD/SSD)";
            }

            return Ok(new
            {
                Environment = environment,
                Gateway = Request.Host.Value,
                Storage = storage,
                RunningInContainer = isRunningInContainer
            });
        }
    }
}
