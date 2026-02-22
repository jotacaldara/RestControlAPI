namespace RestControlAPI.DTOs
{
    public class RestaurantRegistrationDTO
    {
        public string OwnerName { get; set; }
        public string OwnerEmail { get; set; }
        public string OwnerPhone { get; set; }
        public string Password { get; set; }
        public string RestaurantName { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string RestaurantPhone { get; set; }
        public string RestaurantEmail { get; set; }
    }
}
