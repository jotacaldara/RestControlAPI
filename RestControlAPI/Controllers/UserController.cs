using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

namespace RestControlAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;

        public UserController(nextlayerapps_SampleDBContext context)
        {
            _context = context;
        }

        // Na API - Controllers/UsersController.cs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.Role) //
                    .Select(u => new
                    {
                        Id = u.UserId,
                        Name = u.FullName ?? "Sem Nome",
                        Email = u.Email ?? "Sem Email",
                        Role = u.Role != null ? u.Role.Name : "Utilizador",
                        IsActive = u.IsActive
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                // Se houver erro na DB, a API avisará
                return StatusCode(500, $"Erro na API: {ex.Message}");
            }
        }

        // POST: api/users (Criação via Registo)
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("Este e-mail já está em uso.");

            // Busca a Role correspondente (ex: 1 para Admin, 2 para Client)
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == dto.RoleId.ToString());
            if (role == null) return BadRequest("Role inválida.");

            var newUser = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PasswordHash = dto.Password, // Nota: Use hashing em produção
                RoleId = role.RoleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync(); // Método SaveAsync do seu DbContext

            return Ok(new { Message = "Utilizador criado com sucesso!" });
        }
    }
}
