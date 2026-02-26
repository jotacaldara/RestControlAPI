

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;
using System;
using System.Globalization;

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

                // Total de produtos
                var totalProducts = await _context.Products
                    .Where(p => p.RestaurantId == restaurantId)
                    .CountAsync();

            var totalReviews = await _context.Reviews
                    .Where(p => p.RestaurantId == restaurantId)
                    .CountAsync();

            var averageRating = totalReviews > 0
                ? await _context.Reviews.Where(p => p.RestaurantId == restaurantId).
                AverageAsync(rev => (double)rev.Rating)
                : 0.0;

            // FATURAMENTO - Calcular receita total
            var totalRevenue = await _context.Orders
                    .Where(o => o.RestaurantId == restaurantId)
                    .SelectMany(o => o.Payments)
                    .SumAsync(p => p.Amount);

                // Comissões pagas à plataforma
                var totalCommissions = await _context.PlatformEarnings
                    .Where(e => e.RestaurantId == restaurantId)
                    .SumAsync(e => e.Amount);

                // Receita líquida (depois das comissões)
                var netRevenue = totalRevenue - totalCommissions;


                // SUBSCRIÇÃO ATIVA
                var subscription = await _context.RestaurantSubscriptions
                    .Include(s => s.Plan)
                    .Where(s => s.RestaurantId == restaurantId && s.IsActive == true)
                    .FirstOrDefaultAsync();

                object subscriptionData = null;
                if (subscription != null)
                {
                    var daysRemaining = subscription.EndDate.HasValue
                        ? (subscription.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days
                        : 9999;

                    subscriptionData = new
                    {
                        subscription.SubscriptionId,
                        PlanName = subscription.Plan.Name,
                        MonthlyPrice = subscription.Plan.MonthlyPrice,
                        Commission = subscription.Plan.ReservationCommission,
                        StartDate = subscription.StartDate,
                        EndDate = subscription.EndDate,
                        DaysRemaining = daysRemaining,
                        IsExpiring = daysRemaining <= 5 && daysRemaining > 0,
                        IsActive = subscription.IsActive
                    };
                }

                return Ok(new
                {
                    RestaurantName = restaurant.Name,
                    City = restaurant.City,
                    TotalReservations = totalReservations,
                    PendingReservations = pendingReservations,
                    AverageRating = averageRating, // Implementar reviews depois
                    TotalReviews = totalReviews,
                    TotalProducts = totalProducts,
                    // Faturamento
                    TotalRevenue = totalRevenue,
                    TotalCommissions = totalCommissions,
                    NetRevenue = netRevenue,
                    // Subscrição
                    Subscription = subscriptionData
                });
            }

            [HttpGet("revenue")]
            public async Task<IActionResult> GetRevenue()
            {
                var restaurantId = await GetOwnerRestaurantIdAsync();
                if (!restaurantId.HasValue)
                    return NotFound(new { message = "Restaurante não encontrado." });

                // Receita total
                var totalRevenue = await _context.Orders
                    .Where(o => o.RestaurantId == restaurantId)
                    .SelectMany(o => o.Payments)
                    .SumAsync(p => p.Amount);

                // Comissões
                var totalCommissions = await _context.PlatformEarnings
                    .Where(e => e.RestaurantId == restaurantId)
                    .SumAsync(e => e.Amount);

                // Líquido
                var netRevenue = totalRevenue - totalCommissions;

                // Total de reservas com pagamento
                var totalReservations = await _context.Orders
                    .Where(o => o.RestaurantId == restaurantId && o.ReservationId != null)
                    .Select(o => o.ReservationId)
                    .Distinct()
                    .CountAsync();

                // Valor médio por reserva
                var avgReservationValue = totalReservations > 0
                    ? totalRevenue / totalReservations
                    : 0;

                // Receita por mês (últimos 6 meses)
                var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                var monthlyData = await _context.Orders
                    .Where(o => o.RestaurantId == restaurantId && o.OrderDate >= sixMonthsAgo)
                    .GroupBy(o => new { o.OrderDate.Value.Year, o.OrderDate.Value.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Revenue = g.SelectMany(o => o.Payments).Sum(p => p.Amount)
                    })
                    .OrderBy(x => x.Year).ThenBy(x => x.Month)
                    .ToListAsync();

                // Adicionar comissões por mês
                var monthlyCommissions = await _context.PlatformEarnings
                    .Where(e => e.RestaurantId == restaurantId && e.CreatedAt >= sixMonthsAgo)
                    .GroupBy(e => new { e.CreatedAt.Value.Year, e.CreatedAt.Value.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Commissions = g.Sum(e => e.Amount)
                    })
                    .ToListAsync();

                var monthlyResult = monthlyData.Select(m =>
                {
                    var commission = monthlyCommissions
                        .FirstOrDefault(c => c.Year == m.Year && c.Month == m.Month)
                        ?.Commissions ?? 0;

                    var monthName = new DateTime(m.Year, m.Month, 1)
                        .ToString("MMMM", new CultureInfo("pt-PT"));

                    return new
                    {
                        Month = m.Month,
                        Year = m.Year,
                        MonthName = monthName,
                        Revenue = m.Revenue,
                        Commissions = commission,
                        Net = m.Revenue - commission
                    };
                }).ToList();

                return Ok(new
                {
                    TotalRevenue = totalRevenue,
                    TotalCommissions = totalCommissions,
                    NetRevenue = netRevenue,
                    TotalReservations = totalReservations,
                    AverageReservationValue = avgReservationValue,
                    MonthlyData = monthlyResult
                });
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
                    : 9999;

                return Ok(new
                {
                    subscription.SubscriptionId,
                    PlanName = subscription.Plan.Name,
                    MonthlyPrice = subscription.Plan.MonthlyPrice,
                    Commission = subscription.Plan.ReservationCommission,
                    StartDate = subscription.StartDate,
                    EndDate = subscription.EndDate,
                    DaysRemaining = daysRemaining,
                    IsExpiring = daysRemaining <= 5 && daysRemaining > 0,
                    IsActive = subscription.IsActive
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
                    AverageRating = 0.0,
                    TotalReviews = 0
                });
            }


            [HttpPut("restaurant")]
            public async Task<IActionResult> UpdateRestaurant([FromBody] RestaurantUpdateDTO dto)
            {
                var restaurantId = await GetOwnerRestaurantIdAsync();
                if (!restaurantId.HasValue)
                    return NotFound(new { message = "Restaurante não encontrado." });

                var restaurant = await _context.Restaurants.FindAsync(restaurantId.Value);
                if (restaurant == null) return NotFound();

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
                    Price = (decimal)dto.Price,
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
                product.Price = (decimal)dto.Price;
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

            [HttpGet("reservations")]
            public async Task<IActionResult> GetReservations([FromQuery] string? status)
            {
                var restaurantId = await GetOwnerRestaurantIdAsync();
                if (!restaurantId.HasValue)
                    return NotFound(new { message = "Restaurante não encontrado." });

                var query = _context.Reservations
                    .Include(r => r.Customer)
                    .Include(r => r.Orders)
                        .ThenInclude(o => o.Payments)
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

                if (dto.Status != "Confirmada" && dto.Status != "Cancelada")
                    return BadRequest(new { message = "Status inválido." });

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
                    await _context.SaveChangesAsync();
                }

                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    Amount = dto.Amount,
                    PaymentMethod = dto.PaymentMethod ?? "Dinheiro",
                    PaymentDate = DateTime.Now,
                    Status = "Pago"
                };

                _context.Payments.Add(payment);

                // Calcular comissão
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

        [HttpGet("reviews")]
        public async Task<IActionResult> GetRestaurantReviews()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.RestaurantId == restaurantId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    ReviewId = r.Id,
                    UserName = r.User.FullName,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    Date = r.CreatedAt.ToString(),
                    Reply = r.Reply,
                    RepliedAt = r.RepliedAt
                })
                .ToListAsync();

            return Ok(reviews);
        }

   
        [HttpPost("reviews/{reviewId}/reply")]
        public async Task<IActionResult> ReplyToReview(int reviewId, [FromBody] ReplyReviewDto dto)
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var review = await _context.Reviews
                .Where(r => r.Id == reviewId && r.RestaurantId == restaurantId)
                .FirstOrDefaultAsync();

            if (review == null)
                return NotFound(new { message = "Review não encontrada." });

            if (!string.IsNullOrWhiteSpace(review.Reply))
                return BadRequest(new { message = "Já respondeu a esta review." });

            review.Reply = dto.Reply;
            review.RepliedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Resposta enviada com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro: {ex.Message}" });
            }
        }

        // ========================================================================
        // PAYMENTS - GET api/owner/payments
        // Histórico de pagamentos do restaurante (comissões pagas)
        // ========================================================================
        [HttpGet("payments")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var restaurantId = await GetOwnerRestaurantIdAsync();
            if (!restaurantId.HasValue)
                return NotFound(new { message = "Restaurante não encontrado." });

            var payments = await _context.PlatformEarnings
                .Include(e => e.Reservation)
                    .ThenInclude(r => r.Customer)
                .Where(e => e.RestaurantId == restaurantId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    PaymentId = e.EarningId,
                    ReservationId = e.ReservationId,
                    CustomerName = e.Reservation.Customer.FullName,
                    CommissionAmount = e.Amount,
                    Date = e.CreatedAt,
                    Month = e.CreatedAt.HasValue ? e.CreatedAt.Value.ToString("MMMM yyyy") : ""
                })
                .ToListAsync();

            var totalCommissions = payments.Sum(p => p.CommissionAmount);

            return Ok(new
            {
                TotalCommissions = totalCommissions,
                Payments = payments
            });
        }
    }


        // DTOs

        public class UpdateStatusDto
        {
            public string Status { get; set; }
        }

        public class AddAmountDto
        {
            public decimal Amount { get; set; }
            public string PaymentMethod { get; set; }
        }


    public class ReplyReviewDto
    {
        public string Reply { get; set; }
    }

}
