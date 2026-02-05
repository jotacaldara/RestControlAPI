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

        // UNIFICADO: Apenas um GET para "stats"
        // GET: api/admin/admindashboard/stats
        [HttpGet("stats")]
        public async Task<ActionResult<DashboardKpiDTO>> GetStats()
        {
            var stats = new DashboardKpiDTO
            {
                TotalRestaurants = await _context.Restaurants.CountAsync(),
                TotalReservations = await _context.Reservations.CountAsync(),
                // Soma os ganhos da tabela PlatformEarnings
                TotalRevenue = await _context.PlatformEarnings.SumAsync(e => e.Amount),
                // Lógica robusta para pendentes: false ou nulo
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
    }
}