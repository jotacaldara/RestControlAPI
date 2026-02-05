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

        // GET: api/user/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        // PUT: api/user/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, User user)
        {
            if (id != user.UserId) return BadRequest();

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(e => e.UserId == id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/user/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            
            _context.Users.Remove(user);

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
