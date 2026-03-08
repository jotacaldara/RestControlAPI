namespace RestControlAPI.DTOs
{
    public class LoginResponseDTO
    {

        public string Token { get; set; }          // ← JWT Token
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }           // "Admin" ou "Owner"
        public int? RestaurantId { get; set; }     // Apenas se for Owner
    }
}
