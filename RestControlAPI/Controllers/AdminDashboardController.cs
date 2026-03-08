using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

namespace RestControlAPI.Controllers
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class AdminDashboardController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;

        public AdminDashboardController(nextlayerapps_SampleDBContext context)
        {
            _context = context;
        }

        [HttpGet("stats")]
        public async Task<ActionResult<DashboardKpiDTO>> GetStats()
        {
            var totalEarnings = await _context.PlatformEarnings
               .SumAsync(e => e.Amount);

            var stats = new DashboardKpiDTO
            {
                TotalRestaurants = await _context.Restaurants.CountAsync(),
                TotalReservations = await _context.Reservations.CountAsync(),
              
                TotalRevenue = totalEarnings,
                
                PendingApprovals = await _context.Restaurants.CountAsync(r => r.IsActive == false || r.IsActive == null)
            };

            return Ok(stats);
        }

        [HttpGet("pending-restaurants")]
        public async Task<ActionResult<IEnumerable<PendingRestaurantsDTO>>> GetPendingRestaurants()
        {
            var pendingRestaurants = await _context.Restaurants
                .Where(r => r.IsActive == false || r.IsActive == null)
                .Include(r => r.Owner)
                .Include(r => r.RestaurantSubscriptions)
                    .ThenInclude(s => s.Plan)
                .Select(r => new PendingRestaurantsDTO
                {
                    RestaurantId = r.RestaurantId,
                    RestaurantName = r.Name,
                    Description = r.Description,
                    Address = r.Address,
                    City = r.City,
                    Phone = r.Phone,
                    Email = r.Email,
                    OwnerName = r.Owner != null ? r.Owner.FullName : "N/A",
                    OwnerEmail = r.Owner != null ? r.Owner.Email : "N/A",
                    OwnerPhone = r.Owner != null ? r.Owner.Phone : "N/A",
                    CreatedAt = r.CreatedAt,
                    DaysWaiting = (DateTime.UtcNow - (r.CreatedAt ?? DateTime.UtcNow)).Days,
                    PlanId = r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false) != null ? r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false).PlanId : 0,
                    PlanName = r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false) != null ? r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false).Plan.Name : "N/A",
                    MonthlyPrice = r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false) != null ? r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false).Plan.MonthlyPrice : 0,
                    CommissionRate = r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false) != null ? r.RestaurantSubscriptions.FirstOrDefault(s => s.IsActive == false).Plan.ReservationCommission : 0
                })
                .ToListAsync();
            return Ok(pendingRestaurants);

        }



        [HttpPost("pending-restaurants/{id}/approve")]
        public async Task<IActionResult> ApproveRestaurant(int id, [FromBody] ApproveRestaurantDto dto)
        {
            var restaurant = await _context.Restaurants
                 .Include(r => r.Owner)
                 .Include(r => r.RestaurantSubscriptions)
                 .FirstOrDefaultAsync(r => r.RestaurantId == id);

            if (restaurant == null)
                return NotFound(new { message = "Restaurante não encontrado." });

            if (restaurant.IsActive == true)
                return BadRequest(new { message = "Restaurante já está ativo." });


            try
            {
                restaurant.IsActive = true;

                if (restaurant.Owner != null)
                {
                    restaurant.Owner.IsActive = true;
                }

                var pendingSubscription = restaurant.RestaurantSubscriptions
                    .FirstOrDefault(s => s.IsActive == false);

                if (pendingSubscription != null)
                {
                    pendingSubscription.IsActive = true;
                    pendingSubscription.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
                    pendingSubscription.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1));
                }
                else
                {
                    //Fallback
                    var planId = dto.PlanId > 0 ? dto.PlanId : 2; 

                    var newSubscription = new RestaurantSubscription
                    {
                        RestaurantId = restaurant.RestaurantId,
                        PlanId = planId,
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.RestaurantSubscriptions.Add(newSubscription);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Restaurante aprovado com sucesso! Subscription ativada."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro: {ex.Message}" });
            }
        }

        [HttpPost("pending-restaurants/{id}/reject")]
        public async Task<IActionResult> RejectRestaurant(int id, [FromBody] RejectReasonDto dto)
        {
            var restaurant = await _context.Restaurants
                .Include(r => r.Owner)
                .Include(r => r.RestaurantSubscriptions)
                .FirstOrDefaultAsync(r => r.RestaurantId == id);

            if (restaurant == null)
                return NotFound(new { message = "Restaurante não encontrado." });

            try
            {
                var owner = restaurant.Owner;

                // Eliminar subscription pendente
                var pendingSubscription = restaurant.RestaurantSubscriptions
                    .FirstOrDefault(s => s.IsActive == false);

                if (pendingSubscription != null)
                {
                    _context.RestaurantSubscriptions.Remove(pendingSubscription);
                }

                _context.Restaurants.Remove(restaurant);

                if (owner != null)
                {
                    _context.Users.Remove(owner);
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Pedido rejeitado e eliminado." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro: {ex.Message}" });
            }
        }
    

        [HttpGet("revenue-data")]
        public async Task<IActionResult> GetRevenueData()
        {
            var currentYear = DateTime.Now.Year;

            // Busca os dados agrupados por mês da tabela PlatformEarnings
            var earnings = await _context.PlatformEarnings
                .Where(e => e.CreatedAt.HasValue && e.CreatedAt.Value.Year == currentYear)
                .GroupBy(e => e.CreatedAt.Value.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Total = g.Sum(e => e.Amount)
                })
                .OrderBy(x => x.Month)
                .ToListAsync();

            var chartData = Enumerable.Range(1, 12).Select(month => new
            {
                Month = month,
                Total = earnings.FirstOrDefault(e => e.Month == month)?.Total ?? 0
            }).ToList();

            return Ok(chartData);
        }

        [HttpGet("api/admin/earnings")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPlatformEarnings()
        {
            // Total geral
            var totalEarnings = await _context.PlatformEarnings
                .SumAsync(e => e.Amount);

            // Por restaurante
            var earningsByRestaurant = await _context.PlatformEarnings
                .Include(e => e.Restaurant)
                .GroupBy(e => new { e.RestaurantId, RestaurantName = e.Restaurant.Name })
                .Select(g => new
                {
                    RestaurantId = g.Key.RestaurantId,
                    RestaurantName = g.Key.RestaurantName,
                    TotalEarnings = g.Sum(e => e.Amount),
                    TotalReservations = g.Count()
                })
                .OrderByDescending(x => x.TotalEarnings)
                .ToListAsync();

            // Por mês (últimos 6 meses)
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var earningsByMonth = await _context.PlatformEarnings
                .Where(e => e.CreatedAt >= sixMonthsAgo)
                .GroupBy(e => new { e.CreatedAt.Value.Year, e.CreatedAt.Value.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    Total = g.Sum(e => e.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            return Ok(new
            {
                TotalEarnings = totalEarnings,
                ByRestaurant = earningsByRestaurant,
                ByMonth = earningsByMonth
            });
        }

        public class RejectReasonDto
        {
            public string Reason { get; set; }
        }


    }

}