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
                   && r.Status != "Cancelada")
            .CountAsync();

        if (existingReservations >= 20)
            return BadRequest("Desculpe, não há mesas disponíveis para este horário.");

        var reservation = new Reservation
        {
            RestaurantId = dto.RestaurantId,
            CustomerId = dto.UserId,
            ReservationDate = dto.ReservationDate,
            NumberOfPeople = dto.NumberOfPeople,
            Status = "Pendente",
            CreatedAt = DateTime.Now
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return Ok(new { reservationId = reservation.ReservationId, message = "Reserva criada com sucesso!" });
    }

    // GET: api/reservations/user/5
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

    // DELETE: api/reservations/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null) return NotFound();

        reservation.Status = "Cancelada";
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Reserva cancelada." });
    }

    // =====================================================================
    // POST: api/reservations/complete/{id}?finalAmount=100
    // ✅ CORRIGIDO: usa comissão DO PLANO do restaurante, não 10% fixo
    // =====================================================================
    [HttpPost("complete/{id}")]
    public async Task<IActionResult> CompleteReservation(int id, [FromQuery] decimal finalAmount)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null) return NotFound("Reserva não encontrada.");

        if (reservation.Status == "Concluída")
            return BadRequest("Esta reserva já foi finalizada.");

        // ✅ Buscar comissão do plano ativo do restaurante
        var subscription = await _context.RestaurantSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.RestaurantId == reservation.RestaurantId && s.IsActive == true)
            .FirstOrDefaultAsync();

        // Se não tiver plano ativo, usa 10% como fallback
        decimal commissionRate = subscription?.Plan?.ReservationCommission ?? 10m;
        decimal platformCommission = finalAmount * (commissionRate / 100m);

        reservation.Status = "Concluída";

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
            FinalAmount = finalAmount,
            CommissionRate = commissionRate,
            PlatformProfit = platformCommission,
            RestaurantNet = finalAmount - platformCommission
        });
    }
}