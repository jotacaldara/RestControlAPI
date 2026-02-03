namespace RestControlAPI.DTOs
{
    public class LoginResponseDTO
    {
        public string Token { get; set; }
        public string Role { get; set; }

        public int UserId { get; set; }
        public string UserName { get; set; }
    }
}
