namespace RestControlAPI.DTOs
{
    public class SubscriptionDTO
    {
        public int SubscriptionId { get; set; }
        public string RestaurantName { get; set; }
        public string PlanName { get; set; }
        public decimal Price { get; set; }
        public DateOnly EndDate { get; set; }
        public int DaysRemaining => EndDate.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
    }           
}
