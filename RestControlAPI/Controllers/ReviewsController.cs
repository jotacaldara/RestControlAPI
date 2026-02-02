using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

namespace RestControlAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;

        public ReviewsController(nextlayerapps_SampleDBContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> PostReview(CreateReviewDTO dto)
        {
            // 1. Busca a reserva
            var reservation = await _context.Reservations
                .Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.ReservationId == dto.ReservationId);

            if (reservation == null) return NotFound("Reserva não encontrada.");

            // 2. Validações de Segurança (Tipo The Fork)
            // Só pode avaliar se estiver Confirmada e a data já passou (já jantou)
            if (reservation.Status != "Confirmed" || reservation.ReservationDate > DateTime.Now)
            {
                return BadRequest("Você só pode avaliar após a data da reserva confirmada.");
            }

            // 3. Verifica se já não avaliou essa reserva antes
            bool alreadyReviewed = await _context.Reviews.AnyAsync(r => r.ReservationId == dto.ReservationId);
            if (alreadyReviewed) return BadRequest("Você já avaliou esta visita.");

            // 4. Salva a Review
            var review = new Review
            {
                ReservationId = reservation.ReservationId,
                RestaurantId = reservation.RestaurantId,
                UserId = reservation.CustomerId, // Pegando do objeto reserva
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.Now,
                
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Avaliação enviada com sucesso!" });
        }

        // Endpoint para pegar as reviews de um restaurante
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<IActionResult> GetReviews(int restaurantId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.RestaurantId == restaurantId)
                .Include(r => r.User)
                .Select(r => new ReviewDTO
                {
                    UserName = r.User.FullName, // Ou Username
                    Rating = r.Rating,
                    Comment = r.Comment,
                    Date = r.CreatedAt.ToString()
                })
                .ToListAsync();

            return Ok(reviews);
        }
    }
}
