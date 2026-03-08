using Microsoft.AspNetCore.Identity.Data;
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

    //  // GET: api/staff/position/{position}
    // Retorna funcionários por posição (1=Balcão, 2=Chefe, 3=Cozinha, 4=Empregado)
    [HttpGet("position/{position}")]
    public async Task<IActionResult> GetByPosition(string position)
    {
        var staff = await _context.Staff
            .Include(s => s.User)
                .ThenInclude(u => u.Role)
            .Where(s => s.Position == position && s.IsActive == true)
            .Select(s => new
            {
                UserId = s.User.UserId,
                Name = s.User.FullName,
                Email = s.User.Email,
                RoleName = s.Position // Retorna a Position como "RoleName" para compatibilidade
            })
            .ToListAsync();

        return Ok(staff);
    }

    // POST: api/staff/login
    // Recebe Email + Password (PIN de 6 dígitos) e devolve token + dados do utilizador
    // Reutiliza o LoginRequest que já existe nos teus DTOs (tem Email e Password)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest dto)
    {
        var staffMember = await _context.Staff
            .Include(s => s.User)
                .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(s => s.User.Email == dto.Email
                                   && s.User.PasswordHash == dto.Password
                                   && s.IsActive == true);

        if (staffMember == null)
            return Unauthorized("Credenciais inválidas ou funcionário inativo.");

        // Placeholder de token — substitui pelo teu gerador JWT real se aplicável
        var token = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(
                $"{staffMember.User.UserId}:{staffMember.User.Email}:{DateTime.UtcNow.Ticks}"));

        return Ok(new
        {
            Token = token,
            User = new
            {
                UserId = staffMember.User.UserId,
                Name = staffMember.User.FullName,
                Email = staffMember.User.Email,
                RoleName = staffMember.User.Role?.Name  // ✅ Corrigido: Name em vez de RoleName
            }
        });
    }

    // POST: api/staff/login-pin  (endpoint original mantido)Login usando o UserId e o PIN (que está em PasswordHash na tabela Users)
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