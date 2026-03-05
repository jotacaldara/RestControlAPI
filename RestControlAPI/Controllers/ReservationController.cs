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
        var restaurant = await _context.Restaurants
            .Include(r => r.Tables)  
            .FirstOrDefaultAsync(r => r.RestaurantId == dto.RestaurantId);

        if (restaurant == null)
            return NotFound("Restaurante não encontrado.");

        if (dto.ReservationDate <= DateTime.Now)
            return BadRequest("Não é possível fazer uma reserva para uma data/hora que já passou.");

        // Validar horário de funcionamento
        TimeSpan horaReserva = dto.ReservationDate.TimeOfDay;

        TimeOnly horaReservaOnly = TimeOnly.FromTimeSpan(horaReserva);

        if (horaReservaOnly < restaurant.OpeningTime || horaReservaOnly >= restaurant.ClosingTime)
        {
            return BadRequest(
                $"Este restaurante só aceita reservas entre " +
                $"{restaurant.OpeningTime:hh\\:mm} e {restaurant.ClosingTime:hh\\:mm}. " +
                $"A hora solicitada foi {horaReserva:hh\\:mm}.");
        }

        // Fallback caso o restaurante nao tenha mesas
        var mesasActivas = restaurant.Tables.Where(t => t.IsActive == true).ToList();

        int capacidadeMaxMesas;
        int capacidadeMaxLugares;

        if (mesasActivas.Any())
        {
            capacidadeMaxMesas = mesasActivas.Count;
            capacidadeMaxLugares = mesasActivas.Sum(t => t.Capacity);
        }
        else
        {
            // Fallback: valores declarados no registo do restaurante
            capacidadeMaxMesas = restaurant.MaxTables;
            capacidadeMaxLugares = restaurant.MaxSeats;
        }

        // reservas activas 1 hora do horário pedido
        DateTime janelaInicio = dto.ReservationDate.AddHours(-1);
        DateTime janelaFim = dto.ReservationDate.AddHours(1);

        var reservasNoSlot = await _context.Reservations
            .Where(r => r.RestaurantId == dto.RestaurantId
                     && r.Status != "Canceled"
                     && r.Status != "Cancelada"
                     && r.ReservationDate >= janelaInicio
                     && r.ReservationDate <= janelaFim)
            .ToListAsync();

        //Validar mesas disponíveis
        int mesasOcupadas = reservasNoSlot.Count;

        if (mesasOcupadas >= capacidadeMaxMesas)
        {
            return BadRequest(
                $"Não há mesas disponíveis para este horário. " +
                $"O restaurante tem {capacidadeMaxMesas} mesa(s) e todas estão ocupadas neste intervalo.");
        }

        //Validar lugares disponíveis
        int lugaresOcupados = reservasNoSlot.Sum(r => r.NumberOfPeople);
        int lugaresDisponiveis = capacidadeMaxLugares - lugaresOcupados;

        if (dto.NumberOfPeople > lugaresDisponiveis)
        {
            if (lugaresDisponiveis <= 0)
                return BadRequest(
                    $"O restaurante atingiu a capacidade máxima ({capacidadeMaxLugares} pessoas) " +
                    $"para este horário. Por favor escolha outro horário.");
            else
                return BadRequest(
                    $"Apenas {lugaresDisponiveis} lugar(es) disponível(eis) para este horário. " +
                    $"Pedido: {dto.NumberOfPeople} pessoa(s).");
        }

        //Criar reserva
        var reservation = new Reservation
        {
            RestaurantId = dto.RestaurantId,
            CustomerId = dto.UserId,
            ReservationDate = dto.ReservationDate,
            NumberOfPeople = dto.NumberOfPeople,
            Status = "Pending",
            CreatedAt = DateTime.Now
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            reservationId = reservation.ReservationId,
            message = "Reserva criada com sucesso!",
            mesasDisponiveis = capacidadeMaxMesas - mesasOcupadas - 1,
            lugaresDisponiveis = lugaresDisponiveis - dto.NumberOfPeople
        });
    }

    [HttpGet("availability")]
    public async Task<IActionResult> CheckAvailability(int restaurantId, DateTime date)
    {
        var restaurant = await _context.Restaurants
            .Include(r => r.Tables)
            .FirstOrDefaultAsync(r => r.RestaurantId == restaurantId);

        if (restaurant == null)
            return NotFound("Restaurante não encontrado.");

        // Fora do horário?
        TimeSpan horaSlot = date.TimeOfDay;
        TimeOnly horaSlotOnly = TimeOnly.FromTimeSpan(horaSlot);
        bool dentroDoHorario = horaSlotOnly >= restaurant.OpeningTime && horaSlotOnly < restaurant.ClosingTime;

        if (!dentroDoHorario)
        {
            return Ok(new
            {
                available = false,
                reason = "Fora do horário de funcionamento",
                openingTime = restaurant.OpeningTime.ToString(@"hh\:mm"),
                closingTime = restaurant.ClosingTime.ToString(@"hh\:mm"),
                tablesAvailable = 0,
                seatsAvailable = 0
            });
        }

        // Capacidade real ou fallback
        var mesasActivas = restaurant.Tables.Where(t => t.IsActive == true).ToList();
        int capMesas = mesasActivas.Any() ? mesasActivas.Count : restaurant.MaxTables;
        int capLugares = mesasActivas.Any() ? mesasActivas.Sum(t => t.Capacity) : restaurant.MaxSeats;

        // Reservas no slot
        DateTime janelaInicio = date.AddHours(-1);
        DateTime janelaFim = date.AddHours(1);

        var reservasNoSlot = await _context.Reservations
            .Where(r => r.RestaurantId == restaurantId
                     && r.Status != "Canceled"
                     && r.Status != "Cancelada"
                     && r.ReservationDate >= janelaInicio
                     && r.ReservationDate <= janelaFim)
            .ToListAsync();

        int mesasOcupadas = reservasNoSlot.Count;
        int lugaresOcupados = reservasNoSlot.Sum(r => r.NumberOfPeople);
        int mesasDisp = Math.Max(0, capMesas - mesasOcupadas);
        int lugaresDisp = Math.Max(0, capLugares - lugaresOcupados);

        return Ok(new
        {
            available = mesasDisp > 0 && lugaresDisp > 0,
            openingTime = restaurant.OpeningTime.ToString(@"hh\:mm"),
            closingTime = restaurant.ClosingTime.ToString(@"hh\:mm"),
            maxTables = capMesas,
            maxSeats = capLugares,
            tablesAvailable = mesasDisp,
            seatsAvailable = lugaresDisp,
            usesRealTables = mesasActivas.Any() 
        });
    }

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

    [HttpPut("owner/reservations/{id}/status")]
    public async Task<IActionResult> UpdateReservationStatus(int id, [FromBody] UpdateStatusDTO dto)
    {
        var reservation = await _context.Reservations.FindAsync(id);

        if (reservation == null)
            return NotFound("Reserva não encontrada.");

        if (reservation.Status == "Canceled" || reservation.Status == "Confirmed")
            return BadRequest("Não é possível alterar o status de uma reserva já finalizada ou cancelada.");

        reservation.Status = dto.Status;
        _context.Entry(reservation).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Reservations.Any(e => e.ReservationId == id)) return NotFound();
            throw;
        }

        return NoContent();
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null) return NotFound();

        reservation.Status = "Canceled";
        _context.Reservations.Update(reservation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Reserva cancelada." });
    }


    [HttpPost("complete/{id}")]
    public async Task<IActionResult> CompleteReservation(int id, [FromQuery] decimal finalAmount)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null) return NotFound("Reserva não encontrada.");

        if (reservation.Status == "Confirmed")
            return BadRequest("Esta reserva já foi finalizada.");

        var subscription = await _context.RestaurantSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.RestaurantId == reservation.RestaurantId && s.IsActive == true)
            .FirstOrDefaultAsync();

        decimal commissionRate = subscription?.Plan?.ReservationCommission ?? 10m;
        decimal platformCommission = finalAmount * (commissionRate / 100m);

        reservation.Status = "Confirmed";

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