namespace RestControlAPI.DTOs
{
    public class ReservationListDTO
    {
        public int Id { get; set; }
        public string RestaurantName { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
        public int NumberOfPeople { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
