using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestControlAPI.DTOs;
using RestControlAPI.Models;
using RestControlAPI.Services;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly nextlayerapps_SampleDBContext _context;
    private readonly JwtTokenService _jwtTokenService;


    public AuthController(nextlayerapps_SampleDBContext context, JwtTokenService jwtTokenService)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email já cadastrado.");

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Phone = dto.Phone,
            PasswordHash = dto.Password, 
            RoleId = 2, 
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Usuário criado com sucesso!" });
    }


    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginDto request)
    {
        try
        {
            // 1. Validar entrada
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "E-mail e senha são obrigatórios." });
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return Unauthorized(new { message = "E-mail ou senha inválidos." });
            }

            // 3. Verificar senha
            bool senhaValida = VerificarSenha(request.Password, user.PasswordHash);

            if (!senhaValida)
            {
                return Unauthorized(new { message = "E-mail ou senha inválidos." });
            }

            // 4. Se for Owner, buscar o RestaurantId
            int? restaurantId = null;

            if (user.Role != null && user.Role.Name == "Owner")
            {
                var restaurant = await _context.Restaurants
                    .Where(r => r.OwnerId == user.UserId)
                    .Select(r => r.RestaurantId)
                    .FirstOrDefaultAsync();

                // Se não encontrar restaurante, restaurant será 0 
                restaurantId = restaurant == 0 ? null : restaurant;
            }

            // 5. Gerar o Token JWT
            string token = _jwtTokenService.GenerateToken(user, restaurantId);

            // 6. Retornar resposta
            var response = new LoginResponseDTO
            {
                Token = token,
                UserId = user.UserId,
                Name = user.FullName,
                Email = user.Email,
                Role = user.Role?.Name ?? "Cliente",
                RestaurantId = restaurantId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erro interno: " + ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize] // Apenas usuários logados podem invalidar sua sessão
    public IActionResult Logout()
    {

        return Ok(new { message = "Logout realizado com sucesso. O token deve ser descartado pelo cliente." });
    }

    private bool VerificarSenha(string senhaDigitada, string senhaHashArmazenada)
    {
        return senhaDigitada == senhaHashArmazenada;
    }
}