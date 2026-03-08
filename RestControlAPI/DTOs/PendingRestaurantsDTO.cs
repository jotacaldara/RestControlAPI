namespace RestControlAPI.DTOs
{
    public class PendingRestaurantsDTO
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string OwnerName { get; set; }
        public string OwnerEmail { get; set; }
        public string OwnerPhone { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int DaysWaiting { get; set; }

        public int PlanId { get; set; }
        public string PlanName { get; set; }
        public decimal MonthlyPrice { get; set; }
        public decimal CommissionRate { get; set; }
    }
}
