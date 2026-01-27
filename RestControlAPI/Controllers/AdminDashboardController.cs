using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

[Route("api/admin/[controller]")] // Rota prefixada com /admin
[ApiController]
public class AdminDashboardController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public AdminDashboardController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // GET: api/admin/admindashboard/stats
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardKpiDTO>> GetStats()
    {
        // Lógica simples de agregação
        var stats = new DashboardKpiDTO
        {
            TotalRestaurants = await _context.Restaurants.CountAsync(),
            TotalReservations = await _context.Reservations.CountAsync(),
            // Soma os ganhos da plataforma (tabela PlatformEarnings)
            TotalRevenue = await _context.PlatformEarnings.SumAsync(e => e.Amount),
            // Conta restaurantes inativos (pendentes de aprovação)
            PendingApprovals = await _context.Restaurants.CountAsync(r => r.IsActive == false)
        };

        return Ok(stats);
    }

    // GET: api/admin/admindashboard/pending-restaurants
    [HttpGet("pending-restaurants")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await _context.Restaurants
            .Include(r => r.Owner) // Include User (Dono)
            .Where(r => r.IsActive == false) // Assumindo que false = pendente
            .Select(r => new AdminRestaurantDTO
            {
                Id = r.RestaurantId,
                Name = r.Name,
                OwnerName = r.Owner.FullName, // Precisa ajustar no Model Context se a navegação chamar 'Owner'
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

        // Opcional: Enviar email para o dono avisando

        return Ok(new { message = "Restaurante aprovado e ativo na plataforma." });
    }
}