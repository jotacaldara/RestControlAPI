using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class TablesController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public TablesController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // GET: api/tables/restaurant/5
    // Retorna todas as mesas e calcula o status (Livre, Ocupada, Reservada)
    [HttpGet("restaurant/{restaurantId}")]
    public async Task<IActionResult> GetTableMap(int restaurantId)
    {
        // Busca as mesas físicas
        var tables = await _context.Tables
            .Where(t => t.RestaurantId == restaurantId && t.IsActive == true)
            .ToListAsync();

        // Busca pedidos ABERTOS para saber quem está ocupado
        var activeOrders = await _context.Orders
            .Where(o => o.RestaurantId == restaurantId && o.Status != "Paid" && o.Status != "Cancelled" && o.TableId != null)
            .Select(o => o.TableId)
            .ToListAsync();

        // Busca reservas para HOJE que ainda não chegaram
        var today = DateTime.Today;
        var reservedTables = await _context.ReservationTables
            .Include(rt => rt.Reservation)
            .Where(rt => rt.Reservation.RestaurantId == restaurantId
                         && rt.Reservation.ReservationDate.Date == today
                         && rt.Reservation.Status == "Confirmed")
            .Select(rt => rt.TableId)
            .ToListAsync();

        var map = tables.Select(t => new
        {
            t.TableId,
            t.TableNumber,
            t.Capacity,
            // Lógica de Status para o WinForms pintar a cor
            Status = activeOrders.Contains(t.TableId) ? "Occupied" :
                     reservedTables.Contains(t.TableId) ? "Reserved" : "Free"
        });

        return Ok(map);
    }
}