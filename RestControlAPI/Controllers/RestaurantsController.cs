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
    public async Task<ActionResult<IEnumerable<RestaurantListDTO>>> GetRestaurants(string? city = null)
    {
        try
        {
            var query = _context.Restaurants
                .Include(r => r.RestaurantImages)
                .AsNoTracking() 
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(city))
            {
                var searchCity = city.Trim().ToUpper();
                query = query.Where(r => r.City != null && r.City.ToUpper().Contains(searchCity));
            }

            var result = await query.Select(r => new RestaurantListDTO
            {
                Id = r.RestaurantId,
                Name = r.Name,
                City = r.City ?? "Cidade não informada",
                Description = r.Description,
                ImageUrl = r.RestaurantImages.Select(i => i.ImageUrl).FirstOrDefault() ?? "default.jpg",
                AverageRating = r.Reviews.Any() ? (decimal)r.Reviews.Average(rev => rev.Rating) : 0,
                TotalReviews = r.Reviews.Count()
            }).ToListAsync();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno: {ex.Message}");
        }
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

        var reviews = await _context.Reviews.Where(r => r.RestaurantId == id).ToListAsync();
        double average = reviews.Any() ? reviews.Average(r => r.Rating) : 0;
        int total = reviews.Count;

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
            AverageRating = (decimal)average,
            TotalReviews = total,
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

    [HttpPost("deactivate/{id}")]
    public async Task<IActionResult> DeactivateRestaurant(int id)
    {
        var restaurant = await _context.Restaurants.FindAsync(id);
        if (restaurant == null) return NotFound();

        restaurant.IsActive = false; 
        await _context.SaveChangesAsync();
        return Ok();
    }

    // PUT para editar informações básicas do restaurante (Admin)
    [HttpPut("{id}")]
    public async Task<IActionResult> PutRestaurant(int id, RestaurantDetailDto dto)
    {
        if (id != dto.Id) return BadRequest();

        var restaurant = await _context.Restaurants.FindAsync(id);
        if (restaurant == null) return NotFound();

        // Atualiza apenas os campos básicos permitidos pelo Admin
        restaurant.Name = dto.Name;
        restaurant.Description = dto.Description;
        restaurant.Address = dto.Address;
        restaurant.City = dto.City;
        restaurant.Phone = dto.Phone;

        _context.Entry(restaurant).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            return Ok(true);
        }
        catch (Exception)
        {
            return StatusCode(500, "Erro ao atualizar no banco de dados.");
        }
    }
}