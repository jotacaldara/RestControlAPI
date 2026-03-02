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
            var reservation = await _context.Reservations
                .Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.ReservationId == dto.ReservationId);

            if (reservation == null)
                return NotFound("Reserva não encontrada.");
             
            if (reservation.Status != "Confirmada" || reservation.Status != "Confirmed")
            {
                return BadRequest("Você só pode avaliar após a ter confirmado a reserva.");
            }

            // Verifica se já avaliou
            bool alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.ReservationId == dto.ReservationId);

            if (alreadyReviewed)
                return BadRequest("Você já avaliou esta visita.");

            var review = new Review
            {
                ReservationId = reservation.ReservationId,
                RestaurantId = reservation.RestaurantId,
                UserId = reservation.CustomerId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.Now,
                Reply = null,
                RepliedAt = null
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Avaliação enviada com sucesso!" });
        }

        // GET: api/reviews/restaurant/5
        [HttpGet("restaurant/{restaurantId}")]
        public async Task<IActionResult> GetReviews(int restaurantId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.RestaurantId == restaurantId)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDTO
                {
                    ReviewId = r.Id, 
                    UserName = r.User != null ? r.User.FullName : "Usuário Anônimo",
                    Rating = (int)r.Rating,
                    Comment = r.Comment,
                    Date = r.CreatedAt.ToString(),
                    Reply = r.Reply,  
                    RepliedAt = r.RepliedAt  
                })
                .ToListAsync();

            return Ok(reviews);
        }

        // POST: api/reviews/{reviewId}/reply
        [HttpPost("{reviewId}/reply")]
        public async Task<IActionResult> ReplyToReview(int reviewId, [FromBody] ReplyReviewDTO dto)
        {
            var review = await _context.Reviews.FindAsync(reviewId);

            if (review == null)
                return NotFound("Review não encontrada.");

            if (!string.IsNullOrWhiteSpace(review.Reply))
                return BadRequest("Já existe uma resposta para esta review.");

            review.Reply = dto.Reply;
            review.RepliedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Resposta publicada com sucesso!" });
        }


    }
}
