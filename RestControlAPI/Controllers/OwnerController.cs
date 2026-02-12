

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.Models;
using RestControlAPI.DTOs;

namespace RestControlAPI.Controllers
{
    [Route("api/owner")]
    [ApiController]
    [Authorize(Roles = "Owner")]
    public class OwnerController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;

        public OwnerController(nextlayerapps_SampleDBContext context)
        {
            _context = context;
        }

        private async Task<int?> GetOwnerRestaurantIdAsync()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return null;

            var userId = int.Parse(userIdClaim);

            var restaurant = await _context.Restaurants
                .Where(r => r.OwnerId == userId && r.IsActive == true)
                .Select(r => r.RestaurantId)
                .FirstOrDefaultAsync();

            return restaurant == 0 ? null : restaurant;
        }
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var restaurant = await _context.Restaurants
                .Where(r => r.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            // Total de reservas
            var totalReservations = await _context.Reservations
                .Where(r => r.RestaurantId == restaurantId)
                .CountAsync();

            // Reservas pendentes
            var pendingReservations = await _context.Reservations
                .Where(r => r.RestaurantId == restaurantId && r.Status == "Pendente")
                .CountAsync();

            var avgRating = 0.0;
            var totalReviews = 0;
            var reviews = await _context.Reviews
               .Where(r => r.RestaurantId == restaurantId)
               .ToListAsync();
            if (reviews.Any())
            {
                avgRating = reviews.Average(r => r.Rating);
                totalReviews = reviews.Count;
            }

            // Total de produtos
            var totalProducts = await _context.Products
                .Where(p => p.RestaurantId == restaurantId)
                .CountAsync();

            return Ok(new
            {
                RestaurantName = restaurant.Name,
                City = restaurant.City,
                TotalReservations = totalReservations,
                PendingReservations = pendingReservations,
                AverageRating = avgRating,
                TotalReviews = totalReviews,
                TotalProducts = totalProducts
            });
        }

        [HttpGet("restaurant")]
        public async Task<IActionResult> GetRestaurant()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var restaurant = await _context.Restaurants
                .Include(r => r.RestaurantImages)
                .Where(r => r.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            if (restaurant == null)
                return NotFound();

            // Buscar categorias e produtos
            var categories = await _context.Categories
                .Where(c => c.RestaurantId == restaurantId)
                .Select(c => new
                {
                    c.CategoryId,
                    c.Name,
                    Products = _context.Products
                        .Where(p => p.CategoryId == c.CategoryId)
                        .Select(p => new
                        {
                            p.ProductId,
                            p.Name,
                            p.Description,
                            p.Price,
                            p.IsAvailable
                        }).ToList()
                }).ToListAsync();

            return Ok(new
            {
                restaurant.RestaurantId,
                restaurant.Name,
                restaurant.Description,
                restaurant.Address,
                restaurant.City,
                restaurant.Phone,
                restaurant.Email,
                Images = restaurant.RestaurantImages.Select(i => i.ImageUrl).ToList(),
                MenuCategories = categories,
                AverageRating = 0.0, // Adicione lógica de reviews aqui
                TotalReviews = 0
            });
        }

     
        [HttpPut("restaurant")]
        public async Task<IActionResult> UpdateRestaurant([FromBody] RestaurantUpdateDto dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var restaurant = await _context.Restaurants.FindAsync(restaurantId.Value);
            if (restaurant == null) return NotFound();

            // Owner só pode editar Description e Phone
            restaurant.Description = dto.Description;
            restaurant.Phone = dto.Phone;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Restaurante atualizado com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro: {ex.Message}" });
            }
        }

        [HttpGet("reservations")]
        public async Task<IActionResult> GetReservations([FromQuery] string? status)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var query = _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Orders) // Para pegar o pagamento
                .Where(r => r.RestaurantId == restaurantId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var reservations = await query
                .OrderByDescending(r => r.ReservationDate)
                .Select(r => new
                {
                    r.ReservationId,
                    r.ReservationDate,
                    r.NumberOfPeople,
                    r.Status,
                    CustomerName = r.Customer.FullName,
                    CustomerEmail = r.Customer.Email,
                    CustomerPhone = r.Customer.Phone,
                    // Calcular total gasto (soma dos pagamentos)
                    TotalAmount = r.Orders
                        .SelectMany(o => o.Payments)
                        .Sum(p => p.Amount)
                })
                .ToListAsync();

            return Ok(reservations);
        }

        [HttpPut("reservations/{id}/status")]
        public async Task<IActionResult> UpdateReservationStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var reservation = await _context.Reservations
                .Where(r => r.ReservationId == id && r.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            if (reservation == null)
                return NotFound(new { message = "Reserva não encontrada." });

            // Validar status
            if (dto.Status != "Confirmada" && dto.Status != "Cancelada")
                return BadRequest(new { message = "Status inválido. Use 'Confirmada' ou 'Cancelada'." });

            reservation.Status = dto.Status;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = $"Reserva {dto.Status.ToLower()} com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro: {ex.Message}" });
            }
        }
        [HttpPut("reservations/{id}/amount")]
        public async Task<IActionResult> AddReservationAmount(int id, [FromBody] AddAmountDto dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var reservation = await _context.Reservations
                .Where(r => r.ReservationId == id && r.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            if (reservation == null)
                return NotFound(new { message = "Reserva não encontrada." });

            // Criar ou atualizar Order e Payment
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.ReservationId == id);

            if (order == null)
            {
                order = new Order
                {
                    RestaurantId = restaurantId.Value,
                    ReservationId = id,
                    OrderDate = DateTime.Now,
                    Status = "Pago"
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Salvar para pegar OrderId
            }

            // Criar Payment
            var payment = new Payment
            {
                OrderId = order.OrderId,
                Amount = dto.Amount,
                PaymentMethod = dto.PaymentMethod ?? "Dinheiro",
                PaymentDate = DateTime.Now,
                Status = "Pago"
            };

            _context.Payments.Add(payment);

            // Calcular comissão da plataforma
            var subscription = await _context.RestaurantSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.RestaurantId == restaurantId && s.IsActive == true)
                .FirstOrDefaultAsync();

            if (subscription != null)
            {
                var commissionPercentage = subscription.Plan.ReservationCommission;
                var commissionAmount = dto.Amount * (commissionPercentage / 100);

                var earning = new PlatformEarning
                {
                    RestaurantId = restaurantId.Value,
                    ReservationId = id,
                    Amount = commissionAmount,
                    CreatedAt = DateTime.Now
                };
                _context.PlatformEarnings.Add(earning);
            }

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Valor adicionado com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro: {ex.Message}" });
            }
        }

    
        [HttpGet("menu")]
        public async Task<IActionResult> GetMenu()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var categories = await _context.Categories
                .Where(c => c.RestaurantId == restaurantId)
                .Select(c => new
                {
                    c.CategoryId,
                    c.Name,
                    Products = _context.Products
                        .Where(p => p.CategoryId == c.CategoryId)
                        .Select(p => new
                        {
                            p.ProductId,
                            p.Name,
                            p.Description,
                            p.Price,
                            p.IsAvailable
                        }).ToList()
                }).ToListAsync();

            return Ok(categories);
        }

        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryDTO dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var category = new Category
            {
                RestaurantId = restaurantId.Value,
                Name = dto.Name
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return Ok(new { categoryId = category.CategoryId, message = "Categoria criada!" });
        }

        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var category = await _context.Categories
                .Where(c => c.CategoryId == id && c.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            if (category == null)
                return NotFound();

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Categoria removida!" });
        }

        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductDto dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var product = new Product
            {
                RestaurantId = restaurantId.Value,
                CategoryId = dto.CategoryId,
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                IsAvailable = dto.IsAvailable
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return Ok(new { productId = product.ProductId, message = "Produto criado!" });
        }

        [HttpPut("products/{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductDto dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var product = await _context.Products
                .Where(p => p.ProductId == id && p.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            if (product == null)
                return NotFound();

            product.CategoryId = dto.CategoryId;
            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.IsAvailable = dto.IsAvailable;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Produto atualizado!" });
        }

        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var product = await _context.Products
                .Where(p => p.ProductId == id && p.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            if (product == null)
                return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Produto removido!" });
        }

        [HttpGet("subscription")]
        public async Task<IActionResult> GetSubscription()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var subscription = await _context.RestaurantSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.RestaurantId == restaurantId && s.IsActive == true)
                .FirstOrDefaultAsync();

            if (subscription == null)
                return NotFound(new { message = "Nenhuma subscrição ativa." });

            var daysRemaining = subscription.EndDate.HasValue
                ? (subscription.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days
                : 0;

            return Ok(new
            {
                subscription.SubscriptionId,
                PlanName = subscription.Plan.Name,
                MonthlyPrice = subscription.Plan.MonthlyPrice,
                Commission = subscription.Plan.ReservationCommission,
                StartDate = subscription.StartDate,
                EndDate = subscription.EndDate,
                DaysRemaining = daysRemaining,
                IsExpiring = daysRemaining <= 5
            });
        }
    }

    public class RestaurantUpdateDto
    {
        public string Description { get; set; }
        public string Phone { get; set; }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } 
    }

    public class AddAmountDto
    {
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
    }


}