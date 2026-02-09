using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;
using System.Security.Claims;

namespace RestControlAPI.Controllers
{
    [Route("api/owner")]
    [ApiController]
    [Authorize(Roles = "Owner")] // ← Apenas Owners podem acessar
    public class OwnerController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;

        public OwnerController(nextlayerapps_SampleDBContext context)
        {
            _context = context;
        }

        // Helper: Retorna o RestaurantId do Owner logado
        private async Task<int?> GetOwnerRestaurantIdAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value
                            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return null;

            var restaurant = await _context.Restaurants
                .Where(r => r.OwnerId == userId && (bool)r.IsActive)
                .Select(r => r.RestaurantId)
                .FirstOrDefaultAsync();

            return restaurant == 0 ? null : restaurant;
        }

        // GET: api/owner/restaurant (Detalhes do restaurante do Owner)
        [HttpGet("restaurant")]
        public async Task<ActionResult<RestaurantDetailDto>> GetMyRestaurant()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();

            if (restaurantId == null)
                return NotFound(new { message = "Restaurante não encontrado para este utilizador." });

            var restaurant = await _context.Restaurants
                .Include(r => r.RestaurantImages)
                .Include(r => r.Categories)
                    .ThenInclude(c => c.Products)
                .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);

            if (restaurant == null)
                return NotFound();

            // Buscar reviews
            var reviews = await _context.Reviews
                .Where(r => r.RestaurantId == restaurantId)
                .ToListAsync();

            double average = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
            int total = reviews.Count;

            var dto = new RestaurantDetailDto
            {
                Id = restaurant.RestaurantId,
                Name = restaurant.Name,
                Description = restaurant.Description,
                Address = restaurant.Address,
                City = restaurant.City,
                Phone = restaurant.Phone,
                Images = restaurant.RestaurantImages.Select(i => i.ImageUrl).ToList(),
                AverageRating = (decimal)average,
                TotalReviews = total,
                MenuCategories = restaurant.Categories.Select(c => new CategoryDTO
                {
                    Name = c.Name,
                    Products = c.Products.Select(p => new ProductDTO
                    {
                        Id = p.ProductId,
                        Name = p.Name,
                        Description = p.Description,
                        Price = p.Price
                    }).ToList()
                }).ToList()
            };

            return Ok(dto);
        }

        // GET: api/owner/dashboard (Estatísticas resumidas)
        [HttpGet("dashboard")]
        public async Task<ActionResult<OwnerDashboardDTO>> GetDashboard()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();

            if (restaurantId == null)
                return NotFound(new { message = "Restaurante não encontrado." });

            // Estatísticas básicas
            var totalReservations = await _context.Reservations
                .CountAsync(r => r.RestaurantId == restaurantId);

            var pendingReservations = await _context.Reservations
                .CountAsync(r => r.RestaurantId == restaurantId && r.Status == "Pendente");

            var totalReviews = await _context.Reviews
                .CountAsync(r => r.RestaurantId == restaurantId);

            var averageRating = await _context.Reviews
                .Where(r => r.RestaurantId == restaurantId)
                .AverageAsync(r => (double?)r.Rating) ?? 0;

            var totalProducts = await _context.Products
                .CountAsync(p => p.Category.RestaurantId == restaurantId);

            var restaurant = await _context.Restaurants
                .Where(r => r.RestaurantId == restaurantId)
                .Select(r => new { r.Name, r.City })
                .FirstOrDefaultAsync();

            var dashboard = new OwnerDashboardDTO
            {
                RestaurantId = restaurantId.Value,
                RestaurantName = restaurant?.Name ?? "N/A",
                City = restaurant?.City ?? "N/A",
                TotalReservations = totalReservations,
                PendingReservations = pendingReservations,
                TotalReviews = totalReviews,
                AverageRating = (decimal)averageRating,
                TotalProducts = totalProducts
            };

            return Ok(dashboard);
        }

        // PUT: api/owner/restaurant (Editar informações básicas)
        [HttpPut("restaurant")]
        public async Task<IActionResult> UpdateMyRestaurant(RestaurantUpdateDTO dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();

            if (restaurantId == null)
                return NotFound(new { message = "Restaurante não encontrado." });

            var restaurant = await _context.Restaurants.FindAsync(restaurantId);

            if (restaurant == null)
                return NotFound();

            // Owner pode editar apenas campos específicos
            restaurant.Description = dto.Description;
            restaurant.Phone = dto.Phone;
            // Não pode alterar Name, Address, City (apenas Admin)

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Restaurante atualizado com sucesso." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar: {ex.Message}" });
            }
        }

        // GET: api/owner/reservations (Lista de reservas do restaurante)
        [HttpGet("reservations")]
        public async Task<ActionResult<IEnumerable<ReservationDTO>>> GetMyReservations(
            [FromQuery] string? status = null)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();

            if (restaurantId == null)
                return NotFound(new { message = "Restaurante não encontrado." });

            var query = _context.Reservations
                .Where(r => r.RestaurantId == restaurantId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var reservations = await query
                .OrderByDescending(r => r.ReservationDate)
                .Select(r => new ReservationDTO
                {
                    Id = r.ReservationId,
                    RestaurantName = r.Restaurant.Name,
                    ReservationDate = r.ReservationDate,
                    NumberOfPeople = r.NumberOfPeople,
                    Status = r.Status ?? "Pendente",
                    IsReviewed = _context.Reviews.Any(rev => rev.ReservationId == r.ReservationId)
                })
                .ToListAsync();

            return Ok(reservations);
        }
    }
}