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

        // GET: api/admin/admindashboard/pending-restaurants
        [HttpGet("pending-restaurants")]
        public async Task<IActionResult> GetPending()
        {
            var pending = await _context.Restaurants
                .Include(r => r.Owner)
                .Where(r => r.IsActive == false || r.IsActive == null)
                .Select(r => new AdminRestaurantDTO
                {
                    Id = r.RestaurantId,
                    Name = r.Name,
                    OwnerName = r.Owner != null ? r.Owner.FullName : "Sem Dono",
                    IsActive = false
                })
                .ToListAsync();

            return Ok(pending);
        }

        // POST: api/admin/admindashboard/approve/5
        [HttpPost("approve/{id}")]
        public async Task<IActionResult> ApproveRestaurant(int id)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null) return NotFound();

            restaurant.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Restaurante aprovado e ativo na plataforma." });
        }

        // GET: api/admin/admindashboard/revenue-data
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

        // RestControlAPI/Controllers/AdminController.cs

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
    }
}