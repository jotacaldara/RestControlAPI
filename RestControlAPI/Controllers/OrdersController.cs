using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public OrdersController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // 1. Abrir um novo pedido (Ocupar uma mesa)
    [HttpPost("create")]
    public async Task<IActionResult> CreateOrder(CreateOrderDTO dto)
    {
        // Verifica se a mesa já está ocupada
        if (dto.TableId.HasValue)
        {
            var activeOrder = await _context.Orders
                .AnyAsync(o => o.TableId == dto.TableId && o.Status != "Paid" && o.Status != "Cancelled");

            if (activeOrder) return BadRequest("Esta mesa já tem um pedido aberto.");
        }

        var order = new Order
        {
            RestaurantId = dto.RestaurantId,
            TableId = dto.TableId,
            ReservationId = dto.ReservationId,
            StaffId = dto.StaffId,
            Status = "Pending", // Status inicial
            OrderDate = DateTime.Now
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return Ok(new { orderId = order.OrderId, message = "Mesa aberta com sucesso." });
    }

    // 2. Adicionar Item ao Pedido
    [HttpPost("add-item")]
    public async Task<IActionResult> AddItem(AddOrderItemDTO dto)
    {
        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null) return NotFound("Produto não encontrado");

        var item = new OrderItem
        {
            OrderId = dto.OrderId,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            UnitPrice = product.Price // Grava o preço do momento
        };

        _context.OrderItems.Add(item);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Item adicionado" });
    }

    // 3. Detalhes do Pedido (Para ver na tela do Garçom/Caixa)
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDetailDTO>> GetOrder(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Staff)
            .Include(o => o.Table)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();

        var dto = new OrderDetailDTO
        {
            OrderId = order.OrderId,
            Status = order.Status,
            TableNumber = order.Table != null ? order.Table.TableNumber : 0,
            WaiterName = order.Staff != null ? order.Staff.User.FullName : "N/A", // Requer Include User no Staff
            Items = order.OrderItems.Select(oi => new OrderItemDTO
            {
                OrderItemId = oi.OrderItemId,
                ProductName = oi.Product.Name,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice
            }).ToList()
        };

        dto.TotalAmount = dto.Items.Sum(i => i.SubTotal);

        return Ok(dto);
    }

    // 4. Fechar Conta (Pagamento)
    [HttpPost("{id}/pay")]
    public async Task<IActionResult> PayOrder(int id, [FromBody] string paymentMethod) // Cash, Card
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();
        if (order.Status == "Paid") return BadRequest("Pedido já está pago.");

        decimal total = order.OrderItems.Sum(i => i.Quantity * i.UnitPrice);

        // Cria registro de pagamento
        var payment = new Payment
        {
            OrderId = id,
            Amount = total,
            PaymentMethod = paymentMethod,
            Status = "Completed",
            PaymentDate = DateTime.Now
        };

        order.Status = "Paid"; // Libera a mesa logicamente

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Pagamento processado e mesa liberada." });
    }
}