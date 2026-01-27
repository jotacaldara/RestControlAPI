using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class RestaurantsController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public RestaurantsController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // GET: api/restaurants?city=Lisboa
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RestaurantListDto>>> GetRestaurants(string? city = null)
    {
        var query = _context.Restaurants
            .Include(r => r.RestaurantImages)
            .AsQueryable();

        if (!string.IsNullOrEmpty(city))
        {
            query = query.Where(r => r.City.Contains(city));
        }

        var result = await query.Select(r => new RestaurantListDto
        {
            Id = r.RestaurantId,
            Name = r.Name,
            City = r.City,
            Description = r.Description,
            ImageUrl = r.RestaurantImages.Any() ? r.RestaurantImages.First().ImageUrl : "default.jpg"
        }).ToListAsync();

        return Ok(result);
    }

    // GET: api/restaurants/5 (Detalhes com Menu)
    [HttpGet("{id}")]
    public async Task<ActionResult<RestaurantDetailDto>> GetRestaurantDetails(int id)
    {
        var restaurant = await _context.Restaurants
            .Include(r => r.RestaurantImages)
            .Include(r => r.Categories) // Categorias do Menu
                .ThenInclude(c => c.Products) // Produtos dentro das Categorias
            .FirstOrDefaultAsync(r => r.RestaurantId == id);

        if (restaurant == null) return NotFound();

        // Mapeamento manual para o DTO
        var dto = new RestaurantDetailDto
        {
            Id = restaurant.RestaurantId,
            Name = restaurant.Name,
            Description = restaurant.Description,
            Address = restaurant.Address,
            City = restaurant.City,
            Phone = restaurant.Phone,
            Images = restaurant.RestaurantImages.Select(i => i.ImageUrl).ToList(),
            MenuCategories = restaurant.Categories.Select(c => new CategoryDTO
            {
                Name = c.Name,
                Products = c.Products.Select(p => new ProductDTO
                {
                    Id = p.ProductId,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price
                }).ToList()
            }).ToList()
        };

        return Ok(dto);
    }
}