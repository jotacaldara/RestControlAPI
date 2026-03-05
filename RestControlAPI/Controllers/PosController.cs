using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class PosController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public PosController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // ══════════════════════════════════════════════════════════════════
    // GET: api/pos/categories/{restaurantId}
    // Lista categorias do restaurante que têm produtos disponíveis
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("categories/{restaurantId}")]
    public async Task<IActionResult> GetCategories(int restaurantId)
    {
        var categories = await _context.Categories
            .Where(c => c.RestaurantId == restaurantId
                     && c.Products.Any(p => p.IsAvailable == true))
            .Select(c => new
            {
                c.CategoryId,
                c.Name
            })
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(categories);
    }

    // ══════════════════════════════════════════════════════════════════
    // GET: api/pos/products/{restaurantId}?categoryId=5
    // Lista produtos disponíveis, com filtro opcional por categoria
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("products/{restaurantId}")]
    public async Task<IActionResult> GetProducts(int restaurantId, [FromQuery] int? categoryId = null)
    {
        var query = _context.Products
            .Where(p => p.RestaurantId == restaurantId && p.IsAvailable == true)
            .AsQueryable();

        if (categoryId.HasValue && categoryId > 0)
            query = query.Where(p => p.CategoryId == categoryId);

        var products = await query
            .Select(p => new
            {
                p.ProductId,
                p.Name,
                p.Description,
                p.Price,
                p.IsAvailable,
                p.CategoryId,
                Category = p.Category != null ? p.Category.Name : null
            })
            .OrderBy(p => p.Name)
            .ToListAsync();

        return Ok(products);
    }

    // ══════════════════════════════════════════════════════════════════
    // POST: api/pos/remove-item
    // Remove completamente um produto do pedido
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("remove-item")]
    public async Task<IActionResult> RemoveItem([FromBody] RemoveItemDTO dto)
    {
        var item = await _context.OrderItems
            .FirstOrDefaultAsync(i => i.OrderId == dto.OrderId && i.ProductId == dto.ProductId);

        if (item == null)
            return NotFound(new { message = "Item não encontrado no pedido." });

        _context.OrderItems.Remove(item);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Item removido com sucesso." });
    }

    // ══════════════════════════════════════════════════════════════════
    // POST: api/pos/order-item/increment
    // Incrementa a quantidade de um item já existente no pedido
    // ══════════════════════════════════════════════════════════════════
    [HttpPost("order-item/increment")]
    public async Task<IActionResult> IncrementItem([FromBody] IncrementItemDTO dto)
    {
        var item = await _context.OrderItems
            .FirstOrDefaultAsync(i => i.OrderId == dto.OrderId && i.ProductId == dto.ProductId);

        if (item == null)
        {
            // Se não existe, cria com quantidade 1
            var product = await _context.Products.FindAsync(dto.ProductId);
            if (product == null) return NotFound("Produto não encontrado.");

            _context.OrderItems.Add(new OrderItem
            {
                OrderId   = dto.OrderId,
                ProductId = dto.ProductId,
                Quantity  = 1,
                UnitPrice = product.Price
            });
        }
        else
        {
            item.Quantity++;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Quantidade actualizada." });
    }

    // ══════════════════════════════════════════════════════════════════
    // GET: api/pos/active-order/{tableId}
    // Verifica se existe pedido aberto para uma mesa
    // ══════════════════════════════════════════════════════════════════
    [HttpGet("active-order/{tableId}")]
    public async Task<IActionResult> GetActiveOrder(int tableId)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.TableId == tableId
                     && o.Status != "Paid"
                     && o.Status != "Cancelled")
            .OrderByDescending(o => o.OrderDate)
            .FirstOrDefaultAsync();

        if (order == null)
            return Ok(null);

        return Ok(new
        {
            order.OrderId,
            order.Status,
            order.TableId,
            Total = order.OrderItems.Sum(i => i.Quantity * i.UnitPrice),
            Items = order.OrderItems.Select(i => new
            {
                i.OrderItemId,
                i.ProductId,
                ProductName = i.Product.Name,
                i.Quantity,
                i.UnitPrice,
                SubTotal = i.Quantity * i.UnitPrice
            })
        });
    }
}

// ════════════════════════════════════════════════════════════════════
// DTOs do POS
// ════════════════════════════════════════════════════════════════════
public class RemoveItemDTO
{
    public int OrderId   { get; set; }
    public int ProductId { get; set; }
}

public class IncrementItemDTO
{
    public int OrderId   { get; set; }
    public int ProductId { get; set; }
}
