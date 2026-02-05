using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.Models;
using RestControlAPI.Services;

namespace RestControlAPI.Controllers
{
    // Controllers/SubscriptionsController.cs
    [Route("api/[controller]")]
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
                DaysRemaining = s.EndDate.HasValue ?
                                (s.EndDate.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days : 0
            });

            return Ok(result);
        }

        // POST: api/subscriptions/notify/5
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
    }
}
