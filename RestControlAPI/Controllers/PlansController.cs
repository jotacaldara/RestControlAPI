using Microsoft.AspNetCore.Mvc;
using global::RestControlAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace RestControlAPI.Controllers
{


    namespace RestControlAPI.Controllers
    {
        [Route("api/[controller]")]
        [ApiController]
        public class PlansController : ControllerBase
        {
            private readonly nextlayerapps_SampleDBContext _context;

            public PlansController(nextlayerapps_SampleDBContext context)
            {
                _context = context;
            }

            // GET: api/plans
            [HttpGet]
            public async Task<IActionResult> GetPlans()
            {
                var plans = await _context.Plans
                    .Select(p => new {
                        p.PlanId,
                        p.Name,
                        p.MonthlyPrice,
                        p.ReservationCommission
                    })
                    .ToListAsync();

                return Ok(plans);
            }

            // POST: api/plans/{id}
            [HttpPost("{id}")]
            public async Task<IActionResult> UpdatePlan(int id, [FromBody] Plan updatedPlan)
            {
                var plan = await _context.Plans.FindAsync(id);
                if (plan == null) return NotFound();

                // Atualiza apenas os valores financeiros
                plan.MonthlyPrice = updatedPlan.MonthlyPrice;
                plan.ReservationCommission = updatedPlan.ReservationCommission;

                try
                {
                    await _context.SaveChangesAsync();
                    return Ok(true);
                }
                catch (Exception)
                {
                    return StatusCode(500, false);
                }
            }
        }
    }
}
