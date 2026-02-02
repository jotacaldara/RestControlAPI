namespace RestControlAPI.DTOs
{
    public class CreateReviewDTO
    {
        public int ReservationId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
    }
}
