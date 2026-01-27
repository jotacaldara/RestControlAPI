using Microsoft.AspNetCore.Mvc;
using RestControlAPI.Models;
using RestControlAPI.DTOs;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
public class KitchenController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public KitchenController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // GET: api/kitchen/orders (Lista pedidos para preparar)
    [HttpGet("orders")]
    public async Task<IActionResult> GetKitchenOrders(int restaurantId)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.RestaurantId == restaurantId
                   && (o.Status == "Pending" || o.Status == "Preparing"))
            .OrderBy(o => o.OrderDate)
            .Select(o => new {
                o.OrderId,
                Table = o.Table.TableNumber,
                Time = o.OrderDate,
                Status = o.Status,
                Items = o.OrderItems.Select(oi => new {
                    ProductName = oi.Product.Name,
                    Qty = oi.Quantity
                })
            })
            .ToListAsync();

        return Ok(orders);
    }

    // PUT: api/kitchen/orders/{id}/status (Mudar status: de Pending -> Preparing -> Served)
    [HttpPut("orders/{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        // Validação simples de estados permitidos
        var validStatuses = new[] { "Pending", "Preparing", "Served" };
        if (!validStatuses.Contains(newStatus)) return BadRequest("Status inválido");

        order.Status = newStatus;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"Pedido {id} atualizado para {newStatus}" });
    }
}