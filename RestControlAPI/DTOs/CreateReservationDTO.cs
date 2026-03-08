namespace RestControlAPI.DTOs
{
    public class CreateReservationDTO
    {
        public int RestaurantId { get; set; }
        public int UserId { get; set; }
        public DateTime ReservationDate { get; set; } 
        public int NumberOfPeople { get; set; }
    }
}
