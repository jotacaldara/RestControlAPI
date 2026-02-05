using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class ReservationsController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public ReservationsController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateReservation(CreateReservationDTO dto)
    {
        var restaurant = await _context.Restaurants.FindAsync(dto.RestaurantId);
        if (restaurant == null) return NotFound("Restaurante não encontrado.");

        
        var existingReservations = await _context.Reservations
            .Where(r => r.RestaurantId == dto.RestaurantId
                   && r.ReservationDate == dto.ReservationDate
                   && r.Status != "Cancelled")
            .CountAsync();


        if (existingReservations >= 20)
        {
            return BadRequest("Desculpe, não há mesas disponíveis para este horário.");
        }

        // 3. Criar a Reserva
        var reservation = new Reservation
        {
            RestaurantId = dto.RestaurantId,
            CustomerId = dto.UserId,
            ReservationDate = dto.ReservationDate,
            NumberOfPeople = dto.NumberOfPeople,
            Status = "Confirmed",
            CreatedAt = DateTime.Now
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return Ok(new { reservationId = reservation.ReservationId, message = "Reserva confirmada!" });
    }

    // GET: api/reservations/user/5 (Minhas Reservas)
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserReservations(int userId)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Restaurant)
            .Where(r => r.CustomerId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .Select(r => new
            {
                r.ReservationId,
                RestaurantName = r.Restaurant.Name,
                Date = r.ReservationDate,
                People = r.NumberOfPeople,
                Status = r.Status,
                IsReviewed = _context.Reviews.Any(rev => rev.ReservationId == r.ReservationId)
            })
            .ToListAsync();

        return Ok(reservations);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null) return NotFound();

        reservation.Status = "Canceled";

        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();

        return Ok();
    }

    // POST: api/reservations/complete/{id}
    [HttpPost("complete/{id}")]
    public async Task<IActionResult> CompleteReservation(int id, [FromQuery] decimal finalAmount)
    {
        // 1. Encontra a reserva
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null) return NotFound("Reserva não encontrada.");

        if (reservation.Status == "Completed")
            return BadRequest("Esta reserva já foi finalizada.");

        reservation.Status = "Completed";
        decimal platformCommission = finalAmount * 0.10m; // 10%

        var earning = new PlatformEarning
        {
            RestaurantId = reservation.RestaurantId,
            ReservationId = reservation.ReservationId,
            Amount = platformCommission,
            CreatedAt = DateTime.UtcNow
        };

        _context.PlatformEarnings.Add(earning);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Reserva concluída com sucesso.",
            PlatformProfit = platformCommission
        });
    }
}