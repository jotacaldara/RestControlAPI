using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

[Route("api/[controller]")]
[ApiController]
public class StaffController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;

    public StaffController(nextlayerapps_SampleDBContext context)
    {
        _context = context;
    }

    // Login usando o UserId e o PIN (que está em PasswordHash na tabela Users)
    [HttpPost("login-pin")]
    public async Task<IActionResult> LoginPin([FromBody] StaffLoginDTO dto)
    {
        // Buscamos na tabela Staff, incluindo os dados do Usuário relacionado
        var staffMember = await _context.Staff
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RestaurantId == dto.RestaurantId
                                 && s.User.PasswordHash == dto.Pin
                                 && s.IsActive == true);

        if (staffMember == null)
        {
            return Unauthorized("PIN inválido ou funcionário inativo neste restaurante.");
        }

        return Ok(new
        {
            StaffId = staffMember.StaffId,
            FullName = staffMember.User.FullName,
            Position = staffMember.Position
        });
    }

    // Busca o desempenho (Ordens) vinculadas ao StaffId
    [HttpGet("{staffId}/performance")]
    public async Task<IActionResult> GetStaffPerformance(int staffId)
    {
        var staff = await _context.Staff
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffId == staffId);

        if (staff == null) return NotFound();

        // No seu banco, a tabela Orders tem a FK StaffId. Vamos contar as ordens pagas.
        var orders = await _context.Orders
            .Where(o => o.StaffId == staffId && o.Status == "Paid")
            .ToListAsync();

        return Ok(new
        {
            StaffName = staff.User.FullName,
            Position = staff.Position,
            TotalOrders = orders.Count,
            LastOrderDate = orders.OrderByDescending(o => o.OrderDate).FirstOrDefault()?.OrderDate
        });
    }
}