using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;
using RestControlAPI.Services;
using System.Globalization;

namespace RestControlAPI.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class SubscriptionsController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;
        private readonly IEmailService _emailService;

        public SubscriptionsController(nextlayerapps_SampleDBContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet("expiring")]
        public async Task<IActionResult> GetExpiringSubscriptions()
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var warningDate = today.AddDays(5);

            var expiring = await _context.RestaurantSubscriptions
                .Include(s => s.Restaurant)
                .Include(s => s.Plan)
                .Where(s => s.IsActive == true && s.EndDate <= warningDate)
                .ToListAsync();

            var result = expiring.Select(s => new
            {
                s.SubscriptionId,
                RestaurantName = s.Restaurant.Name,
                PlanName = s.Plan.Name,
                EndDate = s.EndDate,
                DaysRemaining = s.EndDate.HasValue
                    ? (s.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days
                    : 0
            });

            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllSubscriptions()
        {
            var subs = await _context.RestaurantSubscriptions
                .Include(s => s.Restaurant)
                .Include(s => s.Plan)
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.EndDate)
                .ToListAsync();

            var result = subs.Select(s => new
            {
                s.SubscriptionId,
                s.RestaurantId,
                RestaurantName = s.Restaurant.Name,
                s.PlanId,
                PlanName = s.Plan.Name,
                MonthlyPrice = s.Plan.MonthlyPrice,
                Commission = s.Plan.ReservationCommission,
                s.StartDate,
                s.EndDate,
                s.IsActive,
                DaysRemaining = s.EndDate.HasValue
                    ? (s.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days
                    : 9999,
                IsExpiring = s.EndDate.HasValue
                    && (s.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days <= 5
                    && (s.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days >= 0
            });

            return Ok(result);
        }

        [HttpPost("renew/{subscriptionId}")]
        public async Task<IActionResult> RenewSubscription(int subscriptionId)
        {
            var sub = await _context.RestaurantSubscriptions
                .Include(s => s.Restaurant).ThenInclude(r => r.Owner)
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (sub == null)
                return NotFound(new { message = "Subscrição não encontrada." });

            // Renovar a partir de hoje ou da data de fim (se ainda no futuro)
            var baseDate = sub.EndDate.HasValue
                && sub.EndDate.Value > DateOnly.FromDateTime(DateTime.Today)
                    ? sub.EndDate.Value
                    : DateOnly.FromDateTime(DateTime.Today);

            sub.EndDate = baseDate.AddDays(30);
            sub.IsActive = true;

            await _context.SaveChangesAsync();

            // Enviar email de confirmação (opcional, não falha se não configurado)
            try
            {
                await _emailService.SendPaymentReminderAsync(
                    sub.Restaurant.Owner.Email,
                    sub.Restaurant.Name,
                    sub.Plan.Name,
                    sub.Plan.MonthlyPrice,
                    sub.EndDate.Value
                );
            }
            catch { /* Email é opcional */ }

            return Ok(new
            {
                message = "Subscrição renovada com sucesso por 30 dias!",
                newEndDate = sub.EndDate
            });
        }

        [HttpPost("notify/{subscriptionId}")]
        public async Task<IActionResult> SendNotification(int subscriptionId)
        {
            var sub = await _context.RestaurantSubscriptions
                .Include(s => s.Restaurant).ThenInclude(r => r.Owner)
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);

            if (sub == null) return NotFound();

            try
            {
                await _emailService.SendPaymentReminderAsync(
                    sub.Restaurant.Owner.Email,
                    sub.Restaurant.Name,
                    sub.Plan.Name,
                    sub.Plan.MonthlyPrice,
                    sub.EndDate ?? DateOnly.FromDateTime(DateTime.Now)
                );
                return Ok(new { Message = "Email enviado com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao enviar email: {ex.Message}");
            }
        }

        
        [HttpGet("earnings")]
        public async Task<IActionResult> GetPlatformEarnings()
        {
            var totalReservationCommissions = await _context.PlatformEarnings
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

          
            // Conta meses desde inicio × preço do plano
            var subscriptions = await _context.RestaurantSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.IsActive == true && s.Plan != null)
                .ToListAsync();

            decimal totalSubscriptionRevenue = 0m;
            foreach (var s in subscriptions)
            {
                if (s.Plan == null) continue;

                var start = s.StartDate.ToDateTime(TimeOnly.MinValue);
                var end = s.EndDate.HasValue
                    ? s.EndDate.Value.ToDateTime(TimeOnly.MinValue)
                    : DateTime.Today;

                var months = Math.Max(
                    ((end.Year - start.Year) * 12) + end.Month - start.Month,
                    1
                );
                totalSubscriptionRevenue += s.Plan.MonthlyPrice * months;
            }

            var totalEarnings = totalReservationCommissions + totalSubscriptionRevenue;

            var byRestaurantRaw = await _context.PlatformEarnings
                .Include(e => e.Restaurant)
                .ToListAsync();

            var byRestaurant = byRestaurantRaw
                .GroupBy(e => e.Restaurant?.Name ?? "Desconhecido")
                .Select(g => new RestaurantEarningItemDTO
                {
                    RestaurantName = g.Key,
                    TotalReservations = g.Count(),
                    TotalEarnings = g.Sum(e => e.Amount)
                })
                .OrderByDescending(x => x.TotalEarnings)
                .ToList();

            //ultimos 6 meses de ganho
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var rawMonthly = await _context.PlatformEarnings
                .Where(e => e.CreatedAt != null && e.CreatedAt >= sixMonthsAgo)
                .ToListAsync();

            var byMonth = rawMonthly
                .GroupBy(e => new { e.CreatedAt!.Value.Year, e.CreatedAt.Value.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyEarningItemDTO
                {
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1)
                        .ToString("MMM yyyy", new CultureInfo("pt-PT")),
                    Total = g.Sum(e => e.Amount)
                })
                .ToList();

            return Ok(new AdminEarningsDTO
            {
                TotalEarnings = totalEarnings,
                TotalReservationCommissions = totalReservationCommissions,
                TotalSubscriptionRevenue = totalSubscriptionRevenue,
                ByRestaurant = byRestaurant,
                ByMonth = byMonth
            });
        }
    }
}
