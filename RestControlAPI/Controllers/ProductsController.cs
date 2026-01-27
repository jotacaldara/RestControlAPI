using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public ProductsController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // POST: api/products (Criar novo prato)
    [HttpPost]
    public async Task<IActionResult> CreateProduct(ProductCreateDTO dto)
    {
        var product = new Product
        {
            RestaurantId = dto.RestaurantId,
            CategoryId = dto.CategoryId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            IsAvailable = dto.IsAvailable
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return Ok(new { productId = product.ProductId, message = "Produto criado com sucesso!" });
    }

    // PUT: api/products/{id}/toggle (Ativar/Desativar estoque)
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleAvailability(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        // Inverte o status atual
        product.IsAvailable = !(product.IsAvailable ?? true);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            productId = id,
            status = product.IsAvailable,
            message = product.IsAvailable == true ? "Produto disponível" : "Produto indisponível"
        });
    }

    // GET: api/products/restaurant/{id} (Listar menu para o Gerente)
    [HttpGet("restaurant/{restaurantId}")]
    public async Task<IActionResult> GetMenu(int restaurantId)
    {
        var menu = await _context.Products
            .Where(p => p.RestaurantId == restaurantId)
            .Select(p => new {
                p.ProductId,
                p.Name,
                p.Price,
                p.IsAvailable,
                Category = p.Category.Name
            }).ToListAsync();

        return Ok(menu);
    }
}