namespace RestControlAPI.DTOs
{

    public class AdminEarningsDTO
    {
        public decimal TotalEarnings { get; set; }

        public decimal TotalReservationCommissions { get; set; }
        public decimal TotalSubscriptionRevenue { get; set; }

        public List<RestaurantEarningItemDTO> ByRestaurant { get; set; } = new List<RestaurantEarningItemDTO>();
        public List<MonthlyEarningItemDTO> ByMonth { get; set; } = new List<MonthlyEarningItemDTO>();
    }

    public class RestaurantEarningItemDTO
    {
        public string RestaurantName { get; set; }
        public int TotalReservations { get; set; }
        public decimal TotalEarnings { get; set; }
    }

    public class MonthlyEarningItemDTO
    {
        public string MonthName { get; set; }
        public decimal Total { get; set; }
    }

    public class AdminSubscriptionListDTO
    {
        public int SubscriptionId { get; set; }
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public int PlanId { get; set; }
        public string PlanName { get; set; }
        public decimal MonthlyPrice { get; set; }
        public decimal Commission { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool IsActive { get; set; }
        public int DaysRemaining { get; set; }
        public bool IsExpiring { get; set; }
    }
}
