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
    public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            // 1. Validar entrada
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "E-mail e senha são obrigatórios." });
            }

            // 2. Buscar usuário no banco
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (user == null)
            {
                return Unauthorized(new { message = "E-mail ou senha inválidos." });
            }

            // 3. Verificar senha com hash
            bool senhaValida = VerificarSenha(request.Password, user.PasswordHash);

            if (!senhaValida)
            {
                return Unauthorized(new { message = "E-mail ou senha inválidos." });
            }

            // 4. Se for Owner, buscar o RestaurantId
            int? restaurantId = null;
            if (user.Role == "Owner")
            {
                var restaurant = await _context.Restaurants
                    .Where(r => r.OwnerId == user.UserId && r.IsActive)
                    .Select(r => r.RestaurantId)
                    .FirstOrDefaultAsync();

                restaurantId = restaurant == 0 ? null : restaurant;
            }

            // 5. Gerar o Token JWT
            string token = _jwtTokenService.GenerateToken(user, restaurantId);

            // 6. Retornar resposta com o Token
            var response = new LoginResponseDTO
            {
                Token = token,
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                RestaurantId = restaurantId
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Erro interno: {ex.Message}" });
        }
    }

    private bool VerificarSenha(string senhaDigitada, string senhaHashArmazenada)
    {
        return senhaDigitada == senhaHashArmazenada;

  
    }
}