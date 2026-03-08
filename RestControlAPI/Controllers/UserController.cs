using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;

namespace RestControlAPI.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly nextlayerapps_SampleDBContext _context;

        public UserController(nextlayerapps_SampleDBContext context)
        {
            _context = context;
        }

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
                return StatusCode(500, $"Erro na API: {ex.Message}");
            }
        }

   
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("Este e-mail já está em uso.");

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == dto.RoleId.ToString());
            if (role == null) return BadRequest("Role inválida.");

            var newUser = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PasswordHash = dto.Password, 
                RoleId = role.RoleId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Utilizador criado com sucesso!" });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null) return NotFound();

            return Ok(new UserDTO
            {
                Id = user.UserId,
                Name = user.FullName,
                Email = user.Email,
                Role = user.Role?.Name ?? "Utilizador",
                IsActive = user.IsActive ?? false
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserDTO dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (id != dto.Id) return BadRequest();

            user.FullName = dto.Name;
            user.Email = dto.Email;
            user.IsActive = dto.IsActive;

            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/user/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.Restaurants) 
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null) return NotFound();

            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception)
            {
                user.IsActive = false;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Utilizador desativado por conter histórico no sistema." });
            }
        }
    }
}
