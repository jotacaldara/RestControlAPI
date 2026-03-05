using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

namespace RestControlAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegistrationController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;

        public RegistrationController(nextlayerapps_SampleDBContext context)
        {
            _context = context;
        }

        // POST: api/registration/restaurant
        [HttpPost("restaurant")]
        public async Task<IActionResult> RegisterRestaurant([FromBody] RestaurantRegistrationApiDTO dto)
        {
            //Validações básicas
            if (dto.MaxTables < 1)
                return BadRequest(new { message = "O número de mesas tem de ser pelo menos 1." });

            if (dto.MaxSeats < dto.MaxTables)
                return BadRequest(new { message = "O número de lugares não pode ser inferior ao número de mesas." });

            if (dto.ClosingTime <= dto.OpeningTime)
                return BadRequest(new { message = "A hora de fecho tem de ser posterior à hora de abertura." });

            var plan = await _context.Plans.FindAsync(dto.PlanId);
            if (plan == null)
                return BadRequest(new { message = "Plano inválido." });

            //buscar users para verificar se email já existe
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.OwnerEmail);
            if (existingUser != null)
                return BadRequest(new { message = "Este email já está registado." });

            //buscar role para associar ao novo user
            var ownerRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name == "Owner");
            if (ownerRole == null)
                return StatusCode(500, new { message = "Erro: Role 'Owner' não encontrada." });

            var newOwner = new User
            {
                FullName = dto.OwnerName,
                Email = dto.OwnerEmail,
                Phone = dto.OwnerPhone,
                PasswordHash = dto.Password,
                RoleId = ownerRole.RoleId,
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newOwner);
            await _context.SaveChangesAsync();

            var newRestaurant = new Restaurant
            {
                Name = dto.RestaurantName,
                Description = dto.Description,
                Address = dto.Address,
                City = dto.City,
                Phone = dto.RestaurantPhone,
                Email = dto.RestaurantEmail,
                OwnerId = newOwner.UserId,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                OpeningTime = dto.OpeningTime,
                ClosingTime = dto.ClosingTime,
                MaxTables = dto.MaxTables,  
                MaxSeats = dto.MaxSeats   
            };

            _context.Restaurants.Add(newRestaurant);
            await _context.SaveChangesAsync();

            // ── Criar subscrição PENDENTE ─────────────────────────────
            var pendingSubscription = new RestaurantSubscription
            {
                RestaurantId = newRestaurant.RestaurantId,
                PlanId = dto.PlanId,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.RestaurantSubscriptions.Add(pendingSubscription);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Pedido submetido com sucesso! Aguarde aprovação do administrador.",
                restaurantId = newRestaurant.RestaurantId,
                ownerId = newOwner.UserId,
                planId = dto.PlanId
            });

        }
    }
}